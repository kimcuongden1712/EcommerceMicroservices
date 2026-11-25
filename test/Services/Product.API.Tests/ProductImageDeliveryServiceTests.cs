using Moq;
using Microsoft.AspNetCore.Http;
using ObjectStorage.Abstractions.Exceptions;
using ObjectStorage.Abstractions.Interfaces;
using ObjectStorage.Abstractions.Models;
using Product.API.Commons.Exceptions;
using Product.API.Options;
using Product.API.Repositories.Interfaces;
using Product.API.Repositories.Models;
using Product.API.Services.V1;
using Microsoft.Extensions.Options;

namespace Product.API.Tests;

public sealed class ProductImageDeliveryServiceTests
{
    private static readonly CancellationToken TestToken = CancellationToken.None;

    [Fact]
    public async Task Invalid_asset_key_is_rejected_without_storage_access()
    {
        var images = new Mock<IProductImageRepository>(MockBehavior.Strict);
        var storage = new Mock<IObjectStorage>(MockBehavior.Strict);
        var service = CreateService(images, storage);

        var result = await service.GetPublicImageMetadataAsync("../staging/file.jpg", TestToken);

        Assert.Null(result);
        images.VerifyNoOtherCalls();
        storage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Image_must_be_referenced_by_the_public_catalog_before_storage_is_read()
    {
        var images = new Mock<IProductImageRepository>();
        var storage = new Mock<IObjectStorage>(MockBehavior.Strict);
        var assetKey = Guid.NewGuid().ToString("N");
        images.Setup(repository => repository.GetPublicProductImageAsync(assetKey, TestToken))
            .ReturnsAsync((ProductImageDeliveryData?)null);
        var service = CreateService(images, storage);

        var result = await service.GetPublicImageMetadataAsync(assetKey, TestToken);

        Assert.Null(result);
        storage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Matching_public_metadata_is_returned()
    {
        var images = new Mock<IProductImageRepository>();
        var storage = new Mock<IObjectStorage>();
        var assetKey = Guid.NewGuid().ToString("N");
        var publicImage = new ProductImageDeliveryData(assetKey, "image/png", 12);
        images.Setup(repository => repository.GetPublicProductImageAsync(assetKey, TestToken))
            .ReturnsAsync(publicImage);
        storage.Setup(client => client.GetMetadataAsync("product-media", assetKey, TestToken))
            .ReturnsAsync(new ObjectMetadata { Key = assetKey, ContentType = "image/png", ContentLength = 12 });
        var service = CreateService(images, storage);

        var result = await service.GetPublicImageMetadataAsync(assetKey, TestToken);

        Assert.Equal(publicImage, result);
    }

    [Fact]
    public async Task Metadata_mismatch_is_not_served()
    {
        var images = new Mock<IProductImageRepository>();
        var storage = new Mock<IObjectStorage>();
        var assetKey = Guid.NewGuid().ToString("N");
        images.Setup(repository => repository.GetPublicProductImageAsync(assetKey, TestToken))
            .ReturnsAsync(new ProductImageDeliveryData(assetKey, "image/png", 12));
        storage.Setup(client => client.GetMetadataAsync("product-media", assetKey, TestToken))
            .ReturnsAsync(new ObjectMetadata { Key = assetKey, ContentType = "image/jpeg", ContentLength = 12 });
        var service = CreateService(images, storage);

        var result = await service.GetPublicImageMetadataAsync(assetKey, TestToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task Storage_failures_are_mapped_to_service_unavailable()
    {
        var images = new Mock<IProductImageRepository>();
        var storage = new Mock<IObjectStorage>();
        var assetKey = Guid.NewGuid().ToString("N");
        images.Setup(repository => repository.GetPublicProductImageAsync(assetKey, TestToken))
            .ReturnsAsync(new ProductImageDeliveryData(assetKey, "image/png", 12));
        storage.Setup(client => client.GetMetadataAsync("product-media", assetKey, TestToken))
            .ThrowsAsync(new ObjectStorageException("GetMetadata", "product-media", assetKey, "Storage unavailable."));
        var service = CreateService(images, storage);

        var exception = await Assert.ThrowsAsync<ProductMediaException>(
            () => service.GetPublicImageMetadataAsync(assetKey, TestToken));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, exception.StatusCode);
        Assert.DoesNotContain("product-media", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Image_stream_returns_the_verified_object_bytes()
    {
        var images = new Mock<IProductImageRepository>();
        var storage = new Mock<IObjectStorage>();
        var assetKey = Guid.NewGuid().ToString("N");
        var bytes = new byte[] { 1, 2, 3, 4 };
        storage.Setup(client => client.DownloadAsync("product-media", assetKey, It.IsAny<Stream>(), TestToken))
            .Returns((string _, string _, Stream destination, CancellationToken token) =>
                destination.WriteAsync(bytes.AsMemory(), token).AsTask());
        var service = CreateService(images, storage);

        await using var image = await service.OpenPublicImageStreamAsync(assetKey, bytes.Length, TestToken);
        using var output = new MemoryStream();
        await image.CopyToAsync(output, TestToken);

        Assert.Equal(bytes, output.ToArray());
    }

    private static ProductImageDeliveryService CreateService(
        Mock<IProductImageRepository> images,
        Mock<IObjectStorage> storage) =>
        new(images.Object, storage.Object, Microsoft.Extensions.Options.Options.Create(new ProductMediaOptions()));
}
