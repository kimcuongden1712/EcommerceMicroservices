namespace Product.API.Contracts.V1;

public sealed record ProductImageGalleryResult(
    long ProductVersion,
    IReadOnlyList<AdminProductImageResponse> Images);
