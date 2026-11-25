using Product.API.Contracts.V1;

namespace Product.API.Services.V1.Interfaces;

public interface IProductImageGalleryService
{
    Task<ProductImageGalleryResult> ReplaceGalleryAsync(
        long productId,
        long expectedVersion,
        ReplaceProductImagesRequest request,
        CatalogAuditContext auditContext,
        CancellationToken cancellationToken);
}
