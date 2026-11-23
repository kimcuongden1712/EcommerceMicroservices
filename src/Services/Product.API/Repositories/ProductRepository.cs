using Contracts.Domains.Interfaces;
using Infrastructure.Common;
using Infrastructure.Common.Models;
using Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Product.API.Commons.Enums;
using Product.API.Contracts.V1;
using Product.API.Entities;
using Product.API.Persistence;
using Product.API.Repositories.Interfaces;
using Product.API.Repositories.Models;

namespace Product.API.Repositories
{
    public class ProductRepository : RepositoryBase<Products, long, ProductContext>, IProductRepository
    {
        private readonly ProductContext _dbContext;
        private readonly IProductVariantsRepository _variants;
        private readonly ICategoryRepository _categories;
        private readonly IProductImageRepository _images;

        public ProductRepository (
            ProductContext dbContext,
            IUnitOfWork<ProductContext> unitOfWork,
            IProductVariantsRepository variants,
            ICategoryRepository categories,
            IProductImageRepository images) : base(dbContext, unitOfWork)
        {
            _dbContext = dbContext;
            _variants = variants;
            _categories = categories;
            _images = images;
        }

        public Task<long?> GetProductCategoryIdAsync (long productId, CancellationToken cancellationToken)
        {
            return FindByCondition(product => product.Id == productId)
                .Select(product => (long?)product.CategoryId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<Products?> LockProductAsync (long productId, CancellationToken cancellationToken)
        {
            var product = await _dbContext.Products
                .FromSqlInterpolated($"SELECT * FROM products WHERE id = {productId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (product is not null)
                await _dbContext.Entry(product).ReloadAsync(cancellationToken);
            return product;
        }

        public Task<PagedList<AdminProductListData>> GetProductsByAdminAsync (
            AdminProductListQuery query,
            CancellationToken cancellationToken)
        {
            var productQuery = FindAll();
            if (query.CategoryId.HasValue)
                productQuery = productQuery.Where(product => product.CategoryId == query.CategoryId.Value);
            if (query.Status.HasValue)
                productQuery = productQuery.Where(product => product.Status == query.Status.Value);
            if (query.SearchTerm is not null) {
                var searchTerm = query.SearchTerm.ToLowerInvariant();
                var sku = query.SearchTerm.ToUpperInvariant();
                var variantQuery = _variants.FindAll();
                productQuery = productQuery.Where(product =>
                    product.Name.ToLower().Contains(searchTerm) ||
                    variantQuery.Any(variant =>
                        variant.ProductId == product.Id && variant.Sku == sku));
            }

            var productRows = productQuery
                .OrderByDescending(product => product.Id)
                .Select(product => new AdminProductListData(
                    product.Id,
                    product.CategoryId,
                    product.Name,
                    product.Slug,
                    product.Status,
                    product.Version));

            return productRows.PaginatedListAsync(query.Page, query.PageSize, cancellationToken);
        }

        public Task<Products?> GetProductAsync (
            long id,
            bool trackChanges,
            CancellationToken cancellationToken)
        {
            return FindByCondition(product => product.Id == id, trackChanges)
                .SingleOrDefaultAsync(cancellationToken);
        }

        public Task<bool> ProductSlugExistsAsync (
            string slug,
            long? exceptId,
            CancellationToken cancellationToken)
        {
            return FindByCondition(
                product => product.Slug == slug && (!exceptId.HasValue || product.Id != exceptId.Value))
                .AnyAsync(cancellationToken);
        }

        public Task<bool> HasPublishedProductsInCategoryAsync (
            long categoryId,
            CancellationToken cancellationToken)
        {
            return FindByCondition(
                product => product.CategoryId == categoryId && product.Status == ProductStatus.Published)
                .AnyAsync(cancellationToken);
        }

        public async Task<PagedList<PublicProductListItemData>> GetProductsAsync (
        ProductListQuery query,
        CancellationToken cancellationToken)
        {
            var products = GetPublicProducts();
            if (query.CategorySlug is not null) {
                var category = await _categories.FindByCondition(item => item.Slug == query.CategorySlug &&
                        item.Status == CategoryStatus.Active &&
                        (item.ParentId == null ||
                         _categories.FindAll().Any(parent =>
                             parent.Id == item.ParentId &&
                             parent.Status == CategoryStatus.Active &&
                             parent.ParentId == null)))
                    .Select(item => new { item.Id, item.ParentId })
                    .SingleOrDefaultAsync(cancellationToken);

                if (category is null)
                    return new PagedList<PublicProductListItemData>([], 0, query.Page, query.PageSize);

                var categoryIds = category.ParentId == null
                    ? await _categories.FindByCondition(item => item.Status == CategoryStatus.Active &&
                            (item.Id == category.Id || item.ParentId == category.Id))
                        .Select(item => item.Id)
                        .ToArrayAsync(cancellationToken)
                    : [category.Id];

                products = products.Where(product => categoryIds.Contains(product.CategoryId));
            }

            var variants = GetMatchingVariants(query);
            products = products.Where(product => variants.Any(variant => variant.ProductId == product.Id));
            if (query.SearchTerm is not null) {
                var nameTerm = query.SearchTerm.ToLowerInvariant();
                var skuTerm = query.SearchTerm.ToUpperInvariant();
                products = products.Where(product =>
                    product.Name.ToLower().Contains(nameTerm) ||
                    variants.Any(variant => variant.ProductId == product.Id && variant.Sku == skuTerm));
            }

            var resultQuery = products.Select(product => new PublicProductListItemData(
                product.Id,
                product.Slug,
                product.Name,
                product.PublishedAt,
                _categories.FindAll()
                    .Where(category => category.Id == product.CategoryId)
                    .Select(category => category.Slug)
                    .First(),
                _images.FindAll()
                    .Where(image => image.ProductId == product.Id && image.Position == 0)
                    .Select(image => image.AssetKey)
                    .First(),
                _images.FindAll()
                    .Where(image => image.ProductId == product.Id && image.Position == 0)
                    .Select(image => image.AltText)
                    .First(),
                variants
                    .Where(variant => variant.ProductId == product.Id)
                    .Min(variant => variant.PriceMinor!.Value)));

            var orderedQuery = query.Sort switch
            {
                ProductSortOrder.PriceAscending => resultQuery
                    .OrderBy(item => item.PriceFromMinor)
                    .ThenBy(item => item.Id),
                ProductSortOrder.PriceDescending => resultQuery
                    .OrderByDescending(item => item.PriceFromMinor)
                    .ThenBy(item => item.Id),
                _ => resultQuery
                    .OrderByDescending(item => item.PublishedAt)
                    .ThenBy(item => item.Id)
            };

            return await orderedQuery.PaginatedListAsync(
                query.Page,
                query.PageSize,
                cancellationToken);
        }

        public async Task<PublicProductDetailData?> GetProductBySlugAsync (
            string slug,
            CancellationToken cancellationToken)
        {
            var product = await GetPublicProducts()
                .Where(item => item.Slug == slug)
                .Select(item => new
                {
                    item.Id,
                    item.Slug,
                    item.Name,
                    item.Description,
                    CategorySlug = _categories.FindAll()
                        .Where(category => category.Id == item.CategoryId)
                        .Select(category => category.Slug)
                        .First()
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (product is null)
                return null;

            var productImages = await _images.FindByCondition(image => image.ProductId == product.Id)
                .OrderBy(image => image.Position)
                .ThenBy(image => image.Id)
                .Select(image => new PublicProductImageData(image.AssetKey, image.AltText))
                .ToListAsync(cancellationToken);

            var productVariants = await _variants.FindByCondition(variant =>
                    variant.ProductId == product.Id &&
                    variant.Status == ProductVariantStatus.Active &&
                    variant.PriceMinor.HasValue)
                .OrderBy(variant => variant.Id)
                .Select(variant => new PublicProductVariantData(
                    variant.Id,
                    variant.Sku,
                    variant.ColorCode,
                    variant.ColorLabel,
                    variant.SizeCode,
                    variant.SizeLabel,
                    variant.PriceMinor!.Value))
                .ToListAsync(cancellationToken);

            return new PublicProductDetailData(
                product.Id,
                product.Slug,
                product.Name,
                product.CategorySlug,
                product.Description,
                productImages,
                productVariants);
        }

        private IQueryable<Products> GetPublicProducts ()
        {
            return FindAll()
                .Where(product =>
                    product.Status == ProductStatus.Published &&
                    _categories.FindAll().Any(category =>
                        category.Id == product.CategoryId &&
                        category.Status == CategoryStatus.Active &&
                        (category.ParentId == null ||
                         _categories.FindAll().Any(parent =>
                             parent.Id == category.ParentId &&
                             parent.Status == CategoryStatus.Active &&
                             parent.ParentId == null))) &&
                    _images.FindAll().Any(image =>
                        image.ProductId == product.Id && image.Position == 0));
        }

        private IQueryable<ProductVariants> GetMatchingVariants (ProductListQuery query)
        {
            var variantQuery = _variants.FindByCondition(variant =>
                    variant.Status == ProductVariantStatus.Active &&
                    variant.PriceMinor.HasValue);

            if (query.ColorCode is not null)
                variantQuery = variantQuery.Where(variant => variant.ColorCode == query.ColorCode);
            if (query.SizeCode is not null)
                variantQuery = variantQuery.Where(variant => variant.SizeCode == query.SizeCode);
            if (query.MinPriceMinor.HasValue)
                variantQuery = variantQuery.Where(variant => variant.PriceMinor >= query.MinPriceMinor.Value);
            if (query.MaxPriceMinor.HasValue)
                variantQuery = variantQuery.Where(variant => variant.PriceMinor <= query.MaxPriceMinor.Value);

            return variantQuery;
        }
    }
}