using Contracts.Domains.Interfaces;
using Product.API.Entities;
using Product.API.Persistence;
using Product.API.Repositories.Models;

namespace Product.API.Repositories.Interfaces
{
    public interface ICategoryRepository : IRepositoryBase<Categories, long, ProductContext>
    {
        Task<IEnumerable<Categories>> GetCategoriesAsync (CancellationToken cancellationToken);

        Task<IReadOnlyList<Categories>> GetCategoriesByAdminAsync (CancellationToken cancellationToken);
        Task<Categories?> GetCategoryAsync (long id, bool trackChanges, CancellationToken cancellationToken);
        Task<IReadOnlyList<CategoryHierarchyItem>> GetCategoryHierarchyAsync (
            IEnumerable<long> categoryIds,
            CancellationToken cancellationToken);
        Task<bool> LockCategoryAsync (long id, CancellationToken cancellationToken);
        Task<bool> CategorySlugExistsAsync (string slug, long? exceptId, CancellationToken cancellationToken);
        Task<bool> HasActiveCategoryChildrenAsync (long categoryId, CancellationToken cancellationToken);
    }
}