using Microsoft.EntityFrameworkCore;
using Product.API.Commons.Enums;
using Product.API.Entities;
using ILogger = Serilog.ILogger;

namespace Product.API.Persistence;

public class ProductContextSeed
{
    private const int SampleCount = 30;
    private const int ProductEntityType = 2;
    private const string SamplePrefix = "sample-";

    private static readonly CategorySample[] Categories =
    [
        new("Women", "women", null),
        new("Men", "men", null),
        new("Kids", "kids", null),
        new("Activewear", "activewear", null),
        new("Footwear", "footwear", null),
        new("Accessories", "accessories", null),
        new("T-Shirts", "women-t-shirts", "women"),
        new("Dresses", "women-dresses", "women"),
        new("Jeans", "women-jeans", "women"),
        new("Sweaters", "women-sweaters", "women"),
        new("Jackets", "women-jackets", "women"),
        new("Shirts", "men-shirts", "men"),
        new("T-Shirts", "men-t-shirts", "men"),
        new("Pants", "men-pants", "men"),
        new("Sweatshirts", "men-sweatshirts", "men"),
        new("Outerwear", "men-outerwear", "men"),
        new("Tops", "kids-tops", "kids"),
        new("Bottoms", "kids-bottoms", "kids"),
        new("Baby", "kids-baby", "kids"),
        new("Swimwear", "kids-swimwear", "kids"),
        new("Training Tops", "activewear-tops", "activewear"),
        new("Leggings", "activewear-leggings", "activewear"),
        new("Shorts", "activewear-shorts", "activewear"),
        new("Sports Bras", "activewear-sports-bras", "activewear"),
        new("Sneakers", "footwear-sneakers", "footwear"),
        new("Boots", "footwear-boots", "footwear"),
        new("Sandals", "footwear-sandals", "footwear"),
        new("Hats", "accessories-hats", "accessories"),
        new("Bags", "accessories-bags", "accessories"),
        new("Belts", "accessories-belts", "accessories")
    ];

    private static readonly string[] ProductNames =
    [
        "Classic Cotton Tee", "Oxford Button-Down Shirt", "Slim Chino Pants", "Everyday Crew Sweater",
        "Lightweight Denim Jacket", "Relaxed Midi Dress", "High-Rise Straight Jeans", "Soft Knit Cardigan",
        "Performance Training Tee", "Core Active Leggings", "Running Shorts", "Support Sports Bra",
        "Canvas Low-Top Sneakers", "Leather Ankle Boots", "Everyday Slide Sandals", "Classic Baseball Cap",
        "Everyday Canvas Tote", "Reversible Leather Belt", "Kids Graphic Tee", "Kids Jogger Pants",
        "Baby Cotton Onesie", "Kids Swim Shorts", "Men's Poplin Shirt", "Men's Fleece Hoodie",
        "Women's Linen Blouse", "Women's Utility Jacket", "Men's Athletic Shorts", "Women's Running Top",
        "Unisex Weekend Backpack", "Kids Lightweight Sneakers"
    ];

    private static readonly string[] Colors = ["BLK", "NVY", "WHT", "OLV", "RED", "GRY"];
    private static readonly string[] ColorLabels = ["Black", "Navy", "White", "Olive", "Red", "Gray"];
    private static readonly string[] Sizes = ["XS", "S", "M", "L", "XL", "XXL"];

    public static async Task SeedProductAsync (ProductContext productContext, ILogger logger)
    {
        var added = new Dictionary<string, int>();

        var categoryBySlug = await SeedCategoriesAsync(productContext, added);
        var productBySlug = await SeedProductsAsync(productContext, categoryBySlug, added);
        var variantBySku = await SeedVariantsAsync(productContext, productBySlug, added);
        await SeedImagesAsync(productContext, productBySlug, added);
        await SeedImageUploadsAsync(productContext, productBySlug, added);
        await SeedAuditAsync(productContext, productBySlug, added);
        await SeedOutboxAsync(productContext, productBySlug, variantBySku, added);

        logger.Information(
            "Product API sample seed complete. Added {CategoryCount} categories, {ProductCount} products, {VariantCount} variants, {ImageCount} images, {UploadCount} uploads, {AuditCount} audit records, and {OutboxCount} outbox records.",
            added.GetValueOrDefault("categories"),
            added.GetValueOrDefault("products"),
            added.GetValueOrDefault("variants"),
            added.GetValueOrDefault("images"),
            added.GetValueOrDefault("uploads"),
            added.GetValueOrDefault("audit"),
            added.GetValueOrDefault("outbox"));
    }

    private static async Task<Dictionary<string, Categories>> SeedCategoriesAsync (
        ProductContext context,
        Dictionary<string, int> added)
    {
        var storedSlugs = Categories.Select(category => SamplePrefix + category.Slug).ToArray();
        var storedCategories = await context.Categories
            .Where(x => storedSlugs.Contains(x.Slug))
            .ToListAsync();
        var categoryBySlug = storedCategories.ToDictionary(x => x.Slug[SamplePrefix.Length..]);

        foreach (var category in Categories.Where(x => x.ParentSlug is null)) {
            if (categoryBySlug.ContainsKey(category.Slug))
                continue;

            var entity = new Categories
            {
                Name = category.Name,
                Slug = SamplePrefix + category.Slug,
                Status = CategoryStatus.Active,
                SortOrder = 0,
                Version = 1
            };
            context.Categories.Add(entity);
            categoryBySlug.Add(category.Slug, entity);
            Increment(added, "categories");
        }

        await context.SaveChangesAsync();

        foreach (var category in Categories.Where(x => x.ParentSlug is not null)) {
            if (categoryBySlug.ContainsKey(category.Slug))
                continue;

            var entity = new Categories
            {
                ParentId = categoryBySlug[category.ParentSlug!].Id,
                Name = category.Name,
                Slug = SamplePrefix + category.Slug,
                Status = CategoryStatus.Active,
                SortOrder = 1,
                Version = 1
            };
            context.Categories.Add(entity);
            categoryBySlug.Add(category.Slug, entity);
            Increment(added, "categories");
        }

        await context.SaveChangesAsync();
        return categoryBySlug;
    }

    private static async Task<Dictionary<string, Products>> SeedProductsAsync (
        ProductContext context,
        IReadOnlyDictionary<string, Categories> categoryBySlug,
        Dictionary<string, int> added)
    {
        var productSlugs = Enumerable.Range(1, SampleCount)
            .Select(CreateProductSlug)
            .ToArray();
        var productBySlug = await context.Products
            .Where(x => productSlugs.Contains(x.Slug))
            .ToDictionaryAsync(x => x.Slug);
        var publishedAt = DateTimeOffset.UtcNow.AddDays(-7);

        for (var index = 0; index < SampleCount; index++) {
            var slug = productSlugs[index];
            if (productBySlug.ContainsKey(slug))
                continue;

            var entity = new Products
            {
                CategoryId = categoryBySlug[Categories[index].Slug].Id,
                Name = ProductNames[index],
                Slug = slug,
                Description = $"Sample product: {ProductNames[index]}. Designed for everyday comfort and style.",
                Status = ProductStatus.Published,
                PublishedAt = publishedAt,
                Version = 1
            };
            context.Products.Add(entity);
            productBySlug.Add(slug, entity);
            Increment(added, "products");
        }

        await context.SaveChangesAsync();
        return productBySlug;
    }

    private static async Task<Dictionary<string, ProductVariants>> SeedVariantsAsync (
        ProductContext context,
        IReadOnlyDictionary<string, Products> productBySlug,
        Dictionary<string, int> added)
    {
        var products = GetSeedProducts(productBySlug);
        var skus = Enumerable.Range(1, SampleCount).Select(CreateSku).ToArray();
        var existingSkus = await context.ProductVariants
            .Where(x => skus.Contains(x.Sku))
            .Select(x => x.Sku)
            .ToListAsync();
        var existingSkuSet = existingSkus.ToHashSet(StringComparer.Ordinal);
        for (var index = 0; index < SampleCount; index++) {
            var product = products[index];
            if (existingSkuSet.Contains(skus[index]))
                continue;

            var colorIndex = index % Colors.Length;
            var sizeIndex = index % Sizes.Length;
            var entity = new ProductVariants
            {
                ProductId = product.Id,
                Sku = skus[index],
                ColorCode = Colors[colorIndex],
                ColorLabel = ColorLabels[colorIndex],
                SizeCode = Sizes[sizeIndex],
                SizeLabel = Sizes[sizeIndex],
                PriceMinor = 2499 + index * 250,
                Status = ProductVariantStatus.Active,
                WeightG = 180 + index * 10,
                LengthMm = 300 + index * 5,
                WidthMm = 220 + index * 5,
                HeightMm = 25 + index
            };
            context.ProductVariants.Add(entity);
            Increment(added, "variants");
        }

        await context.SaveChangesAsync();

        return await context.ProductVariants
            .Where(x => skus.Contains(x.Sku))
            .ToDictionaryAsync(x => x.Sku);
    }

    private static async Task SeedImagesAsync (
        ProductContext context,
        IReadOnlyDictionary<string, Products> productBySlug,
        Dictionary<string, int> added)
    {
        var products = GetSeedProducts(productBySlug);
        var assetKeys = Enumerable.Range(1, SampleCount).Select(CreateAssetKey).ToArray();
        var existingAssets = await context.ProductImages
            .Where(x => assetKeys.Contains(x.AssetKey))
            .Select(x => x.AssetKey)
            .ToListAsync();
        var existingAssetSet = existingAssets.ToHashSet(StringComparer.Ordinal);

        for (var index = 0; index < SampleCount; index++) {
            if (existingAssetSet.Contains(assetKeys[index]))
                continue;

            context.ProductImages.Add(new ProductImages
            {
                ProductId = products[index].Id,
                AssetKey = assetKeys[index],
                AltText = $"{ProductNames[index]} product image",
                Position = 0
            });
            Increment(added, "images");
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedImageUploadsAsync (
        ProductContext context,
        IReadOnlyDictionary<string, Products> productBySlug,
        Dictionary<string, int> added)
    {
        var products = GetSeedProducts(productBySlug);
        var stagingKeys = Enumerable.Range(1, SampleCount).Select(CreateStagingKey).ToArray();
        var existingStagingKeys = await context.ProductImageUploads
            .Where(x => stagingKeys.Contains(x.StagingKey))
            .Select(x => x.StagingKey)
            .ToListAsync();
        var existingStagingKeySet = existingStagingKeys.ToHashSet(StringComparer.Ordinal);

        for (var index = 0; index < SampleCount; index++) {
            if (existingStagingKeySet.Contains(stagingKeys[index]))
                continue;

            context.ProductImageUploads.Add(new ProductImageUploads
            {
                ProductId = products[index].Id,
                StagingKey = stagingKeys[index],
                AssetKey = CreateAssetKey(index + 1),
                ExpectedContentType = "image/webp",
                ExpectedSize = 128000 + index * 1024,
                Status = ProductImageUploadStatus.Ready,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
            });
            Increment(added, "uploads");
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedAuditAsync (
        ProductContext context,
        IReadOnlyDictionary<string, Products> productBySlug,
        Dictionary<string, int> added)
    {
        var products = GetSeedProducts(productBySlug);
        var correlationIds = Enumerable.Range(1, SampleCount).Select(CreateCorrelationId).ToArray();
        var existingCorrelationIds = await context.CatalogAudit
            .Where(x => correlationIds.Contains(x.CorrelationId))
            .Select(x => x.CorrelationId)
            .ToListAsync();
        var existingCorrelationIdSet = existingCorrelationIds.ToHashSet(StringComparer.Ordinal);
        var occurredAt = DateTimeOffset.UtcNow;

        for (var index = 0; index < SampleCount; index++) {
            if (existingCorrelationIdSet.Contains(correlationIds[index]))
                continue;

            context.CatalogAudit.Add(new CatalogAudit
            {
                EntityType = ProductEntityType,
                EntityId = products[index].Id,
                Action = "SampleSeeded",
                ActorId = "product-api-sample-seed",
                CorrelationId = correlationIds[index],
                ChangedFieldsJson = "{\"status\":\"Published\",\"source\":\"sample-seed\"}",
                OccurredAt = occurredAt
            });
            Increment(added, "audit");
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedOutboxAsync (
        ProductContext context,
        IReadOnlyDictionary<string, Products> productBySlug,
        IReadOnlyDictionary<string, ProductVariants> variantBySku,
        Dictionary<string, int> added)
    {
        var products = GetSeedProducts(productBySlug);
        var existingPayloads = await context.CatalogOutbox
            .Where(x => x.Topic == "catalog.product.changed.v1")
            .Select(x => x.PayloadJson)
            .ToListAsync();
        var occurredAt = DateTimeOffset.UtcNow;

        for (var index = 0; index < SampleCount; index++) {
            var seedKey = $"product-api:outbox:{index + 1:000}";
            if (existingPayloads.Any(payload => payload.Contains(seedKey, StringComparison.Ordinal)))
                continue;

            var product = products[index];
            var variant = variantBySku[CreateSku(index + 1)];
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                eventId = seedKey,
                seedKey,
                occurredAt,
                productId = product.Id,
                productVersion = product.Version,
                changeType = "SampleSeeded",
                affectedSkus = new[] { variant.Sku },
                correlationId = CreateCorrelationId(index + 1)
            });

            context.CatalogOutbox.Add(new CatalogOutbox
            {
                Topic = "catalog.product.changed.v1",
                PayloadJson = payload,
                Attempts = 1,
                NextAttemptAt = occurredAt,
                PublishedAt = occurredAt,
                OccurredAt = occurredAt
            });
            Increment(added, "outbox");
        }

        await context.SaveChangesAsync();
    }

    private static Products[] GetSeedProducts (IReadOnlyDictionary<string, Products> productBySlug) =>
        Enumerable.Range(1, SampleCount)
            .Select(index => productBySlug[CreateProductSlug(index)])
            .ToArray();

    private static string CreateProductSlug (int index) => $"{SamplePrefix}apparel-{index:000}";
    private static string CreateSku (int index) => $"SAMPLE-APP-{index:000}-{Colors[(index - 1) % Colors.Length]}-{Sizes[(index - 1) % Sizes.Length]}";
    private static string CreateAssetKey (int index) => $"sample/catalog/product-{index:000}.webp";
    private static string CreateStagingKey (int index) => $"sample/staging/upload-{index:000}";
    private static string CreateCorrelationId (int index) => $"product-api-sample-{index:000}";

    private static void Increment (Dictionary<string, int> counts, string key) =>
        counts[key] = counts.GetValueOrDefault(key) + 1;

    private sealed record CategorySample (string Name, string Slug, string? ParentSlug);
}
