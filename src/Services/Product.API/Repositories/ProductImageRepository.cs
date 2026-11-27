using Contracts.Domains.Interfaces;
using Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using Product.API.Commons.Enums;
using Product.API.Contracts.V1;
using Product.API.Entities;
using Product.API.Persistence;
using Product.API.Repositories.Interfaces;
using Product.API.Repositories.Models;

namespace Product.API.Repositories
{
    public class ProductImageRepository : RepositoryBase<ProductImages, long, ProductContext>, IProductImageRepository
    {
        private readonly IRepositoryBase<Products, long, ProductContext> _products;
        private readonly ICategoryRepository _categories;
        private readonly IProductImageUploadRepository _uploads;

        public ProductImageRepository (
            ProductContext dbContext,
            IUnitOfWork<ProductContext> unitOfWork,
            IProductImageUploadRepository uploads,
            IRepositoryBase<Products, long, ProductContext> products,
            ICategoryRepository categories) : base(dbContext,
            unitOfWork)
        {
            _products = products;
            _categories = categories;
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

        public async Task<IReadOnlyList<AdminProductImageResponse>> GetProductImagesByAssetKeyAsync (
            string assetKey,
            CancellationToken cancellationToken)
        {
            return await FindByCondition(image => image.AssetKey == assetKey)
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
            var readyUploads = _uploads.FindByCondition(upload =>
                upload.ProductId == productId && upload.Status == ProductImageUploadStatus.Ready);

            return FindByCondition(
                image => image.ProductId == productId &&
                    image.Position == 0 &&
                    readyUploads.Any(upload => upload.AssetKey == image.AssetKey))
                .AnyAsync(cancellationToken);
        }

        public Task<bool> HasAssetReferenceAsync (string assetKey, CancellationToken cancellationToken) =>
            FindByCondition(image => image.AssetKey == assetKey).AnyAsync(cancellationToken);

        public Task<ProductImageDeliveryData?> GetPublicProductImageAsync (
            string assetKey,
            CancellationToken cancellationToken)
        {
            var query =
                from image in FindByCondition(image => image.AssetKey == assetKey)
                join product in _products.FindAll() on image.ProductId equals product.Id
                join category in _categories.FindAll() on product.CategoryId equals category.Id
                join upload in _uploads.FindAll()
                    on new
                    {
                        image.ProductId,
                        image.AssetKey
                    } equals new
                    {
                        upload.ProductId,
                        AssetKey = upload.AssetKey!
                    }
                where product.Status == ProductStatus.Published &&
                      category.Status == CategoryStatus.Active &&
                      (category.ParentId == null || _categories.FindAll().Any(parent =>
                          parent.Id == category.ParentId &&
                          parent.Status == CategoryStatus.Active &&
                          parent.ParentId == null)) &&
                      upload.Status == ProductImageUploadStatus.Ready
                select new ProductImageDeliveryData(image.AssetKey, upload.ExpectedContentType, upload.ExpectedSize);

            return query.FirstOrDefaultAsync(cancellationToken);
        }
    }
}
