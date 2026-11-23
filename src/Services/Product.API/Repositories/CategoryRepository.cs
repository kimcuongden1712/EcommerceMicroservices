using Contracts.Domains.Interfaces;
using Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using Product.API.Commons.Enums;
using Product.API.Entities;
using Product.API.Persistence;
using Product.API.Repositories.Interfaces;
using Product.API.Repositories.Models;

namespace Product.API.Repositories
{
    public class CategoryRepository : RepositoryBase<Categories, long, ProductContext>, ICategoryRepository
    {
        private readonly ProductContext _dbContext;

        public CategoryRepository (ProductContext dbContext, IUnitOfWork<ProductContext> unitOfWork) : base(dbContext,
            unitOfWork)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<Categories>> GetCategoriesAsync (CancellationToken cancellationToken)
        {
            var rootCategories = FindByCondition(category =>
                category.Status == CategoryStatus.Active &&
                category.ParentId == null);

            var categories = await (
                from category in FindAll()
                join parent in rootCategories
                    on category.ParentId equals parent.Id into matchingParents
                from parent in matchingParents.DefaultIfEmpty()
                where category.Status == CategoryStatus.Active &&
                      (category.ParentId == null || parent != null)
                orderby category.SortOrder, category.Id
                select category
            ).ToListAsync(cancellationToken);

            return categories;
        }

        public async Task<IReadOnlyList<Categories>> GetCategoriesByAdminAsync (CancellationToken cancellationToken)
        {
            return await FindAll()
                .OrderBy(category => category.ParentId)
                .ThenBy(category => category.SortOrder)
                .ThenBy(category => category.Id)
                .ToListAsync(cancellationToken);
        }

        public Task<Categories?> GetCategoryAsync (
            long id,
            bool trackChanges,
            CancellationToken cancellationToken)
        {
            return FindByCondition(category => category.Id == id, trackChanges)
                .SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<CategoryHierarchyItem>> GetCategoryHierarchyAsync (
            IEnumerable<long> categoryIds,
            CancellationToken cancellationToken)
        {
            var requestedIds = categoryIds.Distinct().ToArray();
            return await FindByCondition(category => requestedIds.Contains(category.Id))
                .Select(category => new CategoryHierarchyItem(category.Id, category.ParentId))
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> LockCategoryAsync (long id, CancellationToken cancellationToken)
        {
            var category = await _dbContext.Categories
                .FromSqlInterpolated($"SELECT * FROM categories WHERE id = {id} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (category is null)
                return false;

            await _dbContext.Entry(category).ReloadAsync(cancellationToken);
            return true;
        }

        public Task<bool> CategorySlugExistsAsync (
            string slug,
            long? exceptId,
            CancellationToken cancellationToken)
        {
            return FindByCondition(
                category => category.Slug == slug && (!exceptId.HasValue || category.Id != exceptId.Value))
                .AnyAsync(cancellationToken);
        }

        public Task<bool> HasActiveCategoryChildrenAsync (long categoryId, CancellationToken cancellationToken)
        {
            return FindByCondition(
                child => child.ParentId == categoryId && child.Status == CategoryStatus.Active)
                .AnyAsync(cancellationToken);
        }
    }
}