namespace Product.API.Contracts.V1;

public sealed record CategoryResponse(
    string Id,
    string Name,
    string Slug,
    int SortOrder,
    IReadOnlyList<CategoryResponse> Children);

public sealed record ProductListResponse(
    IReadOnlyList<ProductListItemResponse> Items,
    int Page,
    int PageSize,
    bool HasNext);

public sealed record ProductListItemResponse(
    string Id,
    string Slug,
    string Name,
    string CategorySlug,
    ProductThumbnailResponse Thumbnail,
    int PriceFromMinor,
    string Currency);

public sealed record ProductThumbnailResponse(string Url, string AltText);

public sealed record ProductDetailResponse(
    string Id,
    string Slug,
    string Name,
    string CategorySlug,
    string? Description,
    IReadOnlyList<ProductImageResponse> Images,
    IReadOnlyList<ProductVariantResponse> Variants);

public sealed record ProductImageResponse(string Url, string AltText);

public sealed record ProductVariantResponse(
    string Id,
    string Sku,
    string ColorCode,
    string ColorLabel,
    string SizeCode,
    string SizeLabel,
    int PriceMinor,
    string Currency);
