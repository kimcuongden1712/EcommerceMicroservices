using Contracts.Domains.Interfaces;
using Product.API.Contracts.V1;
using Product.API.Entities;
using Product.API.Persistence;

namespace Product.API.Repositories.Interfaces
{
    public interface IProductImageRepository : IRepositoryBase<ProductImages, long, ProductContext>
    {
        Task<IReadOnlyList<AdminProductImageResponse>> GetProductImagesAsync (
            long productId,
            CancellationToken cancellationToken);
        Task<bool> HasReadyPrimaryImageAsync (long productId, CancellationToken cancellationToken);
    }
}