namespace Product.API.Contracts.V1;

public sealed class ProductImageItemRequest
{
    public string UploadId { get; init; } = string.Empty;

    public string? AltText { get; init; }
}
