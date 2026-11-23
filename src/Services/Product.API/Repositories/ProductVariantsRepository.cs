using Contracts.Domains.Interfaces;
using Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using Product.API.Commons.Enums;
using Product.API.Entities;
using Product.API.Persistence;
using Product.API.Repositories.Interfaces;

namespace Product.API.Repositories
{
    public class ProductVariantsRepository : RepositoryBase<ProductVariants, long, ProductContext>, IProductVariantsRepository
    {
        public ProductVariantsRepository (ProductContext dbContext, IUnitOfWork<ProductContext> unitOfWork) : base(dbContext,
            unitOfWork)
        {
        }

        public Task<ProductVariants?> GetProductVariantAsync (
            long productId,
            long variantId,
            bool trackChanges,
            CancellationToken cancellationToken)
        {
            return FindByCondition(
                variant => variant.Id == variantId && variant.ProductId == productId,
                trackChanges).SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<ProductVariants>> GetVariantsByProductAsync (
            long productId,
            bool activeOnly,
            CancellationToken cancellationToken)
        {
            var variants = FindByCondition(variant => variant.ProductId == productId);
            if (activeOnly)
                variants = variants.Where(variant => variant.Status == ProductVariantStatus.Active);

            return await variants.OrderBy(variant => variant.Id).ToListAsync(cancellationToken);
        }

        public Task<bool> VariantSkuExistsAsync (
            string sku,
            long? exceptId,
            CancellationToken cancellationToken)
        {
            return FindByCondition(
                variant => variant.Sku == sku && (!exceptId.HasValue || variant.Id != exceptId.Value))
                .AnyAsync(cancellationToken);
        }

        public Task<bool> VariantIdentityExistsAsync (
            long productId,
            string colorCode,
            string sizeCode,
            long? exceptId,
            CancellationToken cancellationToken)
        {
            return FindByCondition(
                variant => variant.ProductId == productId &&
                    variant.ColorCode == colorCode &&
                    variant.SizeCode == sizeCode &&
                    (!exceptId.HasValue || variant.Id != exceptId.Value))
                .AnyAsync(cancellationToken);
        }

        public Task<bool> HasOtherActiveVariantsAsync (
            long productId,
            long exceptVariantId,
            CancellationToken cancellationToken)
        {
            return FindByCondition(
                variant => variant.ProductId == productId &&
                    variant.Id != exceptVariantId &&
                    variant.Status == ProductVariantStatus.Active)
                .AnyAsync(cancellationToken);
        }
    }
}