using Contracts.Domains.Interfaces;
using Product.API.Entities;
using Product.API.Persistence;

namespace Product.API.Repositories.Interfaces
{
    public interface ICatalogAuditRepository : IRepositoryBase<CatalogAudit, long, ProductContext>
    {
        Task<bool> WasCategoryActivatedAsync (
            int entityType,
            long categoryId,
            CancellationToken cancellationToken);
        Task<bool> WasProductPublishedAsync (
            int entityType,
            long productId,
            CancellationToken cancellationToken);
    }
}