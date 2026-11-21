using Contracts.Domains;
using Product.API.Enum;

namespace Product.API.Entities;

public class ProductVariants : EntityBase<long>
{
    public long ProductId
    {
        get; set;
    }
    public required string Sku
    {
        get; set;
    }
    public required string ColorCode
    {
        get; set;
    }
    public required string ColorLabel
    {
        get; set;
    }
    public required string SizeCode
    {
        get; set;
    }
    public required string SizeLabel
    {
        get; set;
    }
    public int? PriceMinor
    {
        get; set;
    }
    public ProductVariantStatus Status
    {
        get; set;
    }
    public int? WeightG
    {
        get; set;
    }
    public int? LengthMm
    {
        get; set;
    }
    public int? WidthMm
    {
        get; set;
    }
    public int? HeightMm
    {
        get; set;
    }
}
