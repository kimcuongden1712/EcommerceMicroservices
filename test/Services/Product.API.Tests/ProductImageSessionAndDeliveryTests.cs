using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using ObjectStorage.Abstractions.Exceptions;
using ObjectStorage.Abstractions.Interfaces;
using ObjectStorage.Abstractions.Models;
using Product.API.Commons.Enums;
using Product.API.Commons.Exceptions;
using Product.API.Contracts.V1;
using Product.API.Entities;
using Product.API.Options;
using Product.API.Repositories.Interfaces;
using Product.API.Repositories.Models;
using Product.API.Services.V1;

namespace Product.API.Tests;

public sealed class ProductImageSessionAndDeliveryTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Upload_session_commits_only_after_presigned_url_succeeds(bool storageFails)
    {
        var token=TestContext.Current.CancellationToken;
        var products=new Mock<IProductRepository>(MockBehavior.Strict);var uploads=new Mock<IProductImageUploadRepository>(MockBehavior.Strict);var storage=new Mock<IObjectStorage>(MockBehavior.Strict);var transaction=new Mock<IDbContextTransaction>();
        products.Setup(r=>r.BeginTransactionAsync(token)).ReturnsAsync(transaction.Object);
        products.Setup(r=>r.LockProductAsync(1,token)).ReturnsAsync(new Products{Id=1,Name="Name",Slug="slug"});
        products.Setup(r=>r.EndTransactionAsync(token)).Returns(Task.CompletedTask);products.Setup(r=>r.RollbackTransactionAsync(CancellationToken.None)).Returns(Task.CompletedTask);
        uploads.Setup(r=>r.CountActiveUploadSlotsAsync(1,It.IsAny<DateTimeOffset>(),token)).ReturnsAsync(0);
        ProductImageUploads? saved=null;
        uploads.Setup(r=>r.CreateAsync(It.IsAny<ProductImageUploads>(),token)).Callback<ProductImageUploads,CancellationToken>((u,_)=>{u.Id=12;saved=u;}).ReturnsAsync(12);
        var result=new PresignedUrlResult{Url="https://storage.test/upload",ExpiresAt=DateTimeOffset.UtcNow.AddMinutes(10),RequiredHeaders=new Dictionary<string,string>{{"Content-Type","image/png"}}};
        var setup=storage.Setup(r=>r.CreatePresignedUploadUrlAsync("product-staging",It.IsAny<string>(),TimeSpan.FromMinutes(10),"image/png",token));
        if(storageFails)setup.ThrowsAsync(new ObjectStorageException("Presign","product-staging",null,"Unavailable"));else setup.ReturnsAsync(result);
        var service=new ProductImageUploadService(products.Object,uploads.Object,storage.Object,Microsoft.Extensions.Options.Options.Create(new ProductMediaOptions()));
        if(storageFails){await Assert.ThrowsAsync<ObjectStorageException>(()=>service.CreateUploadSessionAsync(1,new(){ContentType=" IMAGE/PNG ",ContentLength=10},token));products.Verify(r=>r.RollbackTransactionAsync(CancellationToken.None),Times.Once);products.Verify(r=>r.EndTransactionAsync(token),Times.Never);}
        else {var response=await service.CreateUploadSessionAsync(1,new(){ContentType=" IMAGE/PNG ",ContentLength=10},token);Assert.Equal("12",response.UploadId);Assert.Equal(result.Url,response.UploadUrl);products.Verify(r=>r.EndTransactionAsync(token),Times.Once);}
        Assert.NotNull(saved);Assert.Equal(ProductImageUploadStatus.Pending,saved.Status);Assert.Equal("image/png",saved.ExpectedContentType);Assert.StartsWith("1/",saved.StagingKey);Assert.True(saved.ExpiresAt>saved.CreatedAt);
        transaction.Verify(r=>r.DisposeAsync(),Times.Once);
    }

    [Theory]
    [InlineData("missing_product")] [InlineData("missing_upload")] [InlineData("Pending")]
    [InlineData("Ready")] [InlineData("Rejected")] [InlineData("Expired")] [InlineData("Cleaned")]
    public async Task Upload_status_is_product_scoped_and_maps_cleaned_to_expired(string state)
    {
        var token=TestContext.Current.CancellationToken;var products=new Mock<IProductRepository>();var uploads=new Mock<IProductImageUploadRepository>();
        if(state!="missing_product")products.Setup(r=>r.GetProductAsync(1,false,token)).ReturnsAsync(new Products{Id=1,Name="Name",Slug="slug"});
        if(!state.StartsWith("missing"))uploads.Setup(r=>r.GetProductImageUploadAsync(1,12,token)).ReturnsAsync(new ProductImageUploads{Id=12,ProductId=1,StagingKey="key",ExpectedContentType="image/png",Status=Enum.Parse<ProductImageUploadStatus>(state)});
        var service=new ProductImageUploadService(products.Object,uploads.Object,Mock.Of<IObjectStorage>(),Microsoft.Extensions.Options.Options.Create(new ProductMediaOptions()));
        var result=await service.GetUploadSessionAsync(1,12,token);
        if(state.StartsWith("missing")){Assert.Null(result);if(state=="missing_product")uploads.VerifyNoOtherCalls();return;}
        Assert.NotNull(result);Assert.Equal("12",result.UploadId);Assert.Equal(state=="Cleaned"?ProductImageUploadStatus.Expired:Enum.Parse<ProductImageUploadStatus>(state),result.Status);
    }

    [Theory]
    [InlineData("missing")] [InlineData("size")] [InlineData("type")] [InlineData("disallowed")]
    public async Task Delivery_metadata_must_match_verified_size_and_allowed_type(string invalid)
    {
        var token=TestContext.Current.CancellationToken;var key=Guid.NewGuid().ToString("N");var images=new Mock<IProductImageRepository>();var storage=new Mock<IObjectStorage>();
        var expectedType=invalid=="disallowed"?"image/gif":"image/png";
        images.Setup(r=>r.GetPublicProductImageAsync(key,token)).ReturnsAsync(new ProductImageDeliveryData(key,expectedType,10));
        storage.Setup(r=>r.GetMetadataAsync("product-media",key,token)).ReturnsAsync(invalid=="missing"?null:new ObjectMetadata{Key=key,ContentLength=invalid=="size"?11:10,ContentType=invalid=="type"?null:expectedType});
        Assert.Null(await new ProductImageDeliveryService(images.Object,storage.Object,Microsoft.Extensions.Options.Options.Create(new ProductMediaOptions())).GetPublicImageMetadataAsync(key,token));
    }

    [Theory]
    [InlineData("storage")] [InlineData("io")] [InlineData("short")] [InlineData("long")]
    public async Task Delivery_stream_rejects_changed_bytes_and_translates_only_storage_errors(string failure)
    {
        var token=TestContext.Current.CancellationToken;var storage=new Mock<IObjectStorage>();
        storage.Setup(r=>r.DownloadAsync("product-media","key",It.IsAny<Stream>(),token)).Returns((string _,string _,Stream target,CancellationToken ct)=>failure switch{
            "storage"=>Task.FromException(new ObjectStorageException("Download","product-media","key","Unavailable")),
            "io"=>Task.FromException(new IOException("Read failed")),
            _=>target.WriteAsync(new byte[failure=="short"?1:3].AsMemory(),ct).AsTask()
        });
        var service=new ProductImageDeliveryService(Mock.Of<IProductImageRepository>(),storage.Object,Microsoft.Extensions.Options.Options.Create(new ProductMediaOptions()));
        var error=await Record.ExceptionAsync(()=>service.OpenPublicImageStreamAsync("key",2,token));
        if(failure=="storage")Assert.Equal(503,Assert.IsType<ProductMediaException>(error).StatusCode);else Assert.IsAssignableFrom<IOException>(error);
    }
}
