using System.Text.Json;
using Infrastructure.Exceptions;
using Infrastructure.Common.Models;
using Microsoft.EntityFrameworkCore;
using Moq;
using Product.API.Commons.Enums;
using Product.API.Commons.Exceptions;
using Product.API.Contracts.V1;
using Product.API.Entities;
using Product.API.Repositories.Interfaces;
using Product.API.Repositories.Models;
using Product.API.Services.V1;
using Product.API.Services.V1.Interfaces;

namespace Product.API.Tests;

public sealed class AdminCatalogServiceTests
{
    private sealed class Fixture
    {
        public readonly Mock<ICategoryRepository> Categories=new();
        public readonly Mock<IProductRepository> Products=new();
        public readonly Mock<IProductVariantsRepository> Variants=new();
        public readonly Mock<IProductImageRepository> Images=new();
        public readonly Mock<ICatalogAuditRepository> Audit=new();
        public readonly Mock<ICatalogOutboxRepository> Outbox=new();
        public readonly List<CatalogAudit> Audits=[];
        public readonly List<CatalogOutbox> Events=[];
        public readonly Categories Category=new(){Id=1,Name="Category",Slug="category",Status=CategoryStatus.Active,Version=1};
        public readonly Products Product=new(){Id=2,CategoryId=1,Name="Product",Slug="product",Description="Description",Version=1};
        public readonly ProductVariants Variant=new(){Id=3,ProductId=2,Sku="SKU",ColorCode="RED",ColorLabel="Red",SizeCode="M",SizeLabel="Medium",PriceMinor=100,WeightG=1,LengthMm=1,WidthMm=1,HeightMm=1};
        public readonly CatalogAuditContext Actor=new("actor","correlation");
        public AdminCatalogService Service => new(Categories.Object,Products.Object,Variants.Object,Images.Object,Audit.Object,Outbox.Object);
        public Fixture()
        {
            Categories.Setup(r=>r.GetCategoryAsync(It.IsAny<long>(),It.IsAny<bool>(),It.IsAny<CancellationToken>())).ReturnsAsync((long id,bool _,CancellationToken _) => id==1 ? Category : new Categories{Id=id,Name="Parent",Slug="parent",Status=CategoryStatus.Active});
            Categories.Setup(r=>r.GetCategoryHierarchyAsync(It.IsAny<IEnumerable<long>>(),It.IsAny<CancellationToken>())).ReturnsAsync((IEnumerable<long> ids,CancellationToken _) => ids.Distinct().Select(id=>new CategoryHierarchyItem(id,id==1 ? Category.ParentId : null)).ToArray());
            Categories.Setup(r=>r.LockCategoryAsync(It.IsAny<long>(),It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Categories.Setup(r=>r.Create(It.IsAny<Categories>())).Callback<Categories>(c=>c.Id=10);
            Categories.Setup(r=>r.GetCategoriesByAdminAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Category]);
            Products.Setup(r=>r.GetProductCategoryIdAsync(2,It.IsAny<CancellationToken>())).ReturnsAsync(1);
            Products.Setup(r=>r.LockProductAsync(2,It.IsAny<CancellationToken>())).ReturnsAsync(Product);
            Products.Setup(r=>r.GetProductAsync(2,It.IsAny<bool>(),It.IsAny<CancellationToken>())).ReturnsAsync(Product);
            Products.Setup(r=>r.Create(It.IsAny<Products>())).Callback<Products>(p=>p.Id=2);
            Variants.Setup(r=>r.GetVariantsByProductAsync(It.IsAny<long>(),It.IsAny<bool>(),It.IsAny<CancellationToken>())).ReturnsAsync([Variant]);
            Variants.Setup(r=>r.GetProductVariantAsync(2,3,It.IsAny<bool>(),It.IsAny<CancellationToken>())).ReturnsAsync(Variant);
            Variants.Setup(r=>r.Create(It.IsAny<ProductVariants>())).Callback<ProductVariants>(v=>v.Id=3);
            Images.Setup(r=>r.GetProductImagesAsync(It.IsAny<long>(),It.IsAny<CancellationToken>())).ReturnsAsync([new("4","asset","Alt",0)]);
            Images.Setup(r=>r.HasReadyPrimaryImageAsync(2,It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Audit.Setup(r=>r.Create(It.IsAny<CatalogAudit>())).Callback<CatalogAudit>(Audits.Add);
            Outbox.Setup(r=>r.Create(It.IsAny<CatalogOutbox>())).Callback<CatalogOutbox>(Events.Add);
        }
        public void AssertEvent(string action)
        {
            var audit=Assert.Single(Audits);Assert.Equal(action,audit.Action);Assert.Equal("actor",audit.ActorId);
            using var json=JsonDocument.Parse(Assert.Single(Events).PayloadJson);
            Assert.Equal("correlation",json.RootElement.GetProperty("correlationId").GetString());
        }
    }

    [Fact]
    public async Task Reads_map_categories_products_variants_and_pagination()
    {
        var f=new Fixture();var service=f.Service;
        Assert.Equal("1",Assert.Single(await service.GetCategoriesAsync(TestContext.Current.CancellationToken)).Id);
        Assert.Equal("Category",(await service.GetCategoryAsync(1,TestContext.Current.CancellationToken)).Name);
        Assert.Equal("SKU",Assert.Single((await service.GetProductAsync(2,TestContext.Current.CancellationToken)).Variants).Sku);
        Assert.Equal("3",(await service.GetVariantAsync(2,3,TestContext.Current.CancellationToken)).Variant.Id);
        var query=new AdminProductListQuery(null,null,null,1,1);
        f.Products.Setup(r=>r.GetProductsByAdminAsync(query,TestContext.Current.CancellationToken)).ReturnsAsync(new PagedList<AdminProductListData>([new(2,1,"Product","product",ProductStatus.Draft,1)],2,1,1));
        var page=await service.GetProductsAsync(query,TestContext.Current.CancellationToken);Assert.True(page.HasNext);Assert.Equal("2",Assert.Single(page.Items).Id);
    }

    [Theory]
    [InlineData("category")] [InlineData("product")] [InlineData("variant")]
    public async Task Missing_resources_are_not_found(string resource)
    {
        var f=new Fixture();
        f.Categories.Setup(r=>r.GetCategoryAsync(1,false,TestContext.Current.CancellationToken)).ReturnsAsync((Categories?)null);
        var error=await Assert.ThrowsAsync<CatalogAdminException>(async()=> {
            if(resource=="category")await f.Service.GetCategoryAsync(1,TestContext.Current.CancellationToken);
            else if(resource=="product")await f.Service.GetProductAsync(999,TestContext.Current.CancellationToken);
            else await f.Service.GetVariantAsync(2,999,TestContext.Current.CancellationToken);
        });
        Assert.Equal(404,error.StatusCode);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Creating_category_normalizes_fields_and_records_audit(bool child)
    {
        var f=new Fixture();var result=await f.Service.CreateCategoryAsync(new(){Name=" New ",Slug=" NEW ",SortOrder=2,ParentId=child ? 9 : null},f.Actor,TestContext.Current.CancellationToken);
        Assert.Equal("New",result.Name);Assert.Equal("new",result.Slug);Assert.Equal("Inactive",result.Status);
        f.AssertEvent("Created");f.Categories.Verify(r=>r.EndTransactionAsync(CancellationToken.None),Times.Once);
    }

    [Theory]
    [InlineData("duplicate")] [InlineData("missing_parent")] [InlineData("nested_parent")] [InlineData("lock_failure")]
    public async Task Invalid_category_creation_rolls_back(string scenario)
    {
        var f=new Fixture();
        if(scenario=="duplicate")f.Categories.Setup(r=>r.CategorySlugExistsAsync("new",null,TestContext.Current.CancellationToken)).ReturnsAsync(true);
        if(scenario=="missing_parent")f.Categories.Setup(r=>r.GetCategoryHierarchyAsync(It.IsAny<IEnumerable<long>>(),TestContext.Current.CancellationToken)).ReturnsAsync(Array.Empty<CategoryHierarchyItem>());
        if(scenario=="nested_parent")f.Category.ParentId=9;
        if(scenario=="lock_failure")f.Categories.Setup(r=>r.LockCategoryAsync(It.IsAny<long>(),TestContext.Current.CancellationToken)).ReturnsAsync(false);
        await Assert.ThrowsAsync<CatalogAdminException>(()=>f.Service.CreateCategoryAsync(new(){Name="New",Slug="new",SortOrder=0,ParentId=1},f.Actor,TestContext.Current.CancellationToken));
        f.Categories.Verify(r=>r.RollbackTransactionAsync(CancellationToken.None),Times.Once);Assert.Empty(f.Events);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Category_update_increments_version_only_when_changed(bool changed)
    {
        var f=new Fixture();var result=await f.Service.UpdateCategoryAsync(1,1,new(){Name=changed ? "Changed" : "Category",Slug="category",SortOrder=0},f.Actor,TestContext.Current.CancellationToken);
        Assert.Equal(changed ? 2 : 1,result.Version);
        if(changed)f.AssertEvent("Updated");else Assert.Empty(f.Audits);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Category_slug_cannot_change_after_activation(bool active)
    {
        var f=new Fixture();f.Category.Status=active ? CategoryStatus.Active : CategoryStatus.Inactive;
        f.Audit.Setup(r=>r.WasCategoryActivatedAsync(1,1,TestContext.Current.CancellationToken)).ReturnsAsync(true);
        var error=await Assert.ThrowsAsync<CatalogAdminException>(()=>f.Service.UpdateCategoryAsync(1,1,new(){Name="Category",Slug="new"},f.Actor,TestContext.Current.CancellationToken));
        Assert.Equal("CATEGORY_SLUG_IMMUTABLE",error.Code);
    }

    [Theory]
    [InlineData(CategoryStatus.Active,CategoryStatus.Active)]
    [InlineData(CategoryStatus.Inactive,CategoryStatus.Active)]
    [InlineData(CategoryStatus.Active,CategoryStatus.Inactive)]
    public async Task Valid_category_status_transitions_are_audited(CategoryStatus from,CategoryStatus to)
    {
        var f=new Fixture();f.Category.Status=from;
        var result=await f.Service.UpdateCategoryStatusAsync(1,1,to,f.Actor,TestContext.Current.CancellationToken);Assert.Equal(to.ToString(),result.Status);
        if(from==to)Assert.Empty(f.Audits);else f.AssertEvent(to==CategoryStatus.Active ? "Activated" : "Deactivated");
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Category_with_active_dependents_cannot_be_deactivated(bool publishedProducts)
    {
        var f=new Fixture();
        f.Categories.Setup(r=>r.HasActiveCategoryChildrenAsync(1,TestContext.Current.CancellationToken)).ReturnsAsync(!publishedProducts);
        f.Products.Setup(r=>r.HasPublishedProductsInCategoryAsync(1,TestContext.Current.CancellationToken)).ReturnsAsync(publishedProducts);
        Assert.Equal("CATEGORY_HAS_ACTIVE_DEPENDENTS",(await Assert.ThrowsAsync<CatalogAdminException>(()=>f.Service.UpdateCategoryStatusAsync(1,1,CategoryStatus.Inactive,f.Actor,TestContext.Current.CancellationToken))).Code);
    }

    [Fact]
    public async Task Creating_product_defaults_to_draft_and_creates_outbox()
    {
        var f=new Fixture();var result=await f.Service.CreateProductAsync(new(){Name=" New ",Slug=" NEW ",CategoryId=1,Description=" Description "},f.Actor,TestContext.Current.CancellationToken);
        Assert.Equal("Draft",result.Status);Assert.Equal("new",result.Slug);Assert.Equal("Description",result.Description);f.AssertEvent("Created");
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Product_update_increments_version_only_on_change(bool changed)
    {
        var f=new Fixture();var result=await f.Service.UpdateProductAsync(2,1,new(){Name=changed ? "Changed" : "Product",Slug="product",CategoryId=1,Description="Description"},f.Actor,TestContext.Current.CancellationToken);
        Assert.Equal(changed ? 2 : 1,result.Version);if(changed)f.AssertEvent("Updated");else Assert.Empty(f.Audits);
    }

    [Theory]
    [InlineData(ProductStatus.Draft,ProductStatus.Published)]
    [InlineData(ProductStatus.Published,ProductStatus.Unpublished)]
    [InlineData(ProductStatus.Published,ProductStatus.Published)]
    public async Task Product_status_preserves_invariants_and_records_audit(ProductStatus from,ProductStatus to)
    {
        var f=new Fixture();f.Product.Status=from;
        var response=await f.Service.UpdateProductStatusAsync(2,1,to,f.Actor,TestContext.Current.CancellationToken);
        Assert.Equal(to.ToString(),response.Status);
        if(from==to)Assert.Empty(f.Events);else f.AssertEvent(to.ToString());
        if(from==ProductStatus.Draft && to==ProductStatus.Published)Assert.NotNull(response.PublishedAt);
    }

    [Theory]
    [InlineData("description")] [InlineData("category")] [InlineData("image")] [InlineData("variant")]
    [InlineData("dimensions")]
    public async Task Incomplete_product_cannot_be_published(string missing)
    {
        var f=new Fixture();
        if(missing=="description")f.Product.Description=" ";
        if(missing=="category")f.Category.Status=CategoryStatus.Inactive;
        if(missing=="image")f.Images.Setup(r=>r.HasReadyPrimaryImageAsync(2,TestContext.Current.CancellationToken)).ReturnsAsync(false);
        if(missing=="variant")f.Variants.Setup(r=>r.GetVariantsByProductAsync(2,true,TestContext.Current.CancellationToken)).ReturnsAsync(Array.Empty<ProductVariants>());
        if(missing=="dimensions")f.Variant.WeightG=null;
        var error=await Record.ExceptionAsync(()=>f.Service.UpdateProductStatusAsync(2,1,ProductStatus.Published,f.Actor,TestContext.Current.CancellationToken));
        Assert.IsAssignableFrom<ApiException>(error);Assert.Empty(f.Events);f.Products.Verify(r=>r.RollbackTransactionAsync(CancellationToken.None),Times.Once);
    }

    [Theory]
    [InlineData(ProductVariantStatus.Draft,ProductVariantStatus.Active,true)]
    [InlineData(ProductVariantStatus.Active,ProductVariantStatus.Retired,true)]
    [InlineData(ProductVariantStatus.Active,ProductVariantStatus.Active,true)]
    [InlineData(ProductVariantStatus.Retired,ProductVariantStatus.Active,false)]
    [InlineData(ProductVariantStatus.Draft,ProductVariantStatus.Retired,false)]
    [InlineData(ProductVariantStatus.Active,ProductVariantStatus.Draft,false)]
    public async Task Variant_status_transition_rules(ProductVariantStatus from,ProductVariantStatus to,bool valid)
    {
        var f=new Fixture();f.Variant.Status=from;
        if(!valid){Assert.Equal("INVALID_VARIANT_TRANSITION",(await Assert.ThrowsAsync<CatalogAdminException>(()=>f.Service.UpdateVariantStatusAsync(2,3,1,to,f.Actor,TestContext.Current.CancellationToken))).Code);return;}
        var result=await f.Service.UpdateVariantStatusAsync(2,3,1,to,f.Actor,TestContext.Current.CancellationToken);Assert.Equal(to.ToString(),result.Variant.Status);
        if(from!=to)f.AssertEvent(to==ProductVariantStatus.Active ? "Activated" : "Retired");
    }

    [Fact]
    public async Task Published_product_cannot_retire_its_last_active_variant()
    {
        var f=new Fixture();f.Product.Status=ProductStatus.Published;f.Variant.Status=ProductVariantStatus.Active;
        Assert.Equal("PUBLISHED_PRODUCT_REQUIRES_ACTIVE_VARIANT",(await Assert.ThrowsAsync<CatalogAdminException>(()=>f.Service.UpdateVariantStatusAsync(2,3,1,ProductVariantStatus.Retired,f.Actor,TestContext.Current.CancellationToken))).Code);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Variant_creation_normalizes_identity_and_detects_duplicate_sku(bool duplicate)
    {
        var f=new Fixture();f.Variants.Setup(r=>r.VariantSkuExistsAsync("SKU",null,TestContext.Current.CancellationToken)).ReturnsAsync(duplicate);
        var request=new CreateProductVariantRequest{Sku=" sku ",ColorCode=" red ",SizeCode=" m ",ColorLabel=" Red ",SizeLabel=" Medium "};
        if(duplicate){Assert.Equal("SKU_ALREADY_EXISTS",(await Assert.ThrowsAsync<CatalogAdminException>(()=>f.Service.CreateVariantAsync(2,1,request,f.Actor,TestContext.Current.CancellationToken))).Code);return;}
        var result=await f.Service.CreateVariantAsync(2,1,request,f.Actor,TestContext.Current.CancellationToken);Assert.Equal("SKU",result.Variant.Sku);Assert.Equal(2,result.ProductVersion);f.AssertEvent("Created");
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Variant_update_changes_version_only_for_changed_fields(bool changed)
    {
        var f=new Fixture();var result=await f.Service.UpdateVariantAsync(2,3,1,new(){ColorLabel=changed ? "Blue" : "Red",SizeLabel="Medium",PriceMinor=100,WeightG=1,LengthMm=1,WidthMm=1,HeightMm=1},f.Actor,TestContext.Current.CancellationToken);
        Assert.Equal(changed ? 2 : 1,result.ProductVersion);if(changed)f.AssertEvent("Updated");else Assert.Empty(f.Events);
    }

    [Theory]
    [InlineData("category")] [InlineData("product")] [InlineData("variant")]
    public async Task Stale_versions_are_rejected_and_rolled_back(string resource)
    {
        var f=new Fixture();var error=await Assert.ThrowsAsync<CatalogAdminException>(async()=> {
            if(resource=="category")await f.Service.UpdateCategoryAsync(1,9,new(){Slug="category"},f.Actor,TestContext.Current.CancellationToken);
            else if(resource=="product")await f.Service.UpdateProductAsync(2,9,new(){CategoryId=1},f.Actor,TestContext.Current.CancellationToken);
            else await f.Service.UpdateVariantStatusAsync(2,3,9,ProductVariantStatus.Active,f.Actor,TestContext.Current.CancellationToken);
        });Assert.Equal(412,error.StatusCode);Assert.Empty(f.Events);
    }

    [Fact]
    public async Task Persistence_concurrency_error_is_mapped_to_precondition_failed()
    {
        var f=new Fixture();f.Categories.Setup(r=>r.SaveChangesAsync(CancellationToken.None)).ThrowsAsync(new DbUpdateConcurrencyException());
        Assert.Equal(412,(await Assert.ThrowsAsync<CatalogAdminException>(()=>f.Service.CreateCategoryAsync(new(){Name="Name",Slug="slug"},f.Actor,TestContext.Current.CancellationToken))).StatusCode);
    }

    [Theory]
    [InlineData("active",false)] [InlineData("inactive",true)] [InlineData("nested",true)] [InlineData("missing",true)]
    public async Task Parent_category_must_be_an_active_root_before_activation(string parentState,bool rejected)
    {
        var f=new Fixture();var ct=TestContext.Current.CancellationToken;f.Category.Status=CategoryStatus.Inactive;f.Category.ParentId=9;
        f.Categories.Setup(r=>r.GetCategoryAsync(9,true,ct)).ReturnsAsync(parentState=="missing"?null:new Categories{Id=9,Name="Parent",Slug="parent",Status=parentState=="inactive"?CategoryStatus.Inactive:CategoryStatus.Active,ParentId=parentState=="nested"?10:null});
        if(rejected){await Assert.ThrowsAsync<CatalogAdminException>(()=>f.Service.UpdateCategoryStatusAsync(1,1,CategoryStatus.Active,f.Actor,ct));Assert.Empty(f.Events);}
        else Assert.Equal("Active",(await f.Service.UpdateCategoryStatusAsync(1,1,CategoryStatus.Active,f.Actor,ct)).Status);
    }

    [Fact]
    public async Task Previously_inactive_category_can_change_to_an_available_slug()
    {
        var f=new Fixture();f.Category.Status=CategoryStatus.Inactive;
        var result=await f.Service.UpdateCategoryAsync(1,1,new(){Name="Category",Slug="new",SortOrder=0},f.Actor,TestContext.Current.CancellationToken);
        Assert.Equal("new",result.Slug);f.AssertEvent("Updated");
    }

    [Theory]
    [InlineData("current_published")] [InlineData("previous_date")] [InlineData("audit_history")]
    [InlineData("duplicate")] [InlineData("available")]
    public async Task Product_slug_changes_respect_publication_history_and_uniqueness(string scenario)
    {
        var f=new Fixture();var ct=TestContext.Current.CancellationToken;
        if(scenario=="current_published")f.Product.Status=ProductStatus.Published;
        if(scenario=="previous_date")f.Product.PublishedAt=DateTimeOffset.UtcNow;
        if(scenario=="audit_history")f.Audit.Setup(r=>r.WasProductPublishedAsync(2,2,ct)).ReturnsAsync(true);
        if(scenario=="duplicate")f.Products.Setup(r=>r.ProductSlugExistsAsync("new",2,ct)).ReturnsAsync(true);
        var request=new UpdateProductRequest{Name="Product",Slug="new",CategoryId=1,Description="Description"};
        if(scenario=="available"){Assert.Equal("new",(await f.Service.UpdateProductAsync(2,1,request,f.Actor,ct)).Slug);return;}
        var error=await Assert.ThrowsAsync<CatalogAdminException>(()=>f.Service.UpdateProductAsync(2,1,request,f.Actor,ct));
        Assert.Equal(scenario=="duplicate"?"SLUG_ALREADY_EXISTS":"PRODUCT_SLUG_IMMUTABLE",error.Code);
    }

    [Theory]
    [InlineData("missing_category")] [InlineData("duplicate_slug")] [InlineData("missing_product")]
    [InlineData("lost_product")]
    public async Task Missing_or_conflicting_product_dependencies_are_rejected(string scenario)
    {
        var f=new Fixture();var ct=TestContext.Current.CancellationToken;
        if(scenario=="missing_category")f.Categories.Setup(r=>r.GetCategoryAsync(1,It.IsAny<bool>(),ct)).ReturnsAsync((Categories?)null);
        if(scenario=="duplicate_slug")f.Products.Setup(r=>r.ProductSlugExistsAsync("new",null,ct)).ReturnsAsync(true);
        if(scenario=="missing_product")f.Products.Setup(r=>r.GetProductCategoryIdAsync(2,ct)).ReturnsAsync((long?)null);
        if(scenario=="lost_product")f.Products.Setup(r=>r.LockProductAsync(2,ct)).ReturnsAsync((Products?)null);
        await Assert.ThrowsAsync<CatalogAdminException>(async()=>{
            if(scenario=="duplicate_slug")await f.Service.CreateProductAsync(new(){Name="New",Slug="new",CategoryId=1},f.Actor,ct);
            else await f.Service.UpdateProductAsync(2,1,new(){Name="Name",Slug="product",CategoryId=1},f.Actor,ct);
        });f.Products.Verify(r=>r.RollbackTransactionAsync(CancellationToken.None),Times.Once);Assert.Empty(f.Events);
    }

    [Theory]
    [InlineData(ProductStatus.Draft,ProductStatus.Unpublished)] [InlineData(ProductStatus.Published,ProductStatus.Draft)]
    public async Task Product_cannot_transition_back_to_draft_or_unpublish_a_draft(ProductStatus from,ProductStatus to)
    {
        var f=new Fixture();f.Product.Status=from;
        Assert.Equal("INVALID_PRODUCT_TRANSITION",(await Assert.ThrowsAsync<CatalogAdminException>(()=>f.Service.UpdateProductStatusAsync(2,1,to,f.Actor,TestContext.Current.CancellationToken))).Code);
    }

    [Theory]
    [InlineData("active")] [InlineData("inactive")] [InlineData("nested")]
    public async Task Publishing_checks_parent_category_and_updates_remain_publishable(string parentState)
    {
        var f=new Fixture();var ct=TestContext.Current.CancellationToken;f.Category.ParentId=9;f.Product.Status=ProductStatus.Published;
        f.Categories.Setup(r=>r.GetCategoryAsync(9,true,ct)).ReturnsAsync(new Categories{Id=9,Name="Parent",Slug="parent",Status=parentState=="inactive"?CategoryStatus.Inactive:CategoryStatus.Active,ParentId=parentState=="nested"?10:null});
        var request=new UpdateProductRequest{Name="Changed",Slug="product",CategoryId=1,Description="Description"};
        if(parentState=="active")Assert.Equal(2,(await f.Service.UpdateProductAsync(2,1,request,f.Actor,ct)).Version);
        else await Assert.ThrowsAsync<CatalogAdminException>(()=>f.Service.UpdateProductAsync(2,1,request,f.Actor,ct));
    }

    [Theory]
    [InlineData("retired")] [InlineData("invalid_dimensions")] [InlineData("missing")]
    public async Task Invalid_variant_updates_roll_back_without_audit(string scenario)
    {
        var f=new Fixture();var ct=TestContext.Current.CancellationToken;
        f.Variant.Status=scenario=="retired"?ProductVariantStatus.Retired:ProductVariantStatus.Active;
        if(scenario=="missing")f.Variants.Setup(r=>r.GetProductVariantAsync(2,3,true,ct)).ReturnsAsync((ProductVariants?)null);
        var error=await Record.ExceptionAsync(()=>f.Service.UpdateVariantAsync(2,3,1,new(){ColorLabel="Blue",SizeLabel="Medium",PriceMinor=-1,WeightG=0,LengthMm=0,WidthMm=0,HeightMm=0},f.Actor,ct));
        Assert.IsAssignableFrom<ApiException>(error);f.Products.Verify(r=>r.RollbackTransactionAsync(CancellationToken.None),Times.Once);Assert.Empty(f.Events);
    }

    [Fact]
    public async Task Duplicate_variant_identity_cannot_be_created()
    {
        var f=new Fixture();var ct=TestContext.Current.CancellationToken;
        f.Variants.Setup(r=>r.VariantIdentityExistsAsync(2,"RED","M",null,ct)).ReturnsAsync(true);
        Assert.Equal("VARIANT_ALREADY_EXISTS",(await Assert.ThrowsAsync<CatalogAdminException>(()=>f.Service.CreateVariantAsync(2,1,new(){Sku="SKU",ColorCode="RED",SizeCode="M"},f.Actor,ct))).Code);
    }

    [Fact]
    public async Task Published_product_can_retire_variant_if_another_active_variant_exists()
    {
        var f=new Fixture();var ct=TestContext.Current.CancellationToken;f.Product.Status=ProductStatus.Published;f.Variant.Status=ProductVariantStatus.Active;
        f.Variants.Setup(r=>r.HasOtherActiveVariantsAsync(2,3,ct)).ReturnsAsync(true);
        Assert.Equal("Retired",(await f.Service.UpdateVariantStatusAsync(2,3,1,ProductVariantStatus.Retired,f.Actor,ct)).Variant.Status);
    }
}
