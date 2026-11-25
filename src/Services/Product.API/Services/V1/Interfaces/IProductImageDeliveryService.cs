using Product.API.Repositories.Models;

namespace Product.API.Services.V1.Interfaces;

public interface IProductImageDeliveryService
{
    Task<ProductImageDeliveryData?> GetPublicImageMetadataAsync(string assetKey, CancellationToken cancellationToken);
    Task<Stream> OpenPublicImageStreamAsync(string assetKey, long expectedLength, CancellationToken cancellationToken);
}
