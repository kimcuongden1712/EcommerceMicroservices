namespace ObjectStorage.Abstractions.Models;

public sealed record ObjectMetadata
{
    public required string Key { get; init; }

    public string? ContentType { get; init; }

    public long ContentLength { get; init; }

    public string? ETag { get; init; }

    public DateTimeOffset? LastModified { get; init; }

    public IReadOnlyDictionary<string, string> CustomMetadata { get; init; }
        = new Dictionary<string, string>();
}
