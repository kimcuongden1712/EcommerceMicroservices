using Contracts.Domains.Interfaces;
using Infrastructure.Common.Models;
using Product.API.Entities;
using Product.API.Extensions;
using Product.API.Persistence;

namespace Product.API.Repositories.Interfaces
{
    public interface IProductRepository : IRepositoryBase<Products, long, ProductContext>
    {
        Task<PagedList<Products>> GetProductsAsync (ProductParameters parameters, bool trackChanges);
        Task<IEnumerable<Products>> GetProductsAsync ();
        Task<Products?> GetProductAsync (long id);
        Task<Products?> GetProductByNoAsync (string productNo);
        Task CreateProductAsync (Products product);
        Task UpdateProductAsync (Products product);
        Task DeleteProductAsync (long id);
    }
}