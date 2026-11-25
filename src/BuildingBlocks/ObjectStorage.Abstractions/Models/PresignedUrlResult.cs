namespace ObjectStorage.Abstractions.Models;

public sealed record PresignedUrlResult
{
    public required string Url { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public IReadOnlyDictionary<string, string> RequiredHeaders { get; init; }
        = new Dictionary<string, string>();
}
