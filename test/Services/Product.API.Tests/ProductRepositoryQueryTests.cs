using Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using Product.API.Commons.Enums;
using Product.API.Contracts.V1;
using Product.API.Entities;
using Product.API.Extensions;
using Product.API.Persistence;
using Product.API.Repositories;
using Serilog;

namespace Product.API.Tests;

// These tests check query semantics in memory. MySQL locking and SQL translation require integration tests.
public sealed class ProductRepositoryQueryTests
{
    private sealed class Fixture : IDisposable
    {
        public readonly ProductContext Context=new(new DbContextOptionsBuilder<ProductContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public readonly CategoryRepository Categories;
        public readonly ProductVariantsRepository Variants;
        public readonly ProductImageRepository Images;
        public readonly ProductImageUploadRepository Uploads;
        public readonly ProductRepository Products;
        public readonly CatalogAuditRepository Audit;
        public Fixture()
        {
            var unit=new UnitOfWork<ProductContext>(Context);
            Categories=new(Context,unit);Variants=new(Context,unit);
            Uploads=new(Context,unit,new RepositoryBase<ProductImages,long,ProductContext>(Context,unit));
            Images=new(Context,unit,Uploads,new RepositoryBase<Products,long,ProductContext>(Context,unit),Categories);
            Products=new(Context,unit,Variants,Categories,Images);Audit=new(Context,unit);
        }
        public async Task Seed()
        {
            using var logger=new LoggerConfiguration().CreateLogger();
            await ProductContextSeed.SeedProductAsync(Context,logger);
        }
        public void Dispose()=>Context.Dispose();
    }

    [Fact]
    public async Task Seed_is_repeatable_without_duplicate_rows_and_creates_catalog_relations()
    {
        using var f=new Fixture();await f.Seed();
        var before=new[]{await f.Context.Categories.CountAsync(TestContext.Current.CancellationToken),await f.Context.Products.CountAsync(TestContext.Current.CancellationToken),await f.Context.ProductVariants.CountAsync(TestContext.Current.CancellationToken),await f.Context.ProductImages.CountAsync(TestContext.Current.CancellationToken),await f.Context.ProductImageUploads.CountAsync(TestContext.Current.CancellationToken),await f.Context.CatalogAudit.CountAsync(TestContext.Current.CancellationToken),await f.Context.CatalogOutbox.CountAsync(TestContext.Current.CancellationToken)};
        Assert.All(before,count=>Assert.True(count>=30));await f.Seed();
        var after=new[]{await f.Context.Categories.CountAsync(TestContext.Current.CancellationToken),await f.Context.Products.CountAsync(TestContext.Current.CancellationToken),await f.Context.ProductVariants.CountAsync(TestContext.Current.CancellationToken),await f.Context.ProductImages.CountAsync(TestContext.Current.CancellationToken),await f.Context.ProductImageUploads.CountAsync(TestContext.Current.CancellationToken),await f.Context.CatalogAudit.CountAsync(TestContext.Current.CancellationToken),await f.Context.CatalogOutbox.CountAsync(TestContext.Current.CancellationToken)};
        Assert.Equal(before,after);
        Assert.All(await f.Context.CatalogOutbox.ToListAsync(TestContext.Current.CancellationToken),row=>Assert.Contains("productId",row.PayloadJson));
    }

    [Fact]
    public async Task Category_queries_filter_active_hierarchy_and_track_only_when_requested()
    {
        using var f=new Fixture();await f.Seed();
        var all=await f.Categories.GetCategoriesByAdminAsync(TestContext.Current.CancellationToken);Assert.Equal(30,all.Count);
        Assert.Equal(30,(await f.Categories.GetCategoriesAsync(TestContext.Current.CancellationToken)).Count());
        var root=all.First(c=>c.ParentId==null);root.Status=CategoryStatus.Inactive;f.Context.ChangeTracker.Clear();f.Context.Update(root);await f.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var visible=(await f.Categories.GetCategoriesAsync(TestContext.Current.CancellationToken)).ToArray();
        Assert.DoesNotContain(visible,c=>c.Id==root.Id || c.ParentId==root.Id);
        Assert.True(await f.Categories.CategorySlugExistsAsync(root.Slug,null,TestContext.Current.CancellationToken));
        Assert.False(await f.Categories.CategorySlugExistsAsync(root.Slug,root.Id,TestContext.Current.CancellationToken));
        Assert.True(await f.Categories.HasActiveCategoryChildrenAsync(root.Id,TestContext.Current.CancellationToken));
        Assert.False(await f.Categories.HasActiveCategoryChildrenAsync(-1,TestContext.Current.CancellationToken));
        Assert.Single(await f.Categories.GetCategoryHierarchyAsync([root.Id,root.Id],TestContext.Current.CancellationToken));
        f.Context.ChangeTracker.Clear();
        var detached=await f.Categories.GetCategoryAsync(root.Id,false,TestContext.Current.CancellationToken);Assert.Equal(EntityState.Detached,f.Context.Entry(detached!).State);
        var tracked=await f.Categories.GetCategoryAsync(root.Id,true,TestContext.Current.CancellationToken);Assert.Equal(EntityState.Unchanged,f.Context.Entry(tracked!).State);
        Assert.Null(await f.Categories.GetCategoryAsync(-1,false,TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Variant_queries_scope_by_product_identity_status_and_exclusion()
    {
        using var f=new Fixture();await f.Seed();var row=await f.Context.ProductVariants.FirstAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(await f.Variants.GetVariantsByProductAsync(row.ProductId,false,TestContext.Current.CancellationToken));
        Assert.All(await f.Variants.GetVariantsByProductAsync(row.ProductId,true,TestContext.Current.CancellationToken),v=>Assert.Equal(ProductVariantStatus.Active,v.Status));
        Assert.NotNull(await f.Variants.GetProductVariantAsync(row.ProductId,row.Id,false,TestContext.Current.CancellationToken));
        Assert.Null(await f.Variants.GetProductVariantAsync(-1,row.Id,false,TestContext.Current.CancellationToken));
        Assert.True(await f.Variants.VariantSkuExistsAsync(row.Sku,null,TestContext.Current.CancellationToken));Assert.False(await f.Variants.VariantSkuExistsAsync(row.Sku,row.Id,TestContext.Current.CancellationToken));
        Assert.True(await f.Variants.VariantIdentityExistsAsync(row.ProductId,row.ColorCode,row.SizeCode,null,TestContext.Current.CancellationToken));
        Assert.False(await f.Variants.VariantIdentityExistsAsync(row.ProductId,row.ColorCode,row.SizeCode,row.Id,TestContext.Current.CancellationToken));
        Assert.False(await f.Variants.HasOtherActiveVariantsAsync(-1,row.Id,TestContext.Current.CancellationToken));
        Assert.True(await f.Variants.HasOtherActiveVariantsAsync(row.ProductId,-1,TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(ProductSortOrder.Newest)] [InlineData(ProductSortOrder.PriceAscending)] [InlineData(ProductSortOrder.PriceDescending)]
    public async Task Public_product_queries_respect_visibility_filters_and_pagination(ProductSortOrder sort)
    {
        using var f=new Fixture();await f.Seed();
        var query=new ProductListQuery(null,null,null,null,null,null,sort,1,100);
        var all=await f.Products.GetProductsAsync(query,TestContext.Current.CancellationToken);Assert.NotEmpty(all);
        var item=all[0];var detail=await f.Products.GetProductBySlugAsync(item.Slug,TestContext.Current.CancellationToken);Assert.NotNull(detail);Assert.NotEmpty(detail.Images);Assert.NotEmpty(detail.Variants);
        Assert.Null(await f.Products.GetProductBySlugAsync("missing",TestContext.Current.CancellationToken));
        Assert.Empty(await f.Products.GetProductsAsync(query with{CategorySlug="missing"},TestContext.Current.CancellationToken));
        Assert.NotEmpty(await f.Products.GetProductsAsync(query with{CategorySlug=item.CategorySlug,SearchTerm=item.Name},TestContext.Current.CancellationToken));
        var variant=detail.Variants[0];
        Assert.NotEmpty(await f.Products.GetProductsAsync(query with{SearchTerm=variant.Sku,ColorCode=variant.ColorCode,SizeCode=variant.SizeCode,MinPriceMinor=variant.PriceMinor,MaxPriceMinor=variant.PriceMinor},TestContext.Current.CancellationToken));
        Assert.Empty(await f.Products.GetProductsAsync(query with{ColorCode="MISSING"},TestContext.Current.CancellationToken));
        Assert.True(await f.Products.ProductSlugExistsAsync(item.Slug,null,TestContext.Current.CancellationToken));Assert.False(await f.Products.ProductSlugExistsAsync(item.Slug,item.Id,TestContext.Current.CancellationToken));
        Assert.NotNull(await f.Products.GetProductCategoryIdAsync(item.Id,TestContext.Current.CancellationToken));Assert.Null(await f.Products.GetProductCategoryIdAsync(-1,TestContext.Current.CancellationToken));
        Assert.NotNull(await f.Products.GetProductAsync(item.Id,true,TestContext.Current.CancellationToken));
        Assert.True(await f.Products.HasPublishedProductsInCategoryAsync((await f.Products.GetProductAsync(item.Id,false,TestContext.Current.CancellationToken))!.CategoryId,TestContext.Current.CancellationToken));
        Assert.False(await f.Products.HasPublishedProductsInCategoryAsync(-1,TestContext.Current.CancellationToken));
        var admin=await f.Products.GetProductsByAdminAsync(new(null,null,null,1,100),TestContext.Current.CancellationToken);Assert.Equal(30,admin.Count);
        Assert.NotEmpty(await f.Products.GetProductsByAdminAsync(new(variant.Sku,null,ProductStatus.Published,1,100),TestContext.Current.CancellationToken));
        Assert.Empty(await f.Products.GetProductsByAdminAsync(new(null,-1,null,1,100),TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Image_queries_return_position_order_and_require_ready_public_catalog_reference()
    {
        using var f=new Fixture();await f.Seed();var image=await f.Context.ProductImages.FirstAsync(TestContext.Current.CancellationToken);
        var rows=await f.Images.GetProductImagesAsync(image.ProductId,TestContext.Current.CancellationToken);Assert.NotEmpty(rows);Assert.Equal(rows.OrderBy(r=>r.Position),rows);
        Assert.Single(await f.Images.GetProductImagesByAssetKeyAsync(image.AssetKey,TestContext.Current.CancellationToken));
        Assert.True(await f.Images.HasAssetReferenceAsync(image.AssetKey,TestContext.Current.CancellationToken));Assert.False(await f.Images.HasAssetReferenceAsync("missing",TestContext.Current.CancellationToken));
        Assert.True(await f.Images.HasReadyPrimaryImageAsync(image.ProductId,TestContext.Current.CancellationToken));Assert.False(await f.Images.HasReadyPrimaryImageAsync(-1,TestContext.Current.CancellationToken));
        Assert.NotNull(await f.Images.GetPublicProductImageAsync(image.AssetKey,TestContext.Current.CancellationToken));Assert.Null(await f.Images.GetPublicProductImageAsync("missing",TestContext.Current.CancellationToken));
        var product=await f.Context.Products.SingleAsync(p=>p.Id==image.ProductId,TestContext.Current.CancellationToken);product.Status=ProductStatus.Unpublished;await f.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Null(await f.Images.GetPublicProductImageAsync(image.AssetKey,TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Legacy_search_sort_and_price_parameters_preserve_filter_semantics()
    {
        var items=new[]{new Products{Id=1,Name="Beta",Slug="beta"},new Products{Id=2,Name="Alpha",Slug="alpha"}}.AsQueryable();
        Assert.Equal(2,items.SearchTerm(" ").Count());Assert.Single(items.SearchTerm(" ALP "));
        foreach(var sort in new string?[]{null," ","missing"})Assert.Equal("Alpha",items.SortBy(sort).First().Name);
        Assert.Equal("Beta",items.SortBy("Name desc").First().Name);
        var parameters=new ProductParameters{SearchTerm="term",MinPrice=1,MaxPrice=2};Assert.True(parameters.ValidPriceRange);Assert.Equal("Name",parameters.OrderBy);
        parameters.MaxPrice=0;Assert.False(parameters.ValidPriceRange);Assert.Equal("term",parameters.SearchTerm);
    }

    [Theory]
    [InlineData("ready",true)] [InlineData("wrong_product",false)] [InlineData("wrong_key",false)]
    [InlineData("pending",false)] [InlineData("not_primary",false)]
    public async Task Ready_primary_image_requires_matching_product_key_position_and_upload_status(string scenario,bool expected)
    {
        using var f=new Fixture();
        f.Context.ProductImages.Add(new(){ProductId=1,AssetKey="asset",AltText="Alt",Position=scenario=="not_primary"?1:0});
        f.Context.ProductImageUploads.Add(new(){ProductId=scenario=="wrong_product"?2:1,AssetKey=scenario=="wrong_key"?"other":"asset",StagingKey="stage",ExpectedContentType="image/png",Status=scenario=="pending"?ProductImageUploadStatus.Pending:ProductImageUploadStatus.Ready});
        await f.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expected,await f.Images.HasReadyPrimaryImageAsync(1,TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Audit_history_queries_exclude_other_entities_and_unrelated_actions()
    {
        using var f=new Fixture();var ct=TestContext.Current.CancellationToken;
        f.Context.CatalogAudit.AddRange(
            new CatalogAudit{EntityType=1,EntityId=1,Action="Activated",ActorId="actor",CorrelationId="correlation",ChangedFieldsJson="{}"},
            new CatalogAudit{EntityType=1,EntityId=2,Action="Deactivated",ActorId="actor",CorrelationId="correlation",ChangedFieldsJson="{}"},
            new CatalogAudit{EntityType=1,EntityId=3,Action="Created",ActorId="actor",CorrelationId="correlation",ChangedFieldsJson="{}"});
        await f.Context.SaveChangesAsync(ct);
        Assert.True(await f.Audit.WasCategoryActivatedAsync(1,1,ct));Assert.True(await f.Audit.WasCategoryActivatedAsync(1,2,ct));
        Assert.False(await f.Audit.WasCategoryActivatedAsync(1,3,ct));Assert.False(await f.Audit.WasCategoryActivatedAsync(2,1,ct));
        Assert.False(await f.Audit.WasProductPublishedAsync(2,1,ct));
    }
}
