using Product.API.Commons.Enums;
using Product.API.Contracts.V1;

namespace Product.API.Services.V1.Interfaces;

public interface IAdminCatalogService
{
    Task<IReadOnlyList<AdminCategoryResponse>> GetCategoriesAsync (CancellationToken cancellationToken);
    Task<AdminCategoryResponse> GetCategoryAsync (long id, CancellationToken cancellationToken);
    Task<AdminCategoryResponse> CreateCategoryAsync (
        CreateCategoryRequest request,
        CatalogAuditContext auditContext,
        CancellationToken cancellationToken);
    Task<AdminCategoryResponse> UpdateCategoryAsync (
        long id,
        long expectedVersion,
        UpdateCategoryRequest request,
        CatalogAuditContext auditContext,
        CancellationToken cancellationToken);
    Task<AdminCategoryResponse> UpdateCategoryStatusAsync (
        long id,
        long expectedVersion,
        CategoryStatus status,
        CatalogAuditContext auditContext,
        CancellationToken cancellationToken);

    Task<AdminProductListResponse> GetProductsAsync (
        AdminProductListQuery query,
        CancellationToken cancellationToken);
    Task<AdminProductResponse> GetProductAsync (long id, CancellationToken cancellationToken);
    Task<AdminProductResponse> CreateProductAsync (
        CreateProductRequest request,
        CatalogAuditContext auditContext,
        CancellationToken cancellationToken);
    Task<AdminProductResponse> UpdateProductAsync (
        long id,
        long expectedVersion,
        UpdateProductRequest request,
        CatalogAuditContext auditContext,
        CancellationToken cancellationToken);
    Task<AdminProductResponse> UpdateProductStatusAsync (
        long id,
        long expectedVersion,
        ProductStatus status,
        CatalogAuditContext auditContext,
        CancellationToken cancellationToken);

    Task<AdminProductVariantDetailResponse> CreateVariantAsync (
        long productId,
        long expectedProductVersion,
        CreateProductVariantRequest request,
        CatalogAuditContext auditContext,
        CancellationToken cancellationToken);
    Task<AdminProductVariantDetailResponse> GetVariantAsync (
        long productId,
        long variantId,
        CancellationToken cancellationToken);
    Task<AdminProductVariantDetailResponse> UpdateVariantAsync (
        long productId,
        long variantId,
        long expectedProductVersion,
        UpdateProductVariantRequest request,
        CatalogAuditContext auditContext,
        CancellationToken cancellationToken);
    Task<AdminProductVariantDetailResponse> UpdateVariantStatusAsync (
        long productId,
        long variantId,
        long expectedProductVersion,
        ProductVariantStatus status,
        CatalogAuditContext auditContext,
        CancellationToken cancellationToken);
}

public sealed record CatalogAuditContext (string ActorId, string CorrelationId);
