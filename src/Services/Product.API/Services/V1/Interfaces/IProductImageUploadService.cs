using Product.API.Contracts.V1;

namespace Product.API.Services.V1.Interfaces;

public interface IProductImageUploadService
{
    Task<ProductImageUploadResponse> CreateUploadSessionAsync(
        long productId,
        CreateProductImageUploadRequest request,
        CancellationToken cancellationToken);

    Task<ProductImageUploadStatusResponse?> GetUploadSessionAsync(
        long productId,
        long uploadId,
        CancellationToken cancellationToken);
}
