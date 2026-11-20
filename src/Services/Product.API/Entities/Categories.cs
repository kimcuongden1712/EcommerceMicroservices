using Contracts.Domains.Interfaces;

namespace Product.API.Entities;

public class Categories : EntityAuditBase<long>
{
    public long? ParentId { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public int Status { get; set; }
    public int SortOrder { get; set; }
    public long Version { get; set; } = 1;
}
