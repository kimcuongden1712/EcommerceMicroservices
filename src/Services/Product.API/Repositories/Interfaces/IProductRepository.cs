using Contracts.Domains.Interfaces;
using Infrastructure.Common.Models;
using Product.API.Contracts.V1;
using Product.API.Entities;
using Product.API.Persistence;
using Product.API.Repositories.Models;

namespace Product.API.Repositories.Interfaces
{
    public interface IProductRepository : IRepositoryBase<Products, long, ProductContext>
    {
        Task<long?> GetProductCategoryIdAsync (long productId, CancellationToken cancellationToken);
        Task<Products?> LockProductAsync (long productId, CancellationToken cancellationToken);
        Task<PagedList<AdminProductListData>> GetProductsByAdminAsync (
            AdminProductListQuery query,
            CancellationToken cancellationToken);
        Task<Products?> GetProductAsync (long id, bool trackChanges, CancellationToken cancellationToken);
        Task<bool> ProductSlugExistsAsync (string slug, long? exceptId, CancellationToken cancellationToken);
        Task<bool> HasPublishedProductsInCategoryAsync (long categoryId, CancellationToken cancellationToken);
        Task<PublicProductDetailData?> GetProductBySlugAsync (
            string slug,
            CancellationToken cancellationToken);
        Task<PagedList<PublicProductListItemData>> GetProductsAsync (
        ProductListQuery query,
        CancellationToken cancellationToken);
    }

}