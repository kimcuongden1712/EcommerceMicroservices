using Contracts.Domains;

namespace Product.API.Entities;

public class ProductImageUploads : EntityBase<long>
{
    public long ProductId { get; set; }
    public required string StagingKey { get; set; }
    public string? AssetKey { get; set; }
    public required string ExpectedContentType { get; set; }
    public long ExpectedSize { get; set; }
    public int Status { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
