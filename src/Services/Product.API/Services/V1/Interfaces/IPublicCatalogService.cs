using Product.API.Contracts.V1;

namespace Product.API.Services.V1.Interfaces;

public interface IPublicCatalogService
{
    Task<IReadOnlyList<CategoryResponse>> GetCategoriesAsync(CancellationToken cancellationToken);

    Task<ProductListResponse> GetProductsAsync(
        ProductListQuery query,
        CancellationToken cancellationToken);

    Task<ProductDetailResponse?> GetProductBySlugAsync(
        string slug,
        CancellationToken cancellationToken);
}
