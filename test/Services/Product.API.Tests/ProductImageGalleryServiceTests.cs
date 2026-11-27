using System.Linq.Expressions;
using Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Product.API.Commons.Enums;
using Product.API.Commons.Exceptions;
using Product.API.Contracts.V1;
using Product.API.Entities;
using Product.API.Options;
using Product.API.Persistence;
using Product.API.Repositories.Interfaces;
using Product.API.Services.V1;
using Product.API.Services.V1.Interfaces;

namespace Product.API.Tests;

public sealed class ProductImageGalleryServiceTests
{
    private sealed class Fixture : IDisposable
    {
        public readonly ProductContext Context=new(new DbContextOptionsBuilder<ProductContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public readonly Mock<IProductRepository> Products=new();
        public readonly Mock<IProductImageRepository> Images=new();
        public readonly Mock<IProductImageUploadRepository> Uploads=new();
        public readonly Mock<ICatalogAuditRepository> Audit=new();
        public readonly Mock<ICatalogOutboxRepository> Outbox=new();
        public readonly Mock<IDbContextTransaction> Transaction=new();
        public readonly Products Product=new(){Id=1,CategoryId=1,Name="Product",Slug="product",Version=2};
        public readonly List<ProductImages> Created=[];
        public readonly List<ProductImageUploads> Sessions=[];
        public readonly CatalogAuditContext Actor=new("actor","correlation");
        public ProductImageGalleryService Service=>new(Products.Object,Images.Object,Uploads.Object,Audit.Object,Outbox.Object,Microsoft.Extensions.Options.Options.Create(new ProductMediaOptions()));
        public Fixture()
        {
            Products.Setup(r=>r.LockProductAsync(1,TestContext.Current.CancellationToken)).ReturnsAsync(Product);
            Images.Setup(r=>r.BeginTransactionAsync(TestContext.Current.CancellationToken)).ReturnsAsync(Transaction.Object);
            Images.Setup(r=>r.FindByCondition(It.IsAny<Expression<Func<ProductImages,bool>>>(),false))
                .Returns((Expression<Func<ProductImages,bool>> predicate,bool _) => Context.ProductImages.AsNoTracking().Where(predicate));
            Images.Setup(r=>r.Create(It.IsAny<ProductImages>())).Callback<ProductImages>(Created.Add);
            Images.Setup(r=>r.GetProductImagesAsync(1,TestContext.Current.CancellationToken)).ReturnsAsync([]);
            Uploads.Setup(r=>r.GetProductImageUploadAsync(1,It.IsAny<long>(),TestContext.Current.CancellationToken)).ReturnsAsync((long _,long id,CancellationToken _) => Sessions.SingleOrDefault(s=>s.Id==id));
            Sessions.Add(new(){Id=1,ProductId=1,StagingKey="staging",AssetKey="new",ExpectedContentType="image/png",Status=ProductImageUploadStatus.Ready});
        }
        public void Dispose()=>Context.Dispose();
    }

    [Theory]
    [InlineData("invalid_id")] [InlineData("zero_id")] [InlineData("duplicate_id")]
    [InlineData("alt_text")] [InlineData("image_limit")]
    public async Task Invalid_request_is_rejected_before_transaction(string scenario)
    {
        using var f=new Fixture();
        var images=scenario switch {
            "invalid_id"=>new[]{new ProductImageItemRequest{UploadId="abc"}},
            "zero_id"=>[new ProductImageItemRequest{UploadId="0"}],
            "duplicate_id"=>[new ProductImageItemRequest{UploadId="1"},new ProductImageItemRequest{UploadId="1"}],
            "alt_text"=>[new ProductImageItemRequest{UploadId="1",AltText=new string('a',161)}],
            _=>Enumerable.Range(1,11).Select(i=>new ProductImageItemRequest{UploadId=i.ToString()}).ToArray()
        };
        Assert.IsAssignableFrom<ApiException>(await Record.ExceptionAsync(()=>f.Service.ReplaceGalleryAsync(1,2,new(){Images=images},f.Actor,TestContext.Current.CancellationToken)));
        f.Images.Verify(r=>r.BeginTransactionAsync(It.IsAny<CancellationToken>()),Times.Never);
    }

    [Theory]
    [InlineData("missing_product")] [InlineData("stale_version")] [InlineData("published_empty")]
    [InlineData("missing_upload")] [InlineData("pending_upload")] [InlineData("missing_asset")]
    [InlineData("duplicate_asset")] [InlineData("concurrency")]
    public async Task Invalid_gallery_state_rolls_back_and_disposes_transaction(string scenario)
    {
        using var f=new Fixture();long expected=2;
        var request=new ReplaceProductImagesRequest{Images=[new(){UploadId="1"}]};
        if(scenario=="missing_product")f.Products.Setup(r=>r.LockProductAsync(1,TestContext.Current.CancellationToken)).ReturnsAsync((Products?)null);
        if(scenario=="stale_version")expected=99;
        if(scenario=="published_empty"){f.Product.Status=ProductStatus.Published;request=new(){Images=[]};}
        if(scenario=="missing_upload")f.Sessions.Clear();
        if(scenario=="pending_upload")f.Sessions[0].Status=ProductImageUploadStatus.Pending;
        if(scenario=="missing_asset")f.Sessions[0].AssetKey=" ";
        if(scenario=="duplicate_asset"){
            f.Sessions.Add(new(){Id=2,ProductId=1,AssetKey="new",StagingKey="second",ExpectedContentType="image/png",Status=ProductImageUploadStatus.Ready});
            request=new(){Images=[new(){UploadId="1"},new(){UploadId="2"}]};
        }
        if(scenario=="concurrency")f.Products.Setup(r=>r.LockProductAsync(1,TestContext.Current.CancellationToken)).ThrowsAsync(new DbUpdateConcurrencyException());
        var error=await Record.ExceptionAsync(()=>f.Service.ReplaceGalleryAsync(1,expected,request,f.Actor,TestContext.Current.CancellationToken));Assert.IsAssignableFrom<ApiException>(error);
        if(scenario is "concurrency" or "stale_version")Assert.Equal(412,((ApiException)error!).StatusCode);
        f.Images.Verify(r=>r.RollbackTransactionAsync(CancellationToken.None),Times.Once);
        f.Images.Verify(r=>r.EndTransactionAsync(It.IsAny<CancellationToken>()),Times.Never);
        f.Transaction.Verify(t=>t.DisposeAsync(),Times.Once);Assert.Empty(f.Created);
    }

    [Theory]
    [InlineData(null)] [InlineData(" Alt ")]
    public async Task Replacing_gallery_preserves_order_schedules_removed_assets_and_audits(string? alt)
    {
        using var f=new Fixture();f.Context.ProductImages.Add(new(){Id=5,ProductId=1,AssetKey="old",AltText="Old",Position=0});await f.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var response=await f.Service.ReplaceGalleryAsync(1,2,new(){Images=[new(){UploadId="1",AltText=alt}]},f.Actor,TestContext.Current.CancellationToken);
        Assert.Equal(3,response.ProductVersion);var image=Assert.Single(f.Created);Assert.Equal("new",image.AssetKey);Assert.Equal(alt?.Trim()??"",image.AltText);Assert.Equal(0,image.Position);
        f.Uploads.Verify(r=>r.MarkAssetUnreferencedForCleanupAsync("old",It.Is<DateTimeOffset>(d=>d>DateTimeOffset.UtcNow),TestContext.Current.CancellationToken),Times.Once);
        f.Audit.Verify(r=>r.Create(It.Is<CatalogAudit>(a=>a.Action=="ImagesReplaced" && a.ActorId=="actor")),Times.Once);
        f.Outbox.Verify(r=>r.Create(It.Is<CatalogOutbox>(o=>o.Topic=="catalog.product.changed.v1")),Times.Once);
        f.Images.Verify(r=>r.EndTransactionAsync(TestContext.Current.CancellationToken),Times.Once);f.Transaction.Verify(t=>t.DisposeAsync(),Times.Once);
    }
}
