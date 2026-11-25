using Contracts.Domains.Interfaces;
using Product.API.Contracts.V1;
using Product.API.Entities;
using Product.API.Persistence;
using Product.API.Repositories.Models;

namespace Product.API.Repositories.Interfaces
{
    public interface IProductImageRepository : IRepositoryBase<ProductImages, long, ProductContext>
    {
        Task<IReadOnlyList<AdminProductImageResponse>> GetProductImagesAsync (
            long productId,
            CancellationToken cancellationToken);
        Task<IReadOnlyList<AdminProductImageResponse>> GetProductImagesByAssetKeyAsync (
            string assetKey,
            CancellationToken cancellationToken);
        Task<bool> HasReadyPrimaryImageAsync (long productId, CancellationToken cancellationToken);
        Task<bool> HasAssetReferenceAsync (string assetKey, CancellationToken cancellationToken);
        Task<ProductImageDeliveryData?> GetPublicProductImageAsync (string assetKey, CancellationToken cancellationToken);
    }
}
