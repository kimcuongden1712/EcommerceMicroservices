namespace Product.API.Contracts.V1;

public enum ProductSortOrder
{
    Newest,
    PriceAscending,
    PriceDescending
}

public sealed record ProductListQuery(
    string? SearchTerm,
    string? CategorySlug,
    string? ColorCode,
    string? SizeCode,
    int? MinPriceMinor,
    int? MaxPriceMinor,
    ProductSortOrder Sort,
    int Page,
    int PageSize);

public sealed class ProductListQueryRequest
{
    public string? Q { get; init; }
    public string? CategorySlug { get; init; }
    public string? Color { get; init; }
    public string? Size { get; init; }
    public int? MinPriceMinor { get; init; }
    public int? MaxPriceMinor { get; init; }
    public string? Sort { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
}
