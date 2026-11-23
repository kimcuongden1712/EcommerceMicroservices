using Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using Product.API.Commons;
using Product.API.Commons.Enums;
using Product.API.Commons.Exceptions;
using Product.API.Contracts.V1;
using Product.API.Entities;
using Product.API.Repositories.Interfaces;
using Product.API.Services.V1.Interfaces;
using System.Text.Json;

namespace Product.API.Services.V1;

public sealed class AdminCatalogService (
    ICategoryRepository categoryRepository,
    IProductRepository productRepository,
    IProductVariantsRepository productVariantsRepository,
    IProductImageRepository productImageRepository,
    ICatalogAuditRepository catalogAuditRepository,
    ICatalogOutboxRepository catalogOutboxRepository) : IAdminCatalogService
{

    #region Categories

    public async Task<IReadOnlyList<AdminCategoryResponse>> GetCategoriesAsync (CancellationToken cancellationToken)
    {
        var categories = await categoryRepository.GetCategoriesByAdminAsync(cancellationToken);

        return categories.Select(ToCategoryResponse).ToArray();
    }

    public async Task<AdminCategoryResponse> GetCategoryAsync (long id, CancellationToken cancellationToken)
    {
        var category = await categoryRepository.GetCategoryAsync(id, false, cancellationToken);

        return category is null
            ? throw CatalogAdminException.NotFound("category")
            : ToCategoryResponse(category);
    }

    public async Task<AdminCategoryResponse> CreateCategoryAsync (CreateCategoryRequest request, CatalogAuditContext auditContext, CancellationToken cancellationToken)
    {
        var name = RequiredTrimmed(request.Name);
        var slug = NormalizeSlug(request.Slug);

        cancellationToken.ThrowIfCancellationRequested();
        await categoryRepository.BeginTransactionAsync();
        try {

            if (request.ParentId is long parentId) {
                var parentHierarchy = await GetCategoryHierarchyIdsAsync([parentId], cancellationToken);
                await LockCategoriesAsync(parentHierarchy, cancellationToken);
                var parent = await FindCategoryAsync(parentId, cancellationToken);
                if (parent.ParentId is not null)
                    throw CatalogAdminException.Conflict(
                        "CATEGORY_MAX_DEPTH_EXCEEDED",
                        "A subcategory can only be created under a root category.");
            }

            await EnsureCategorySlugAvailableAsync(slug, null, cancellationToken);

            var category = new Categories
            {
                ParentId = request.ParentId,
                Name = name,
                Slug = slug,
                SortOrder = request.SortOrder.GetValueOrDefault(),
                Status = CategoryStatus.Inactive,
                Version = 1
            };
            categoryRepository.Create(category);
            cancellationToken.ThrowIfCancellationRequested();
            await categoryRepository.SaveChangesAsync();
            AddAudit(Const.CATEGORYENTITYTYPE, category.Id, "Created", auditContext,
                new
                {
                    category.ParentId,
                    category.Name,
                    category.Slug,
                    category.SortOrder,
                    status = category.Status.ToString()
                });
            AddCategoryOutbox(category, "Created", auditContext.CorrelationId);

            cancellationToken.ThrowIfCancellationRequested();
            await categoryRepository.EndTransactionAsync();
            return ToCategoryResponse(category);
        }
        catch (Exception exception) {
            await categoryRepository.RollbackTransactionAsync();
            ThrowMappedPersistenceException(exception);
            throw;
        }
    }


    public async Task<AdminCategoryResponse> UpdateCategoryAsync (
        long id,
        long expectedVersion,
        UpdateCategoryRequest request,
        CatalogAuditContext auditContext,
        CancellationToken cancellationToken)
    {
        var name = RequiredTrimmed(request.Name);
        var slug = NormalizeSlug(request.Slug);
        var sortOrder = request.SortOrder.GetValueOrDefault();
        cancellationToken.ThrowIfCancellationRequested();
        await categoryRepository.BeginTransactionAsync();
        try {
            var hierarchy = await GetCategoryHierarchyIdsAsync([id], cancellationToken);
            await LockCategoriesAsync(hierarchy, cancellationToken);
            var category = await FindCategoryAsync(id, cancellationToken);
            EnsureVersion(category.Version, expectedVersion);

            if (!string.Equals(category.Slug, slug, StringComparison.Ordinal)) {
                if (category.Status == CategoryStatus.Active ||
                    await WasCategoryActivatedAsync(id, cancellationToken))
                    throw CatalogAdminException.Conflict(
                        "CATEGORY_SLUG_IMMUTABLE",
                        "A category slug cannot change after its first activation.");

                await EnsureCategorySlugAvailableAsync(slug, id, cancellationToken);
            }

            if (category.Name == name && category.Slug == slug && category.SortOrder == sortOrder) {
                cancellationToken.ThrowIfCancellationRequested();
                await categoryRepository.EndTransactionAsync();
                return ToCategoryResponse(category);
            }

            var changes = new
            {
                name = new
                {
                    before = category.Name,
                    after = name
                },
                slug = new
                {
                    before = category.Slug,
                    after = slug
                },
                sortOrder = new
                {
                    before = category.SortOrder,
                    after = sortOrder
                }
            };
            category.Name = name;
            category.Slug = slug;
            category.SortOrder = sortOrder;
            category.Version++;

            AddAudit(Const.CATEGORYENTITYTYPE, category.Id, "Updated", auditContext, changes);
            AddCategoryOutbox(category, "Updated", auditContext.CorrelationId);
            cancellationToken.ThrowIfCancellationRequested();
            await categoryRepository.EndTransactionAsync();
            return ToCategoryResponse(category);

        }
        catch (Exception exception) {
            await categoryRepository.RollbackTransactionAsync();
            ThrowMappedPersistenceException(exception);
            throw;
        }
    }

    public async Task<AdminCategoryResponse> UpdateCategoryStatusAsync (
        long id,
        long expectedVersion,
        CategoryStatus status,
        CatalogAuditContext auditContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await categoryRepository.BeginTransactionAsync();

        try {
            var hierarchy = await GetCategoryHierarchyIdsAsync([id], cancellationToken);
            await LockCategoriesAsync(hierarchy, cancellationToken);
            var category = await FindCategoryAsync(id, cancellationToken);
            EnsureVersion(category.Version, expectedVersion);

            if (category.Status == status) {
                cancellationToken.ThrowIfCancellationRequested();
                await categoryRepository.EndTransactionAsync();
                return ToCategoryResponse(category);
            }


            if (status == CategoryStatus.Active) {
                if (category.ParentId is long parentId) {
                    var parent = await FindCategoryAsync(parentId, cancellationToken);
                    if (parent.ParentId is not null || parent.Status != CategoryStatus.Active)
                        throw CatalogAdminException.Conflict(
                            "CATEGORY_PARENT_INACTIVE",
                            "A subcategory can only be activated under an active root category.");
                }
            }
            else {
                var hasActiveChildren = await categoryRepository.HasActiveCategoryChildrenAsync(category.Id, cancellationToken);
                var hasPublishedProducts = await productRepository.HasPublishedProductsInCategoryAsync(category.Id, cancellationToken);
                if (hasActiveChildren || hasPublishedProducts)
                    throw CatalogAdminException.Conflict(
                        "CATEGORY_HAS_ACTIVE_DEPENDENTS",
                        "Deactivate active subcategories and unpublish products assigned directly to this category first.");
            }

            var previousStatus = category.Status;
            category.Status = status;
            category.Version++;
            var action = status == CategoryStatus.Active ? "Activated" : "Deactivated";

            AddAudit(
                Const.CATEGORYENTITYTYPE,
                category.Id,
                action,
                auditContext,
                new
                {
                    status = new
                    {
                        before = previousStatus.ToString(),
                        after = status.ToString()
                    }
                });
            AddCategoryOutbox(category, action, auditContext.CorrelationId);
            cancellationToken.ThrowIfCancellationRequested();
            await categoryRepository.EndTransactionAsync();
            return ToCategoryResponse(category);
        }
        catch (Exception exception) {
            await categoryRepository.RollbackTransactionAsync();
            ThrowMappedPersistenceException(exception);
            throw;
        }
    }

    #region Private

    private async Task<IReadOnlyList<long>> GetCategoryHierarchyIdsAsync (
        IEnumerable<long> categoryIds,
        CancellationToken cancellationToken)
    {
        var requestedIds = categoryIds.Distinct().ToArray();
        var categories = await categoryRepository.GetCategoryHierarchyAsync(requestedIds, cancellationToken);
        if (categories.Count != requestedIds.Length)
            throw CatalogAdminException.NotFound("category");

        return categories
            .SelectMany(category => category.ParentId.HasValue
                ? new[] { category.Id, category.ParentId.Value }
                : new[] { category.Id })
            .Distinct()
            .Order()
            .ToArray();
    }

    private async Task LockCategoriesAsync (IEnumerable<long> categoryIds, CancellationToken cancellationToken)
    {
        foreach (var categoryId in categoryIds.Distinct().Order()) {
            if (!await categoryRepository.LockCategoryAsync(categoryId, cancellationToken))
                throw CatalogAdminException.NotFound("category");
        }
    }

    private async Task<Categories> FindCategoryAsync (long id, CancellationToken cancellationToken)
    {
        return await categoryRepository.GetCategoryAsync(id, true, cancellationToken)
            ?? throw CatalogAdminException.NotFound("category");
    }

    private async Task EnsureCategoryExistsAsync (long id, CancellationToken cancellationToken)
    {
        if (await categoryRepository.GetCategoryAsync(id, false, cancellationToken) is null)
            throw CatalogAdminException.NotFound("category");
    }

    private async Task EnsureCategorySlugAvailableAsync (
        string slug,
        long? exceptId,
        CancellationToken cancellationToken)
    {
        if (await categoryRepository.CategorySlugExistsAsync(slug, exceptId, cancellationToken))
            throw CatalogAdminException.Conflict("SLUG_ALREADY_EXISTS", "The category slug is already in use.");
    }

    private Task<bool> WasCategoryActivatedAsync (long id, CancellationToken cancellationToken)
    {
        return catalogAuditRepository.WasCategoryActivatedAsync(Const.CATEGORYENTITYTYPE, id, cancellationToken);
    }

    private static AdminCategoryResponse ToCategoryResponse (Categories category)
    {
        return new AdminCategoryResponse(
            category.Id.ToString(),
            category.ParentId?.ToString(),
            category.Name,
            category.Slug,
            category.Status.ToString(),
            category.SortOrder,
            category.Version);
    }

    private void AddCategoryOutbox (Categories category, string changeType, string correlationId)
    {
        var occurredAt = DateTimeOffset.UtcNow;
        var payload = JsonSerializer.Serialize(new
        {
            eventId = Guid.NewGuid().ToString("N"),
            occurredAt,
            categoryId = category.Id,
            categoryVersion = category.Version,
            changeType,
            correlationId
        });
        catalogOutboxRepository.Create(new CatalogOutbox
        {
            Topic = "catalog.category.changed.v1",
            PayloadJson = payload,
            Attempts = 0,
            NextAttemptAt = occurredAt,
            OccurredAt = occurredAt
        });
    }

    #endregion

    #endregion

    #region Products

    public async Task<AdminProductListResponse> GetProductsAsync (
        AdminProductListQuery query,
        CancellationToken cancellationToken)
    {
        var page = await productRepository.GetProductsByAdminAsync(query, cancellationToken);
        var metadata = page.GetMetaData();
        var hasNext = metadata.CurrentPage < metadata.TotalPages;

        var items = page.Select(product => new AdminProductListItemResponse(
            product.Id.ToString(),
            product.CategoryId.ToString(),
            product.Name,
            product.Slug,
            product.Status.ToString(),
            product.Version)).ToArray();
        return new AdminProductListResponse(items, query.Page, query.PageSize, hasNext);
    }

    public async Task<AdminProductResponse> GetProductAsync (
        long id,
        CancellationToken cancellationToken)
    {
        var product = await productRepository.GetProductAsync(id, false, cancellationToken);
        if (product is null)
            throw CatalogAdminException.NotFound("product");

        return await ToProductResponseAsync(product, cancellationToken);
    }

    public async Task<AdminProductResponse> CreateProductAsync (
        CreateProductRequest request,
        CatalogAuditContext auditContext,
        CancellationToken cancellationToken)
    {
        var name = RequiredTrimmed(request.Name);
        var slug = NormalizeSlug(request.Slug);

        cancellationToken.ThrowIfCancellationRequested();
        await productRepository.BeginTransactionAsync();
        try {
            var categoryHierarchy = await GetCategoryHierarchyIdsAsync(
                [request.CategoryId!.Value],
                cancellationToken);
            await LockCategoriesAsync(categoryHierarchy, cancellationToken);
            await EnsureProductSlugAvailableAsync(slug, null, cancellationToken);

            var product = new Products
            {
                CategoryId = request.CategoryId.Value,
                Name = name,
                Slug = slug,
                Description = request.Description?.Trim(),
                Status = ProductStatus.Draft,
                Version = 1
            };
            productRepository.Create(product);
            cancellationToken.ThrowIfCancellationRequested();
            await productRepository.SaveChangesAsync();

            AddAudit(
                Const.PRODUCTENTITYTYPE,
                product.Id,
                "Created",
                auditContext,
                new
                {
                    product.CategoryId,
                    product.Name,
                    product.Slug,
                    product.Description,
                    status = product.Status.ToString()
                });
            AddProductOutbox(product, "Created", [], auditContext.CorrelationId);
            var response = await ToProductResponseAsync(product, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await productRepository.EndTransactionAsync();
            return response;
        }
        catch (Exception exception) {
            await productRepository.RollbackTransactionAsync();
            ThrowMappedPersistenceException(exception);
            throw;
        }
    }

    public async Task<AdminProductResponse> UpdateProductAsync (
        long id,
        long expectedVersion,
        UpdateProductRequest request,
        CatalogAuditContext auditContext,
        CancellationToken cancellationToken)
    {
        var name = RequiredTrimmed(request.Name);
        var slug = NormalizeSlug(request.Slug);

        cancellationToken.ThrowIfCancellationRequested();
        await productRepository.BeginTransactionAsync();
        try {
            var product = await LockProductAggregateAsync(
                id,
                expectedVersion,
                [request.CategoryId!.Value],
                cancellationToken);
            await EnsureCategoryExistsAsync(request.CategoryId.Value, cancellationToken);

            if (!string.Equals(product.Slug, slug, StringComparison.Ordinal)) {
                if (product.Status == ProductStatus.Published ||
                    product.PublishedAt.HasValue ||
                    await WasProductPublishedAsync(id, cancellationToken))
                    throw CatalogAdminException.Conflict(
                        "PRODUCT_SLUG_IMMUTABLE",
                        "A product slug cannot change after its first publication.");

                await EnsureProductSlugAvailableAsync(slug, id, cancellationToken);
            }

            var description = request.Description?.Trim();
            if (product.CategoryId == request.CategoryId.Value &&
                product.Name == name &&
                product.Slug == slug &&
                product.Description == description) {
                var unchangedResponse = await ToProductResponseAsync(product, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                await productRepository.EndTransactionAsync();
                return unchangedResponse;
            }

            var changes = new
            {
                categoryId = new
                {
                    before = product.CategoryId,
                    after = request.CategoryId.Value
                },
                name = new
                {
                    before = product.Name,
                    after = name
                },
                slug = new
                {
                    before = product.Slug,
                    after = slug
                },
                description = new
                {
                    before = product.Description,
                    after = description
                }
            };
            product.CategoryId = request.CategoryId.Value;
            product.Name = name;
            product.Slug = slug;
            product.Description = description;
            if (product.Status == ProductStatus.Published)
                await EnsureProductPublishableAsync(product, cancellationToken);
            product.Version++;

            AddAudit(Const.PRODUCTENTITYTYPE, product.Id, "Updated", auditContext, changes);
            AddProductOutbox(product, "Updated", [], auditContext.CorrelationId);
            var response = await ToProductResponseAsync(product, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await productRepository.EndTransactionAsync();
            return response;
        }
        catch (Exception exception) {
            await productRepository.RollbackTransactionAsync();
            ThrowMappedPersistenceException(exception);
            throw;
        }
    }

    public async Task<AdminProductResponse> UpdateProductStatusAsync (
        long id,
        long expectedVersion,
        ProductStatus status,
        CatalogAuditContext auditContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await productRepository.BeginTransactionAsync();
        try {
            var product = await LockProductAggregateAsync(id, expectedVersion, [], cancellationToken);
            if (product.Status == status) {
                var unchangedResponse = await ToProductResponseAsync(product, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                await productRepository.EndTransactionAsync();
                return unchangedResponse;
            }

            if (status == ProductStatus.Published) {
                await EnsureProductPublishableAsync(product, cancellationToken);
                if (product.Status != ProductStatus.Published)
                    product.PublishedAt = DateTimeOffset.UtcNow;
            }
            else if (status != ProductStatus.Unpublished ||
                product.Status != ProductStatus.Published) {
                throw CatalogAdminException.Conflict(
                    "INVALID_PRODUCT_TRANSITION",
                    "Only a published product can be unpublished.");
            }

            var previousStatus = product.Status;
            product.Status = status;
            product.Version++;
            var action = status == ProductStatus.Published ? "Published" : "Unpublished";

            AddAudit(
                Const.PRODUCTENTITYTYPE,
                product.Id,
                action,
                auditContext,
                new
                {
                    status = new
                    {
                        before = previousStatus.ToString(),
                        after = status.ToString()
                    }
                });
            AddProductOutbox(product, action, [], auditContext.CorrelationId);
            var response = await ToProductResponseAsync(product, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await productRepository.EndTransactionAsync();
            return response;
        }
        catch (Exception exception) {
            await productRepository.RollbackTransactionAsync();
            ThrowMappedPersistenceException(exception);
            throw;
        }
    }

    #region Private

    private async Task<Products> LockProductAggregateAsync (
        long productId,
        long expectedVersion,
        IEnumerable<long> additionalCategoryIds,
        CancellationToken cancellationToken)
    {
        var categoryId = await productRepository.GetProductCategoryIdAsync(productId, cancellationToken);
        if (!categoryId.HasValue)
            throw CatalogAdminException.NotFound("product");

        var categoryIds = await GetCategoryHierarchyIdsAsync(
            additionalCategoryIds.Append(categoryId.Value),
            cancellationToken);
        await LockCategoriesAsync(categoryIds, cancellationToken);

        var product = await productRepository.LockProductAsync(productId, cancellationToken);
        if (product is null)
            throw CatalogAdminException.NotFound("product");

        EnsureVersion(product.Version, expectedVersion);
        return product;
    }

    private async Task EnsureProductSlugAvailableAsync (
        string slug,
        long? exceptId,
        CancellationToken cancellationToken)
    {
        if (await productRepository.ProductSlugExistsAsync(slug, exceptId, cancellationToken))
            throw CatalogAdminException.Conflict("SLUG_ALREADY_EXISTS", "The product slug is already in use.");
    }

    private Task<bool> WasProductPublishedAsync (long id, CancellationToken cancellationToken)
    {
        return catalogAuditRepository.WasProductPublishedAsync(Const.PRODUCTENTITYTYPE, id, cancellationToken);
    }

    private async Task EnsureProductPublishableAsync (Products product, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(product.Description))
            throw CatalogAdminException.Conflict(
                "PUBLISH_REQUIREMENTS_NOT_MET",
                "A product description is required before publishing.");

        var category = await FindCategoryAsync(product.CategoryId, cancellationToken);
        if (category.Status != CategoryStatus.Active)
            throw CatalogAdminException.Conflict(
                "PUBLISH_REQUIREMENTS_NOT_MET",
                "The product category must be active before publishing.");

        if (category.ParentId is long parentId) {
            var parent = await FindCategoryAsync(parentId, cancellationToken);
            if (parent.ParentId is not null || parent.Status != CategoryStatus.Active)
                throw CatalogAdminException.Conflict(
                    "PUBLISH_REQUIREMENTS_NOT_MET",
                    "The product's parent category must be an active root category.");
        }

        var hasReadyPrimaryImage = await productImageRepository.HasReadyPrimaryImageAsync(product.Id, cancellationToken);
        if (!hasReadyPrimaryImage)
            throw CatalogAdminException.Conflict(
                "PUBLISH_REQUIREMENTS_NOT_MET",
                "The product needs a ready primary image before publishing.");

        var activeVariants = await productVariantsRepository.GetVariantsByProductAsync(
            product.Id,
            true,
            cancellationToken);
        if (activeVariants.Count == 0)
            throw CatalogAdminException.Conflict(
                "PUBLISH_REQUIREMENTS_NOT_MET",
                "The product needs at least one active variant before publishing.");

        foreach (var variant in activeVariants)
            ValidateSellableVariant(
                variant.PriceMinor,
                variant.WeightG,
                variant.LengthMm,
                variant.WidthMm,
                variant.HeightMm);
    }

    private async Task<AdminProductResponse> ToProductResponseAsync (
        Products product,
        CancellationToken cancellationToken)
    {
        var storedVariants = await productVariantsRepository.GetVariantsByProductAsync(
            product.Id,
            false,
            cancellationToken);
        var images = await productImageRepository.GetProductImagesAsync(product.Id, cancellationToken);

        return new AdminProductResponse(
            product.Id.ToString(),
            product.CategoryId.ToString(),
            product.Name,
            product.Slug,
            product.Description,
            product.Status.ToString(),
            product.PublishedAt,
            product.Version,
            storedVariants.Select(ToVariantResponse).ToArray(),
            images);
    }

    private void AddProductOutbox (
        Products product,
        string changeType,
        IReadOnlyList<string> affectedSkus,
        string correlationId)
    {
        var occurredAt = DateTimeOffset.UtcNow;
        var payload = JsonSerializer.Serialize(new
        {
            eventId = Guid.NewGuid().ToString("N"),
            occurredAt,
            productId = product.Id,
            productVersion = product.Version,
            changeType,
            affectedSkus,
            correlationId
        });
        catalogOutboxRepository.Create(new CatalogOutbox
        {
            Topic = "catalog.product.changed.v1",
            PayloadJson = payload,
            Attempts = 0,
            NextAttemptAt = occurredAt,
            OccurredAt = occurredAt
        });
    }

    #endregion

    #endregion

    #region Product Variants

    public async Task<AdminProductVariantDetailResponse> CreateVariantAsync (
        long productId,
        long expectedProductVersion,
        CreateProductVariantRequest request,
        CatalogAuditContext auditContext,
        CancellationToken cancellationToken)
    {
        var sku = NormalizeCode(request.Sku);
        var colorCode = NormalizeCode(request.ColorCode);
        var sizeCode = NormalizeCode(request.SizeCode);
        var colorLabel = RequiredTrimmed(request.ColorLabel);
        var sizeLabel = RequiredTrimmed(request.SizeLabel);

        cancellationToken.ThrowIfCancellationRequested();
        await productRepository.BeginTransactionAsync();
        try {
            var product = await LockProductAggregateAsync(productId, expectedProductVersion, [], cancellationToken);
            await EnsureVariantIdentityAvailableAsync(
                productId,
                sku,
                colorCode,
                sizeCode,
                null,
                cancellationToken);

            var variant = new ProductVariants
            {
                ProductId = productId,
                Sku = sku,
                ColorCode = colorCode,
                ColorLabel = colorLabel,
                SizeCode = sizeCode,
                SizeLabel = sizeLabel,
                PriceMinor = request.PriceMinor,
                WeightG = request.WeightG,
                LengthMm = request.LengthMm,
                WidthMm = request.WidthMm,
                HeightMm = request.HeightMm,
                Status = ProductVariantStatus.Draft
            };
            productVariantsRepository.Create(variant);
            product.Version++;
            cancellationToken.ThrowIfCancellationRequested();
            await productRepository.SaveChangesAsync();

            var audit = new
            {
                variant.ProductId,
                variant.Sku,
                variant.ColorCode,
                variant.SizeCode,
                status = variant.Status.ToString()
            };
            AddAudit(Const.PRODUCTVARIANTENTITYTYPE, variant.Id, "Created", auditContext, audit);
            AddProductOutbox(product, "VariantCreated", [variant.Sku], auditContext.CorrelationId);
            var response = new AdminProductVariantDetailResponse(ToVariantResponse(variant), product.Version);
            cancellationToken.ThrowIfCancellationRequested();
            await productRepository.EndTransactionAsync();
            return response;
        }
        catch (Exception exception) {
            await productRepository.RollbackTransactionAsync();
            ThrowMappedPersistenceException(exception);
            throw;
        }
    }

    public async Task<AdminProductVariantDetailResponse> GetVariantAsync (
        long productId,
        long variantId,
        CancellationToken cancellationToken)
    {
        var productForVersion = await productRepository.GetProductAsync(productId, false, cancellationToken);
        if (productForVersion is null)
            throw CatalogAdminException.NotFound("product");

        var variant = await productVariantsRepository.GetProductVariantAsync(
            productId,
            variantId,
            false,
            cancellationToken);
        if (variant is null)
            throw CatalogAdminException.NotFound("product variant");

        return new AdminProductVariantDetailResponse(ToVariantResponse(variant), productForVersion.Version);
    }

    public async Task<AdminProductVariantDetailResponse> UpdateVariantAsync (
        long productId,
        long variantId,
        long expectedProductVersion,
        UpdateProductVariantRequest request,
        CatalogAuditContext auditContext,
        CancellationToken cancellationToken)
    {
        var colorLabel = RequiredTrimmed(request.ColorLabel);
        var sizeLabel = RequiredTrimmed(request.SizeLabel);

        cancellationToken.ThrowIfCancellationRequested();
        await productRepository.BeginTransactionAsync();
        try {
            var product = await LockProductAggregateAsync(productId, expectedProductVersion, [], cancellationToken);
            var variant = await FindVariantAsync(productId, variantId, cancellationToken);
            if (variant.Status == ProductVariantStatus.Retired)
                throw CatalogAdminException.Conflict("VARIANT_RETIRED", "A retired variant cannot be updated.");

            if (variant.ColorLabel == colorLabel &&
                variant.SizeLabel == sizeLabel &&
                variant.PriceMinor == request.PriceMinor &&
                variant.WeightG == request.WeightG &&
                variant.LengthMm == request.LengthMm &&
                variant.WidthMm == request.WidthMm &&
                variant.HeightMm == request.HeightMm) {
                var unchangedResponse = new AdminProductVariantDetailResponse(
                    ToVariantResponse(variant),
                    product.Version);
                cancellationToken.ThrowIfCancellationRequested();
                await productRepository.EndTransactionAsync();
                return unchangedResponse;
            }

            if (variant.Status == ProductVariantStatus.Active)
                ValidateSellableVariant(
                    request.PriceMinor,
                    request.WeightG,
                    request.LengthMm,
                    request.WidthMm,
                    request.HeightMm);

            var changes = new
            {
                colorLabel = new
                {
                    before = variant.ColorLabel,
                    after = colorLabel
                },
                sizeLabel = new
                {
                    before = variant.SizeLabel,
                    after = sizeLabel
                },
                priceMinor = new
                {
                    before = variant.PriceMinor,
                    after = request.PriceMinor
                },
                weightG = new
                {
                    before = variant.WeightG,
                    after = request.WeightG
                },
                lengthMm = new
                {
                    before = variant.LengthMm,
                    after = request.LengthMm
                },
                widthMm = new
                {
                    before = variant.WidthMm,
                    after = request.WidthMm
                },
                heightMm = new
                {
                    before = variant.HeightMm,
                    after = request.HeightMm
                }
            };
            variant.ColorLabel = colorLabel;
            variant.SizeLabel = sizeLabel;
            variant.PriceMinor = request.PriceMinor;
            variant.WeightG = request.WeightG;
            variant.LengthMm = request.LengthMm;
            variant.WidthMm = request.WidthMm;
            variant.HeightMm = request.HeightMm;
            product.Version++;

            AddAudit(Const.PRODUCTVARIANTENTITYTYPE, variant.Id, "Updated", auditContext, changes);
            AddProductOutbox(product, "VariantUpdated", [variant.Sku], auditContext.CorrelationId);
            var response = new AdminProductVariantDetailResponse(ToVariantResponse(variant), product.Version);
            cancellationToken.ThrowIfCancellationRequested();
            await productRepository.EndTransactionAsync();
            return response;
        }
        catch (Exception exception) {
            await productRepository.RollbackTransactionAsync();
            ThrowMappedPersistenceException(exception);
            throw;
        }
    }

    public async Task<AdminProductVariantDetailResponse> UpdateVariantStatusAsync (
        long productId,
        long variantId,
        long expectedProductVersion,
        ProductVariantStatus status,
        CatalogAuditContext auditContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await productRepository.BeginTransactionAsync();
        try {
            var product = await LockProductAggregateAsync(productId, expectedProductVersion, [], cancellationToken);
            var variant = await FindVariantAsync(productId, variantId, cancellationToken);

            if (variant.Status == status) {
                var unchangedResponse = new AdminProductVariantDetailResponse(
                    ToVariantResponse(variant),
                    product.Version);
                cancellationToken.ThrowIfCancellationRequested();
                await productRepository.EndTransactionAsync();
                return unchangedResponse;
            }
            if (status == ProductVariantStatus.Active) {
                if (variant.Status != ProductVariantStatus.Draft)
                    throw CatalogAdminException.Conflict(
                        "INVALID_VARIANT_TRANSITION",
                        "Only a draft variant can be activated.");

                ValidateSellableVariant(
                    variant.PriceMinor,
                    variant.WeightG,
                    variant.LengthMm,
                    variant.WidthMm,
                    variant.HeightMm);
            }
            else if (status == ProductVariantStatus.Retired) {
                if (variant.Status != ProductVariantStatus.Active)
                    throw CatalogAdminException.Conflict(
                        "INVALID_VARIANT_TRANSITION",
                        "Only an active variant can be retired.");

                if (product.Status == ProductStatus.Published) {
                    var hasAnotherActiveVariant = await productVariantsRepository.HasOtherActiveVariantsAsync(
                        productId,
                        variantId,
                        cancellationToken);
                    if (!hasAnotherActiveVariant)
                        throw CatalogAdminException.Conflict(
                            "PUBLISHED_PRODUCT_REQUIRES_ACTIVE_VARIANT",
                            "A published product must retain at least one active variant.");
                }
            }
            else {
                throw CatalogAdminException.Conflict(
                    "INVALID_VARIANT_TRANSITION",
                    "Variants can only be activated or retired through this operation.");
            }

            var previousStatus = variant.Status;
            variant.Status = status;
            product.Version++;
            var action = status == ProductVariantStatus.Active ? "Activated" : "Retired";

            AddAudit(
                Const.PRODUCTVARIANTENTITYTYPE,
                variant.Id,
                action,
                auditContext,
                new
                {
                    status = new
                    {
                        before = previousStatus.ToString(),
                        after = status.ToString()
                    }
                });
            AddProductOutbox(product, $"Variant{action}", [variant.Sku], auditContext.CorrelationId);
            var response = new AdminProductVariantDetailResponse(ToVariantResponse(variant), product.Version);
            cancellationToken.ThrowIfCancellationRequested();
            await productRepository.EndTransactionAsync();
            return response;
        }
        catch (Exception exception) {
            await productRepository.RollbackTransactionAsync();
            ThrowMappedPersistenceException(exception);
            throw;
        }
    }

    #region Private

    private async Task<ProductVariants> FindVariantAsync (
        long productId,
        long variantId,
        CancellationToken cancellationToken)
    {
        return await productVariantsRepository.GetProductVariantAsync(
                productId,
                variantId,
                true,
                cancellationToken)
            ?? throw CatalogAdminException.NotFound("product variant");
    }

    private async Task EnsureVariantIdentityAvailableAsync (
        long productId,
        string sku,
        string colorCode,
        string sizeCode,
        long? exceptId,
        CancellationToken cancellationToken)
    {
        if (await productVariantsRepository.VariantSkuExistsAsync(sku, exceptId, cancellationToken))
            throw CatalogAdminException.Conflict("SKU_ALREADY_EXISTS", "The SKU is already in use.");

        if (await productVariantsRepository.VariantIdentityExistsAsync(
                productId,
                colorCode,
                sizeCode,
                exceptId,
                cancellationToken))
            throw CatalogAdminException.Conflict(
                "VARIANT_ALREADY_EXISTS",
                "A variant with this product, color, and size already exists.");
    }

    private static void ValidateSellableVariant (
        int? priceMinor,
        int? weightG,
        int? lengthMm,
        int? widthMm,
        int? heightMm)
    {
        var errors = new Dictionary<string, List<string>>();
        if (priceMinor is < 1 or > Const.MAXIMUMPRICEMINOR)
            AddError(errors, "priceMinor", $"priceMinor must be between 1 and {Const.MAXIMUMPRICEMINOR}.");
        if (weightG is null or <= 0)
            AddError(errors, "weightG", "weightG must be positive.");
        if (lengthMm is null or <= 0)
            AddError(errors, "lengthMm", "lengthMm must be positive.");
        if (widthMm is null or <= 0)
            AddError(errors, "widthMm", "widthMm must be positive.");
        if (heightMm is null or <= 0)
            AddError(errors, "heightMm", "heightMm must be positive.");
        ThrowIfInvalid(errors);
    }

    private static AdminProductVariantResponse ToVariantResponse (ProductVariants variant)
    {
        return new AdminProductVariantResponse(
            variant.Id.ToString(),
            variant.Sku,
            variant.ColorCode,
            variant.ColorLabel,
            variant.SizeCode,
            variant.SizeLabel,
            variant.PriceMinor,
            variant.Status.ToString(),
            variant.WeightG,
            variant.LengthMm,
            variant.WidthMm,
            variant.HeightMm);
    }

    #endregion

    #endregion

    #region Shared

    #region Private

    private static MySqlException? FindDuplicateKeyException (Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException) {
            if (current is MySqlException { Number: 1062 } mySqlException)
                return mySqlException;
        }

        return null;
    }

    private static void ThrowMappedPersistenceException (Exception exception)
    {
        if (exception is DbUpdateConcurrencyException)
            throw CatalogAdminException.PreconditionFailed();

        if (exception is not DbUpdateException dbUpdateException ||
            FindDuplicateKeyException(dbUpdateException) is not { } duplicateKey)
            return;

        var message = duplicateKey.Message;
        var code = message.Contains("sku", StringComparison.OrdinalIgnoreCase)
            ? "SKU_ALREADY_EXISTS"
            : message.Contains("slug", StringComparison.OrdinalIgnoreCase)
                ? "SLUG_ALREADY_EXISTS"
                : "CATALOG_CONFLICT";
        var detail = code switch
        {
            "SKU_ALREADY_EXISTS" => "The SKU is already in use.",
            "SLUG_ALREADY_EXISTS" => "The slug is already in use.",
            _ => "A unique catalog value is already in use."
        };

        throw CatalogAdminException.Conflict(code, detail);
    }

    private void AddAudit (
        int entityType,
        long entityId,
        string action,
        CatalogAuditContext auditContext,
        object changes)
    {
        catalogAuditRepository.Create(new CatalogAudit
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            ActorId = auditContext.ActorId,
            CorrelationId = auditContext.CorrelationId,
            ChangedFieldsJson = JsonSerializer.Serialize(changes),
            OccurredAt = DateTimeOffset.UtcNow
        });
    }

    private static void EnsureVersion (long actualVersion, long expectedVersion)
    {
        if (actualVersion != expectedVersion)
            throw CatalogAdminException.PreconditionFailed();
    }

    private static string RequiredTrimmed (string? value) => value?.Trim() ?? string.Empty;

    private static string NormalizeSlug (string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static string NormalizeCode (string? value) => value?.Trim().ToUpperInvariant() ?? string.Empty;

    private static void AddError (Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var messages)) {
            messages = [];
            errors.Add(field, messages);
        }

        messages.Add(message);
    }

    private static void ThrowIfInvalid (Dictionary<string, List<string>> errors)
    {
        if (errors.Count > 0)
            throw new BadRequestException(
                "One or more catalog fields are invalid.",
                "VALIDATION_FAILED",
                errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()),
                "Catalog validation failed");
    }

    #endregion

    #endregion
}
