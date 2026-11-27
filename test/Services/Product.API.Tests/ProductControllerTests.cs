using System.Security.Claims;
using Infrastructure.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Product.API.Commons.Enums;
using Product.API.Commons.Exceptions;
using Product.API.Contracts.V1;
using Product.API.Controllers.V1;
using Product.API.Repositories.Models;
using Product.API.Services.V1.Interfaces;

namespace Product.API.Tests;

public sealed class ProductControllerTests
{
    private static T Prepare<T>(T controller,string etag="\"product-1-v2\"") where T:ControllerBase
    {
        controller.ControllerContext=new(){HttpContext=new DefaultHttpContext()};
        controller.HttpContext.User=new(new ClaimsIdentity([new Claim("sub","actor")],"test"));
        controller.Request.Headers.IfMatch=etag;controller.Request.Headers["X-Correlation-ID"]="correlation";
        return controller;
    }
    private static readonly AdminCategoryResponse Category=new("1",null,"Category","category","Active",0,3);
    private static readonly AdminProductResponse Product=new("1","1","Product","product","Description","Draft",null,3,[],[]);
    private static readonly AdminProductVariantDetailResponse Variant=new(new("2","SKU","RED","Red","M","Medium",100,"Active",1,1,1,1),3);

    [Theory]
    [InlineData("list")] [InlineData("get")] [InlineData("create")] [InlineData("update")] [InlineData("status")]
    public async Task Category_actions_forward_requests_and_return_resource_etags(string action)
    {
        var service=new Mock<IAdminCatalogService>(MockBehavior.Strict);var token=TestContext.Current.CancellationToken;
        service.Setup(s=>s.GetCategoriesAsync(token)).ReturnsAsync([Category]);service.Setup(s=>s.GetCategoryAsync(1,token)).ReturnsAsync(Category);
        service.Setup(s=>s.CreateCategoryAsync(It.IsAny<CreateCategoryRequest>(),It.Is<CatalogAuditContext>(a=>a.ActorId=="actor"&&a.CorrelationId=="correlation"),token)).ReturnsAsync(Category);
        service.Setup(s=>s.UpdateCategoryAsync(1,2,It.IsAny<UpdateCategoryRequest>(),It.IsAny<CatalogAuditContext>(),token)).ReturnsAsync(Category);
        service.Setup(s=>s.UpdateCategoryStatusAsync(1,2,CategoryStatus.Active,It.IsAny<CatalogAuditContext>(),token)).ReturnsAsync(Category);
        var controller=Prepare(new AdminCategoriesController(service.Object),"\"category-1-v2\"");
        var result=action switch {
            "list"=>await controller.GetCategories(token),"get"=>await controller.GetCategory(1,token),
            "create"=>await controller.CreateCategory(new(),token),"update"=>await controller.UpdateCategory(1,new(),token),
            _=>await controller.UpdateCategoryStatus(1,new(){Status="Active"},token)
        };
        Assert.Equal(action=="create"?201:200,Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        if(action!="list")Assert.Equal("\"category-1-v3\"",controller.Response.Headers.ETag.ToString());
        if(action=="create")Assert.Equal("AdminCategoryByIdV1",Assert.IsType<CreatedAtRouteResult>(result).RouteName);
    }

    [Theory]
    [InlineData("list")] [InlineData("get")] [InlineData("create")] [InlineData("update")] [InlineData("status")]
    [InlineData("variant_create")] [InlineData("variant_get")] [InlineData("variant_update")] [InlineData("variant_status")]
    public async Task Product_actions_forward_versions_and_return_updated_etags(string action)
    {
        var service=new Mock<IAdminCatalogService>(MockBehavior.Strict);var token=TestContext.Current.CancellationToken;
        service.Setup(s=>s.GetProductsAsync(It.IsAny<AdminProductListQuery>(),token)).ReturnsAsync(new AdminProductListResponse([],1,24,false));
        service.Setup(s=>s.GetProductAsync(1,token)).ReturnsAsync(Product);
        service.Setup(s=>s.CreateProductAsync(It.IsAny<CreateProductRequest>(),It.IsAny<CatalogAuditContext>(),token)).ReturnsAsync(Product);
        service.Setup(s=>s.UpdateProductAsync(1,2,It.IsAny<UpdateProductRequest>(),It.IsAny<CatalogAuditContext>(),token)).ReturnsAsync(Product);
        service.Setup(s=>s.UpdateProductStatusAsync(1,2,ProductStatus.Published,It.IsAny<CatalogAuditContext>(),token)).ReturnsAsync(Product);
        service.Setup(s=>s.CreateVariantAsync(1,2,It.IsAny<CreateProductVariantRequest>(),It.IsAny<CatalogAuditContext>(),token)).ReturnsAsync(Variant);
        service.Setup(s=>s.GetVariantAsync(1,2,token)).ReturnsAsync(Variant);
        service.Setup(s=>s.UpdateVariantAsync(1,2,2,It.IsAny<UpdateProductVariantRequest>(),It.IsAny<CatalogAuditContext>(),token)).ReturnsAsync(Variant);
        service.Setup(s=>s.UpdateVariantStatusAsync(1,2,2,ProductVariantStatus.Active,It.IsAny<CatalogAuditContext>(),token)).ReturnsAsync(Variant);
        var controller=Prepare(new AdminProductsController(service.Object));
        var result=action switch {
            "list"=>await controller.GetProducts(new(),token),"get"=>await controller.GetProduct(1,token),
            "create"=>await controller.CreateProduct(new(),token),"update"=>await controller.UpdateProduct(1,new(),token),
            "status"=>await controller.UpdateProductStatus(1,new(){Status="Published"},token),
            "variant_create"=>await controller.CreateVariant(1,new(),token),"variant_get"=>await controller.GetVariant(1,2,token),
            "variant_update"=>await controller.UpdateVariant(1,2,new(),token),
            _=>await controller.UpdateVariantStatus(1,2,new(){Status="Active"},token)
        };
        Assert.Equal(action is "create" or "variant_create"?201:200,Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        if(action!="list")Assert.Equal("\"product-1-v3\"",controller.Response.Headers.ETag.ToString());
    }

    [Theory]
    [InlineData(null,428)] [InlineData(" ",428)] [InlineData("wrong",412)] [InlineData("\"product-2-v1\"",412)]
    [InlineData("\"product-1-v0\"",412)] [InlineData("\"product-1-v-1\"",412)]
    [InlineData("\"product-1-vx\"",412)] [InlineData("\"product-1-v1",412)]
    public async Task Invalid_if_match_never_reaches_service(string? etag,int status)
    {
        var service=new Mock<IAdminCatalogService>(MockBehavior.Strict);
        var controller=Prepare(new AdminProductsController(service.Object),etag!);
        Assert.Equal(status,(await Assert.ThrowsAsync<CatalogAdminException>(()=>controller.UpdateProduct(1,new(),TestContext.Current.CancellationToken))).StatusCode);
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)] [InlineData("999")] [InlineData("unknown")]
    public async Task Invalid_status_is_bad_request(string? status)
    {
        var controller=Prepare(new AdminProductsController(Mock.Of<IAdminCatalogService>()));
        await Assert.ThrowsAsync<BadRequestException>(()=>controller.UpdateProductStatus(1,new(){Status=status},TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)] [InlineData(" ")] [InlineData("too_long")]
    public async Task Missing_or_invalid_actor_is_forbidden(string? actor)
    {
        var service=new Mock<IAdminCatalogService>(MockBehavior.Strict);var controller=Prepare(new AdminCategoriesController(service.Object));
        controller.HttpContext.User=new(new ClaimsIdentity(actor is null?Array.Empty<Claim>():[new Claim("sub",actor=="too_long"?new string('a',129):actor)],"test"));
        Assert.Equal(403,(await Assert.ThrowsAsync<CatalogAdminException>(()=>controller.CreateCategory(new(),TestContext.Current.CancellationToken))).StatusCode);service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Name_identifier_and_request_trace_are_audit_fallbacks()
    {
        var service=new Mock<IAdminCatalogService>();var controller=Prepare(new AdminCategoriesController(service.Object));
        controller.HttpContext.User=new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"fallback")],"test"));
        controller.Request.Headers.Remove("X-Correlation-ID");controller.HttpContext.TraceIdentifier="trace";
        service.Setup(s=>s.CreateCategoryAsync(It.IsAny<CreateCategoryRequest>(),It.Is<CatalogAuditContext>(a=>a.ActorId=="fallback" && a.CorrelationId=="trace"),It.IsAny<CancellationToken>())).ReturnsAsync(Category);
        Assert.IsType<CreatedAtRouteResult>(await controller.CreateCategory(new(),TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)] [InlineData(" ")] [InlineData("too_long")] [InlineData("missing")]
    public async Task Nonpublic_product_is_a_404(string? slug)
    {
        var controller=Prepare(new PublicProductsController(Mock.Of<IPublicCatalogService>()));
        Assert.Equal(404,(await Assert.ThrowsAsync<CatalogAdminException>(()=>controller.GetProduct(slug=="too_long"?new string('a',161):slug!,TestContext.Current.CancellationToken))).StatusCode);
    }

    [Fact]
    public async Task Public_actions_return_service_responses()
    {
        var service=new Mock<IPublicCatalogService>();var token=TestContext.Current.CancellationToken;
        var product=new ProductDetailResponse("1","product","Product","category",null,[],[]);
        service.Setup(s=>s.GetProductBySlugAsync("product",token)).ReturnsAsync(product);
        service.Setup(s=>s.GetProductsAsync(It.IsAny<ProductListQuery>(),token)).ReturnsAsync(new ProductListResponse([],1,24,false));
        service.Setup(s=>s.GetCategoriesAsync(token)).ReturnsAsync([]);
        Assert.Same(product,Assert.IsType<OkObjectResult>((await Prepare(new PublicProductsController(service.Object)).GetProduct("product",token)).Result).Value);
        Assert.IsType<OkObjectResult>((await Prepare(new PublicProductsController(service.Object)).GetProducts(new(),token)).Result);
        var categories=Prepare(new PublicCategoriesController(service.Object));
        Assert.NotNull(await categories.GetCategories(token));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Image_delivery_returns_verified_stream_with_cache_and_security_headers(bool exists)
    {
        var service=new Mock<IProductImageDeliveryService>();var token=TestContext.Current.CancellationToken;
        service.Setup(s=>s.GetPublicImageMetadataAsync("key",token)).ReturnsAsync(exists?new ProductImageDeliveryData("key","image/png",2):null);
        using var bytes=new MemoryStream([1,2]);service.Setup(s=>s.OpenPublicImageStreamAsync("key",2,token)).ReturnsAsync(bytes);
        var controller=Prepare(new ProductImageDeliveryController(service.Object));var result=await controller.GetImage("key",token);
        if(!exists){Assert.IsType<NotFoundResult>(result);return;}
        var file=Assert.IsType<FileStreamResult>(result);Assert.Same(bytes,file.FileStream);Assert.False(file.EnableRangeProcessing);Assert.Equal("image/png",file.ContentType);
        Assert.Equal("2",controller.Response.Headers.ContentLength.ToString());Assert.Equal("nosniff",controller.Response.Headers["X-Content-Type-Options"].ToString());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Image_admin_returns_created_session_and_status_or_404(bool exists)
    {
        var upload=new Mock<IProductImageUploadService>();var gallery=new Mock<IProductImageGalleryService>();var token=TestContext.Current.CancellationToken;
        upload.Setup(s=>s.CreateUploadSessionAsync(1,It.IsAny<CreateProductImageUploadRequest>(),token)).ReturnsAsync(new ProductImageUploadResponse("2","url",new Dictionary<string,string>(),DateTimeOffset.UtcNow));
        upload.Setup(s=>s.GetUploadSessionAsync(1,2,token)).ReturnsAsync(exists?new ProductImageUploadStatusResponse("2",ProductImageUploadStatus.Ready,DateTimeOffset.UtcNow):null);
        gallery.Setup(s=>s.ReplaceGalleryAsync(1,2,It.IsAny<ReplaceProductImagesRequest>(),It.IsAny<CatalogAuditContext>(),token)).ReturnsAsync(new ProductImageGalleryResult(3,[]));
        var controller=Prepare(new ProductImageAdminController(upload.Object,gallery.Object));
        Assert.IsType<CreatedAtActionResult>(await controller.CreateImageUploadSession(1,new(),token));
        Assert.Equal(exists?typeof(OkObjectResult):typeof(NotFoundResult),(await controller.GetImageUploadStatus(1,2,token)).GetType());
        Assert.IsType<OkObjectResult>(await controller.ReplaceProductImages(1,new(){Images=[]},token));Assert.Equal("\"product-1-v3\"",controller.Response.Headers.ETag.ToString());
    }
}
