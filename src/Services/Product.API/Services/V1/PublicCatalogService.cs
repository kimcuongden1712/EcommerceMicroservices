using Product.API.Commons;
using Product.API.Contracts.V1;
using Product.API.Repositories.Interfaces;
using Product.API.Services.V1.Interfaces;

namespace Product.API.Services.V1;

public sealed class PublicCatalogService : IPublicCatalogService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IProductRepository _productRepository;

    public PublicCatalogService (
        ICategoryRepository categoryRepository,
        IProductRepository productRepository)
    {
        _categoryRepository = categoryRepository;
        _productRepository = productRepository;
    }

    public async Task<IReadOnlyList<CategoryResponse>> GetCategoriesAsync (
        CancellationToken cancellationToken)
    {
        var categories = await _categoryRepository.GetCategoriesAsync(cancellationToken);

        var childrenByParentId = categories
            .Where(category => category.ParentId.HasValue)
            .GroupBy(category => category.ParentId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<CategoryResponse>)group
                    .Select(category => new CategoryResponse(
                        category.Id.ToString(),
                        category.Name,
                        category.Slug,
                        category.SortOrder,
                        Array.Empty<CategoryResponse>()))
                    .ToArray());

        return categories
            .Where(category => category.ParentId == null)
            .Select(category => new CategoryResponse(
                category.Id.ToString(),
                category.Name,
                category.Slug,
                category.SortOrder,
                childrenByParentId.GetValueOrDefault(category.Id, Array.Empty<CategoryResponse>())))
            .ToArray();
    }

    public async Task<ProductListResponse> GetProductsAsync (
        ProductListQuery query,
        CancellationToken cancellationToken)
    {
        var page = await _productRepository.GetProductsAsync(query, cancellationToken);
        var metadata = page.GetMetaData();
        var hasNext = metadata.CurrentPage < metadata.TotalPages;
        var items = page.Select(item => new ProductListItemResponse(
            item.Id.ToString(),
            item.Slug,
            item.Name,
            item.CategorySlug,
            new ProductThumbnailResponse(GetPublicImageUrl(item.AssetKey), item.AltText),
            item.PriceFromMinor,
            Const.CURRENCY)).ToArray();

        return new ProductListResponse(items, query.Page, query.PageSize, hasNext);
    }

    public async Task<ProductDetailResponse?> GetProductBySlugAsync (
        string slug,
        CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetProductBySlugAsync(slug, cancellationToken);
        if (product is null)
            return null;

        return new ProductDetailResponse(
            product.Id.ToString(),
            product.Slug,
            product.Name,
            product.CategorySlug,
            product.Description,
            product.Images.Select(image => new ProductImageResponse(
                GetPublicImageUrl(image.AssetKey),
                image.AltText)).ToArray(),
            product.Variants.Select(variant => new ProductVariantResponse(
                variant.Id.ToString(),
                variant.Sku,
                variant.ColorCode,
                variant.ColorLabel,
                variant.SizeCode,
                variant.SizeLabel,
                variant.PriceMinor,
                Const.CURRENCY)).ToArray());
    }

    private string GetPublicImageUrl (string assetKey)
    {
        var escapedAssetKey = string.Join('/', assetKey.Split('/').Select(Uri.EscapeDataString));

        return $"/api/v1/media/{escapedAssetKey}";
    }
}
