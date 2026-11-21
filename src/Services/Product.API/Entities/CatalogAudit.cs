using Contracts.Domains;

namespace Product.API.Entities;

public class CatalogAudit : EntityBase<long>
{
    public int EntityType
    {
        get; set;
    }
    public long EntityId
    {
        get; set;
    }
    public required string Action
    {
        get; set;
    }
    public required string ActorId
    {
        get; set;
    }
    public required string CorrelationId
    {
        get; set;
    }
    public required string ChangedFieldsJson
    {
        get; set;
    }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
