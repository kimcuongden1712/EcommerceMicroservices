namespace ObjectStorage.Abstractions.Models;

public sealed record ObjectDescriptor
{
    public required string BucketName { get; init; }

    public required string ObjectKey { get; init; }

    public string? ETag { get; init; }

    public long ContentLength { get; init; }

    public string? ContentType { get; init; }

    public DateTimeOffset? LastModified { get; init; }
}
