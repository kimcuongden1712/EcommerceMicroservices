using Infrastructure.Common.Models;
using Moq;
using Product.API.Contracts.V1;
using Product.API.Entities;
using Product.API.Repositories.Interfaces;
using Product.API.Repositories.Models;
using Product.API.Services.V1;

namespace Product.API.Tests;

public sealed class PublicCatalogServiceTests
{
    [Fact]
    public async Task Category_tree_attaches_children_and_preserves_root_order()
    {
        var categories=new Mock<ICategoryRepository>();
        categories.Setup(r=>r.GetCategoriesAsync(CancellationToken.None)).ReturnsAsync([
            new Categories{Id=1,Name="Root",Slug="root"},new Categories{Id=2,Name="Child",Slug="child",ParentId=1},
            new Categories{Id=3,Name="Other",Slug="other"}]);
        var result=await new PublicCatalogService(categories.Object,Mock.Of<IProductRepository>()).GetCategoriesAsync(CancellationToken.None);
        Assert.Equal(new[]{"1","3"},result.Select(x=>x.Id));Assert.Equal("2",Assert.Single(result[0].Children).Id);Assert.Empty(result[1].Children);
    }

    [Theory]
    [InlineData(1,true)] [InlineData(2,false)]
    public async Task Product_list_maps_prices_urls_and_next_page(int page,bool hasNext)
    {
        var products=new Mock<IProductRepository>();var query=new ProductListQuery(null,null,null,null,null,null,ProductSortOrder.Newest,page,1);
        products.Setup(r=>r.GetProductsAsync(query,CancellationToken.None)).ReturnsAsync(new PagedList<PublicProductListItemData>([
            new(1,"shoe","Shoe",DateTimeOffset.UtcNow,"shoes","folder/a b","Alt",100)],2,page,1));
        var result=await new PublicCatalogService(Mock.Of<ICategoryRepository>(),products.Object).GetProductsAsync(query,CancellationToken.None);
        Assert.Equal(hasNext,result.HasNext);var item=Assert.Single(result.Items);
        Assert.Equal("/api/v1/media/folder/a%20b",item.Thumbnail.Url);Assert.Equal(100,item.PriceFromMinor);Assert.Equal("USD",item.Currency);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Product_detail_maps_images_and_variants_or_returns_null(bool exists)
    {
        var products=new Mock<IProductRepository>();
        products.Setup(r=>r.GetProductBySlugAsync("shoe",CancellationToken.None)).ReturnsAsync(exists ? new PublicProductDetailData(
            1,"shoe","Shoe","shoes","Description",[new("key","Alt")],[new(2,"SKU","RED","Red","M","Medium",100)]) : null);
        var result=await new PublicCatalogService(Mock.Of<ICategoryRepository>(),products.Object).GetProductBySlugAsync("shoe",CancellationToken.None);
        if(!exists){Assert.Null(result);return;}
        Assert.NotNull(result);Assert.Equal("1",result.Id);Assert.Equal("/api/v1/media/key",Assert.Single(result.Images).Url);
        Assert.Equal("SKU",Assert.Single(result.Variants).Sku);
    }
}

