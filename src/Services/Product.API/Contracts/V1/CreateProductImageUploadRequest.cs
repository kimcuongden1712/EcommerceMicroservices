namespace Product.API.Contracts.V1;

public sealed class CreateProductImageUploadRequest
{
    public string? ContentType { get; init; }

    public long ContentLength { get; init; }
}