using Contracts.Domains.Interfaces;
using Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using Product.API.Commons.Enums;
using Product.API.Contracts.V1;
using Product.API.Entities;
using Product.API.Persistence;
using Product.API.Repositories.Interfaces;

namespace Product.API.Repositories
{
    public class ProductImageRepository : RepositoryBase<ProductImages, long, ProductContext>, IProductImageRepository
    {
        private readonly IProductImageUploadRepository _uploads;

        public ProductImageRepository (
            ProductContext dbContext,
            IUnitOfWork<ProductContext> unitOfWork,
            IProductImageUploadRepository uploads) : base(dbContext,
            unitOfWork)
        {
            _uploads = uploads;
        }

        public async Task<IReadOnlyList<AdminProductImageResponse>> GetProductImagesAsync (
            long productId,
            CancellationToken cancellationToken)
        {
            return await FindByCondition(image => image.ProductId == productId)
                .OrderBy(image => image.Position)
                .ThenBy(image => image.Id)
                .Select(image => new AdminProductImageResponse(
                    image.Id.ToString(),
                    image.AssetKey,
                    image.AltText,
                    image.Position))
                .ToListAsync(cancellationToken);
        }

        public Task<bool> HasReadyPrimaryImageAsync (long productId, CancellationToken cancellationToken)
        {
            return FindByCondition(
                image => image.ProductId == productId &&
                    image.Position == 0 &&
                    _uploads.FindByCondition(upload =>
                        upload.ProductId == productId &&
                        upload.AssetKey == image.AssetKey &&
                        upload.Status == ProductImageUploadStatus.Ready).Any())
                .AnyAsync(cancellationToken);
        }
    }
}