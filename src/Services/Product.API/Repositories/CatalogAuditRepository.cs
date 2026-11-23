using Contracts.Domains.Interfaces;
using Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using Product.API.Commons;
using Product.API.Entities;
using Product.API.Persistence;
using Product.API.Repositories.Interfaces;

namespace Product.API.Repositories
{
    public class CatalogAuditRepository : RepositoryBase<CatalogAudit, long, ProductContext>, ICatalogAuditRepository
    {
        public CatalogAuditRepository (ProductContext dbContext, IUnitOfWork<ProductContext> unitOfWork) : base(dbContext,
            unitOfWork)
        {
        }

        public Task<bool> WasCategoryActivatedAsync (
            int entityType,
            long categoryId,
            CancellationToken cancellationToken)
        {
            return FindByCondition(
                audit => audit.EntityType == entityType &&
                    audit.EntityId == categoryId &&
                    (audit.Action == Const.CATALOGAUDITACTIVATED || audit.Action == Const.CATALOGAUDITDEACTIVATED))
                .AnyAsync(cancellationToken);
        }

        public Task<bool> WasProductPublishedAsync (
            int entityType,
            long productId,
            CancellationToken cancellationToken)
        {
            return FindByCondition(
                audit => audit.EntityType == entityType &&
                    audit.EntityId == productId &&
                    audit.Action == Const.CATALOGAUDITACTIVATED)
                .AnyAsync(cancellationToken);
        }
    }
}