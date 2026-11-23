using Contracts.Domains.Interfaces;
using Product.API.Entities;
using Product.API.Persistence;

namespace Product.API.Repositories.Interfaces
{
    public interface IProductVariantsRepository : IRepositoryBase<ProductVariants, long, ProductContext>
    {
        Task<ProductVariants?> GetProductVariantAsync (
            long productId,
            long variantId,
            bool trackChanges,
            CancellationToken cancellationToken);
        Task<IReadOnlyList<ProductVariants>> GetVariantsByProductAsync (
            long productId,
            bool activeOnly,
            CancellationToken cancellationToken);
        Task<bool> VariantSkuExistsAsync (string sku, long? exceptId, CancellationToken cancellationToken);
        Task<bool> VariantIdentityExistsAsync (
            long productId,
            string colorCode,
            string sizeCode,
            long? exceptId,
            CancellationToken cancellationToken);
        Task<bool> HasOtherActiveVariantsAsync (
            long productId,
            long exceptVariantId,
            CancellationToken cancellationToken);
    }
}