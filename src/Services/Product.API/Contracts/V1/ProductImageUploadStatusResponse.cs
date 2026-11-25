using Product.API.Commons.Enums;

namespace Product.API.Contracts.V1;

public sealed record ProductImageUploadStatusResponse(
    string UploadId,
    ProductImageUploadStatus Status,
    DateTimeOffset ExpiresAt);
