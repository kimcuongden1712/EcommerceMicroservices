namespace ObjectStorage.Abstractions.Models;

public sealed record ObjectUploadRequest
{
    public required string BucketName { get; init; }

    public required string ObjectKey { get; init; }

    public string? ContentType { get; init; }

    public IReadOnlyDictionary<string, string> Metadata { get; init; }
        = new Dictionary<string, string>();
}
