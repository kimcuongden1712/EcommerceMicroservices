using Product.API.Commons.Enums;
using Product.API.Entities;

namespace Product.API.Services.V1.Interfaces;

public interface IProductImageUploadProcessingService
{
    Task ProcessUploadAsync (
        ProductImageUploads upload,
        CancellationToken cancellationToken);

    Task ProcessExpiredUploadsAsync (
        CancellationToken cancellationToken);

    Task ProcessCleanupAsync(CancellationToken cancellationToken);
}
