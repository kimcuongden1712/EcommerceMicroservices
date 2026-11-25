using Contracts.Domains;
using Product.API.Commons.Enums;

namespace Product.API.Entities;

public class ProductImageUploads : EntityBase<long>
{
    public long ProductId
    {
        get; set;
    }
    public required string StagingKey
    {
        get; set;
    }
    public string? AssetKey
    {
        get; set;
    }
    public required string ExpectedContentType
    {
        get; set;
    }
    public long ExpectedSize
    {
        get; set;
    }
    public ProductImageUploadStatus Status
    {
        get; set;
    }
    public DateTimeOffset ExpiresAt
    {
        get; set;
    }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public int AttemptCount
    {
        get; set;
    }

    public int CleanupAttempts
    {
        get; set;
    }

    public string? CorrelationId
    {
        get; set;
    }

    public DateTimeOffset? NextAttemptAt
    {
        get; set;
    }

    public DateTimeOffset? ProcessingStartedAt
    {
        get; set;
    }

    public DateTimeOffset? StagingDeletedAt
    {
        get; set;
    }

}
