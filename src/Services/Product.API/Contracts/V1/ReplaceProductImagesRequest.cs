namespace Product.API.Contracts.V1;

public sealed class ReplaceProductImagesRequest
{
    public IReadOnlyList<ProductImageItemRequest> Images { get; init; } = [];
}
