using Contracts.Domains;

namespace Product.API.Entities;

public class CatalogOutbox : EntityBase<long>
{
    public required string Topic
    {
        get; set;
    }
    public required string PayloadJson
    {
        get; set;
    }
    public int Attempts
    {
        get; set;
    }
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt
    {
        get; set;
    }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
