using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ObjectStorage.Abstractions.Interfaces;
using ObjectStorage.Abstractions.Models;
using Product.API.Commons.Enums;
using Product.API.Entities;
using Product.API.Options;
using Product.API.Repositories.Interfaces;
using Product.API.Services.V1;
using SkiaSharp;

namespace Product.API.Tests;

public sealed class ProductImageProcessingEdgeTests
{
    private sealed class Fixture
    {
        public readonly CancellationToken Token=TestContext.Current.CancellationToken;
        public readonly Mock<IProductImageUploadRepository> Uploads=new();
        public readonly Mock<IProductImageRepository> Images=new();
        public readonly Mock<IObjectStorage> Storage=new();
        public readonly ProductImageUploads Upload=new(){Id=1,ProductId=1,StagingKey="stage",AssetKey="asset",ExpectedContentType="image/png",Status=ProductImageUploadStatus.Pending,ExpiresAt=DateTimeOffset.UtcNow.AddHours(1),ProcessingStartedAt=DateTimeOffset.UtcNow};
        public ProductImageUploadProcessingService Service=>new(Mock.Of<IProductRepository>(),Images.Object,Uploads.Object,Storage.Object,Microsoft.Extensions.Options.Options.Create(new ProductMediaOptions()),NullLogger<ProductImageUploadProcessingService>.Instance);
        public Fixture()
        {
            Uploads.Setup(r=>r.TryClaimPendingUploadAsync(1,It.IsAny<DateTimeOffset>(),It.IsAny<DateTimeOffset>(),Token)).ReturnsAsync(true);
            Uploads.Setup(r=>r.GetUploadByIdAsync(1,true,It.IsAny<CancellationToken>())).ReturnsAsync(Upload);
            Uploads.Setup(r=>r.GetUnreferencedReadyUploadsAsync(It.IsAny<DateTimeOffset>(),10,Token)).ReturnsAsync([]);
            Uploads.Setup(r=>r.GetCleanupUploadsAsync(10,Token)).ReturnsAsync([Upload]);
            Uploads.Setup(r=>r.TryClaimCleanupUploadAsync(1,It.IsAny<DateTimeOffset>(),It.IsAny<DateTimeOffset>(),Token)).ReturnsAsync(true);
        }
    }

    [Theory]
    [InlineData("source_type")] [InlineData("source_mismatch")] [InlineData("source_size")]
    [InlineData("short_download")] [InlineData("long_download")] [InlineData("io_failure")]
    [InlineData("destination_missing")] [InlineData("destination_size")] [InlineData("destination_type")]
    [InlineData("destination_digest_missing")] [InlineData("destination_digest_wrong")]
    public async Task Invalid_or_unavailable_image_is_rejected_or_retried_without_becoming_ready(string scenario)
    {
        var f=new Fixture();using var bitmap=new SKBitmap(1,1);using var data=bitmap.Encode(SKEncodedImageFormat.Png,100);var bytes=data.ToArray();f.Upload.ExpectedSize=bytes.Length;
        f.Storage.Setup(s=>s.GetMetadataAsync("product-staging","stage",f.Token)).ReturnsAsync(new ObjectMetadata{
            Key="stage",ContentType=scenario=="source_type"?"image/gif":scenario=="source_mismatch"?"image/jpeg":"image/png",ContentLength=scenario=="source_size"?bytes.Length+1:bytes.Length});
        f.Storage.Setup(s=>s.DownloadAsync("product-staging","stage",It.IsAny<Stream>(),f.Token)).Returns((string _,string _,Stream target,CancellationToken token)=>{
            if(scenario=="io_failure")return Task.FromException(new IOException("Download failed"));
            var content=scenario=="short_download"?bytes[..^1]:scenario=="long_download"?bytes.Concat(new byte[]{0}).ToArray():bytes;
            return target.WriteAsync(content.AsMemory(),token).AsTask();
        });
        f.Storage.Setup(s=>s.GetMetadataAsync("product-media","asset",f.Token)).ReturnsAsync(scenario=="destination_missing"?null:new ObjectMetadata{
            Key="asset",ContentLength=scenario=="destination_size"?bytes.Length+1:bytes.Length,ContentType=scenario=="destination_type"?"image/jpeg":"image/png",
            CustomMetadata=scenario=="destination_digest_missing"?new Dictionary<string,string>():new Dictionary<string,string>{{"content-sha256","wrong"}}
        });
        await f.Service.ProcessUploadAsync(f.Upload,f.Token);
        Assert.Equal(scenario=="io_failure"?ProductImageUploadStatus.Pending:ProductImageUploadStatus.Rejected,f.Upload.Status);
        Assert.Null(f.Upload.ProcessingStartedAt);Assert.NotNull(f.Upload.NextAttemptAt);
        f.Uploads.Verify(r=>r.UpdateAsync(f.Upload,CancellationToken.None),Times.Once);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Missing_or_already_completed_claim_does_not_read_storage(bool missing)
    {
        var f=new Fixture();f.Upload.Status=ProductImageUploadStatus.Ready;
        if(missing)f.Uploads.Setup(r=>r.GetUploadByIdAsync(1,true,f.Token)).ReturnsAsync((ProductImageUploads?)null);
        if(missing)await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.ProcessUploadAsync(f.Upload,f.Token));
        else await f.Service.ProcessUploadAsync(f.Upload,f.Token);
        f.Storage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Expired_upload_batch_marks_each_candidate_expired()
    {
        var f=new Fixture();f.Uploads.Setup(r=>r.GetExpiredUploadsAsync(10,f.Token)).ReturnsAsync([f.Upload]);
        await f.Service.ProcessExpiredUploadsAsync(f.Token);
        f.Uploads.Verify(r=>r.MarkExpiredAsync(1,It.IsAny<DateTimeOffset>(),f.Token),Times.Once);
    }

    [Theory]
    [InlineData("future_rejected")] [InlineData("future_ready_stage")] [InlineData("future_ready_deleted")]
    [InlineData("pending_deleted")] [InlineData("rejected_stage")]
    [InlineData("referenced")] [InlineData("missing")] [InlineData("lost_claim")]
    public async Task Cleanup_rechecks_state_before_deleting_objects(string scenario)
    {
        var f=new Fixture();f.Upload.Status=scenario=="pending_deleted"?ProductImageUploadStatus.Pending:scenario.StartsWith("future_ready")?ProductImageUploadStatus.Ready:ProductImageUploadStatus.Rejected;
        f.Upload.NextAttemptAt=scenario.StartsWith("future")?DateTimeOffset.UtcNow.AddHours(1):null;
        f.Upload.StagingDeletedAt=scenario is "future_ready_deleted" or "pending_deleted" or "referenced"?DateTimeOffset.UtcNow.AddHours(-1):null;
        if(scenario=="missing")f.Uploads.Setup(r=>r.GetUploadByIdAsync(1,true,f.Token)).ReturnsAsync((ProductImageUploads?)null);
        if(scenario=="lost_claim")f.Uploads.Setup(r=>r.TryClaimCleanupUploadAsync(1,It.IsAny<DateTimeOffset>(),It.IsAny<DateTimeOffset>(),f.Token)).ReturnsAsync(false);
        f.Storage.Setup(s=>s.ExistsAsync("product-staging","stage",f.Token)).ReturnsAsync(true);
        f.Images.Setup(r=>r.HasAssetReferenceAsync("asset",f.Token)).ReturnsAsync(scenario=="referenced");
        await f.Service.ProcessCleanupAsync(f.Token);
        if(scenario=="rejected_stage"){
            f.Storage.Verify(s=>s.DeleteAsync("product-staging","stage",f.Token),Times.Once);
            f.Storage.Verify(s=>s.DeleteAsync("product-media","asset",f.Token),Times.Once);Assert.Null(f.Upload.AssetKey);
        }
        else f.Storage.Verify(s=>s.DeleteAsync("product-media",It.IsAny<string>(),f.Token),Times.Never);
        if(scenario is "future_rejected" or "future_ready_stage" or "future_ready_deleted")f.Uploads.Verify(r=>r.UpdateAsync(f.Upload,CancellationToken.None),Times.Once);
    }
}
