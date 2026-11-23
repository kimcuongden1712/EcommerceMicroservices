using Contracts.Domains.Interfaces;
using Product.API.Entities;
using Product.API.Persistence;

namespace Product.API.Repositories.Interfaces
{
    public interface IProductImageUploadRepository : IRepositoryBase<ProductImageUploads, long, ProductContext>
    {
    }
}