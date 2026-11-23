using Product.API.Commons.Enums;

namespace Product.API.Repositories.Models
{
    public sealed record CategoryHierarchyItem (long Id, long? ParentId);


    public sealed record AdminProductListData (
        long Id,
        long CategoryId,
        string Name,
        string Slug,
        ProductStatus Status,
        long Version);

    public sealed record PublicProductListItemData (
    long Id,
    string Slug,
    string Name,
    DateTimeOffset? PublishedAt,
    string CategorySlug,
    string AssetKey,
    string AltText,
    int PriceFromMinor);

    public sealed record PublicProductDetailData (
        long Id,
        string Slug,
        string Name,
        string CategorySlug,
        string? Description,
        IReadOnlyList<PublicProductImageData> Images,
        IReadOnlyList<PublicProductVariantData> Variants);

    public sealed record PublicProductImageData (string AssetKey, string AltText);

    public sealed record PublicProductVariantData (
        long Id,
        string Sku,
        string ColorCode,
        string ColorLabel,
        string SizeCode,
        string SizeLabel,
        int PriceMinor);
}
