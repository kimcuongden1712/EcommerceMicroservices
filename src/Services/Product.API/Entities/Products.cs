using Contracts.Domains.Interfaces;

namespace Product.API.Entities;

public class Products : EntityAuditBase<long>
{
    public long CategoryId { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public int Status { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public long Version { get; set; } = 1;
}
