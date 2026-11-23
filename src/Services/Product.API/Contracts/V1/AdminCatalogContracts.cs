using Product.API.Commons.Enums;

namespace Product.API.Contracts.V1;

public sealed class CreateCategoryRequest
{
    public long? ParentId { get; init; }
    public string? Name { get; init; }
    public string? Slug { get; init; }
    public int? SortOrder { get; init; }
}

public sealed class UpdateCategoryRequest
{
    public string? Name { get; init; }
    public string? Slug { get; init; }
    public int? SortOrder { get; init; }
}

public sealed class UpdateCategoryStatusRequest
{
    public string? Status { get; init; }
}

public sealed record AdminCategoryResponse(
    string Id,
    string? ParentId,
    string Name,
    string Slug,
    string Status,
    int SortOrder,
    long Version);

public sealed record AdminProductListItemResponse(
    string Id,
    string CategoryId,
    string Name,
    string Slug,
    string Status,
    long Version);

public sealed record AdminProductListResponse(
    IReadOnlyList<AdminProductListItemResponse> Items,
    int Page,
    int PageSize,
    bool HasNext);

public sealed class AdminProductListQueryRequest
{
    public string? Q { get; init; }
    public long? CategoryId { get; init; }
    public string? Status { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
}

public sealed record AdminProductListQuery(
    string? SearchTerm,
    long? CategoryId,
    ProductStatus? Status,
    int Page,
    int PageSize);

public sealed class CreateProductRequest
{
    public string? Name { get; init; }
    public string? Slug { get; init; }
    public long? CategoryId { get; init; }
    public string? Description { get; init; }
}

public sealed class UpdateProductRequest
{
    public string? Name { get; init; }
    public string? Slug { get; init; }
    public long? CategoryId { get; init; }
    public string? Description { get; init; }
}

public sealed class UpdateProductStatusRequest
{
    public string? Status { get; init; }
}

public sealed record AdminProductResponse(
    string Id,
    string CategoryId,
    string Name,
    string Slug,
    string? Description,
    string Status,
    DateTimeOffset? PublishedAt,
    long Version,
    IReadOnlyList<AdminProductVariantResponse> Variants,
    IReadOnlyList<AdminProductImageResponse> Images);

public sealed record AdminProductVariantResponse(
    string Id,
    string Sku,
    string ColorCode,
    string ColorLabel,
    string SizeCode,
    string SizeLabel,
    int? PriceMinor,
    string Status,
    int? WeightG,
    int? LengthMm,
    int? WidthMm,
    int? HeightMm);

public sealed record AdminProductImageResponse(
    string Id,
    string AssetKey,
    string AltText,
    int Position);

public sealed class CreateProductVariantRequest
{
    public string? Sku { get; init; }
    public string? ColorCode { get; init; }
    public string? ColorLabel { get; init; }
    public string? SizeCode { get; init; }
    public string? SizeLabel { get; init; }
    public int? PriceMinor { get; init; }
    public int? WeightG { get; init; }
    public int? LengthMm { get; init; }
    public int? WidthMm { get; init; }
    public int? HeightMm { get; init; }
}

public sealed class UpdateProductVariantRequest
{
    public string? ColorLabel { get; init; }
    public string? SizeLabel { get; init; }
    public int? PriceMinor { get; init; }
    public int? WeightG { get; init; }
    public int? LengthMm { get; init; }
    public int? WidthMm { get; init; }
    public int? HeightMm { get; init; }
}

public sealed class UpdateProductVariantStatusRequest
{
    public string? Status { get; init; }
}

public sealed record AdminProductVariantDetailResponse(
    AdminProductVariantResponse Variant,
    long ProductVersion);
