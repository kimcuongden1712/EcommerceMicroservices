namespace Product.API.Repositories.Models;

public sealed record ProductImageDeliveryData(
    string AssetKey,
    string ContentType,
    long ContentLength);
