using Contracts.Domains;

namespace Product.API.Entities;

public class ProductImages : EntityBase<long>
{
    public long ProductId { get; set; }
    public required string AssetKey { get; set; }
    public required string AltText { get; set; }
    public int Position { get; set; }
}
