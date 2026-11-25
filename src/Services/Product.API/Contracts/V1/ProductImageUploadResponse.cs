namespace Product.API.Contracts.V1;

public sealed record ProductImageUploadResponse(
    string UploadId,
    string UploadUrl,
    IReadOnlyDictionary<string, string> Headers,
    DateTimeOffset ExpiresAt);