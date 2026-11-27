using Microsoft.Extensions.Options;
using ObjectStorage.Abstractions.Exceptions;
using ObjectStorage.Abstractions.Interfaces;
using Product.API.Commons;
using Product.API.Commons.Exceptions;
using Product.API.Options;
using Product.API.Repositories.Interfaces;
using Product.API.Repositories.Models;
using Product.API.Services.V1.Interfaces;

namespace Product.API.Services.V1;

public sealed class ProductImageDeliveryService : IProductImageDeliveryService
{
    private readonly IProductImageRepository _productImageRepository;
    private readonly IObjectStorage _objectStorage;
    private readonly ProductMediaOptions _productMediaOptions;

    public ProductImageDeliveryService (
        IProductImageRepository productImageRepository,
        IObjectStorage objectStorage,
        IOptions<ProductMediaOptions> productMediaOptions)
    {
        _productImageRepository = productImageRepository;
        _objectStorage = objectStorage;
        _productMediaOptions = productMediaOptions.Value;
    }

    #region Image delivery

    public async Task<ProductImageDeliveryData?> GetPublicImageMetadataAsync (
        string assetKey,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(assetKey, "N", out _))
            return null;

        var publicImage = await _productImageRepository.GetPublicProductImageAsync(assetKey, cancellationToken);
        if (publicImage is null)
            return null;

        try {
            var stored = await _objectStorage.GetMetadataAsync(_productMediaOptions.MediaBucket, assetKey, cancellationToken);
            if (stored is null || stored.ContentLength != publicImage.ContentLength ||
                !string.Equals(stored.ContentType, publicImage.ContentType, StringComparison.OrdinalIgnoreCase) ||
                !_productMediaOptions.AllowedContentTypes.Contains(stored.ContentType ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                return null;

            return publicImage;
        }
        catch (ObjectStorageException exception) {
            throw StorageUnavailable(exception);
        }
    }

    public async Task<Stream> OpenPublicImageStreamAsync (
        string assetKey,
        long expectedLength,
        CancellationToken cancellationToken)
    {
        var temporaryFile = Path.Combine(Path.GetTempPath(), $"product-media-{Guid.NewGuid():N}.tmp");
        var output = new FileStream(
            temporaryFile,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);
        try {
            await using (var limited = new SizeLimitedWriteStream(output, expectedLength)) {
                await _objectStorage.DownloadAsync(_productMediaOptions.MediaBucket, assetKey, limited, cancellationToken);
                if (limited.BytesWritten != expectedLength)
                    throw new IOException("The stored image length no longer matches its verified metadata.");
            }
            output.Position = 0;
            return output;
        }
        catch (ObjectStorageException exception) {
            await output.DisposeAsync();
            try {
                File.Delete(temporaryFile);
            }
            catch (IOException) { }
            throw StorageUnavailable(exception);
        }
        catch {
            await output.DisposeAsync();
            try {
                File.Delete(temporaryFile);
            }
            catch (IOException) { }
            throw;
        }
    }

    #region Private

    private static ProductMediaException StorageUnavailable (ObjectStorageException exception) =>
        new(
            StatusCodes.Status503ServiceUnavailable,
            "PRODUCT_MEDIA_UNAVAILABLE",
            "Product media temporarily unavailable",
            "The requested product image cannot be retrieved right now.",
            exception);

    #endregion

    #endregion
}
