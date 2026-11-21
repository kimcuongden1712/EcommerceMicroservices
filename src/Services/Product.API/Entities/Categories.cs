using Contracts.Domains.Interfaces;
using Product.API.Enum;

namespace Product.API.Entities;

public class Categories : EntityAuditBase<long>
{
    public long? ParentId
    {
        get; set;
    }
    public required string Name
    {
        get; set;
    }
    public required string Slug
    {
        get; set;
    }
    public CategoryStatus Status
    {
        get; set;
    }
    public int SortOrder
    {
        get; set;
    }
    public long Version { get; set; } = 1;
}
