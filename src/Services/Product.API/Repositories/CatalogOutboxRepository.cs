using Contracts.Domains.Interfaces;
using Infrastructure.Common;
using Product.API.Entities;
using Product.API.Persistence;
using Product.API.Repositories.Interfaces;

namespace Product.API.Repositories
{
    public class CatalogOutboxRepository : RepositoryBase<CatalogOutbox, long, ProductContext>, ICatalogOutboxRepository
    {
        public CatalogOutboxRepository (ProductContext dbContext, IUnitOfWork<ProductContext> unitOfWork) : base(dbContext,
            unitOfWork)
        {
        }
    }
}