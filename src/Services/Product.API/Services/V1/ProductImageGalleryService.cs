using Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;
using Product.API.Commons;
using Product.API.Commons.Enums;
using Product.API.Commons.Exceptions;
using Product.API.Contracts.V1;
using Product.API.Entities;
using Product.API.Options;
using Product.API.Persistence;
using Product.API.Repositories.Interfaces;
using Product.API.Services.V1.Interfaces;
using System.Globalization;
using System.Text.Json;

namespace Product.API.Services.V1;

public sealed class ProductImageGalleryService : IProductImageGalleryService
{
    private readonly ProductContext _dbContext;
    private readonly IProductRepository _productRepository;
    private readonly IProductImageRepository _imageRepository;
    private readonly IProductImageUploadRepository _uploadRepository;
    private readonly ProductMediaOptions _options;

    public ProductImageGalleryService(
        ProductContext dbContext,
        IProductRepository productRepository,
        IProductImageRepository imageRepository,
        IProductImageUploadRepository uploadRepository,
        Microsoft.Extensions.Options.IOptions<ProductMediaOptions> options)
    {
        _dbContext = dbContext;
        _productRepository = productRepository;
        _imageRepository = imageRepository;
        _uploadRepository = uploadRepository;
        _options = options.Value;
    }

    public async Task<ProductImageGalleryResult> ReplaceGalleryAsync(
        long productId,
        long expectedVersion,
        ReplaceProductImagesRequest request,
        CatalogAuditContext auditContext,
        CancellationToken cancellationToken)
    {
        if (request.Images.Count > _options.MaxImagesPerProduct)
            throw new ProductMediaException("IMAGE_LIMIT_EXCEEDED", "The gallery exceeds the maximum image count.");

        var uploadIds = new List<long>(request.Images.Count);
        var assetKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var image in request.Images)
        {
            if (!long.TryParse(image.UploadId, NumberStyles.None, CultureInfo.InvariantCulture, out var uploadId) || uploadId <= 0)
                throw new BadRequestException(
                    "UploadId must be a positive integer.",
                    "INVALID_UPLOAD_ID",
                    new Dictionary<string, string[]> { ["images.uploadId"] = ["UploadId must be a positive integer."] },
                    "Product image validation failed");
            if (!uploadIds.AddDistinct(uploadId))
                throw new ProductMediaException("DUPLICATE_UPLOAD_ID", "Duplicate upload IDs are not allowed.");
            if (image.AltText is { Length: > 160 })
                throw new ProductMediaException("ALT_TEXT_TOO_LONG", "Image alt text cannot exceed 160 characters.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var product = await _productRepository.LockProductAsync(productId, cancellationToken);
            if (product is null)
                throw CatalogAdminException.NotFound("product");
            if (product.Version != expectedVersion)
                throw CatalogAdminException.PreconditionFailed();
            if (product.Status == ProductStatus.Published && request.Images.Count == 0)
                throw new ProductMediaException(
                    StatusCodes.Status409Conflict,
                    "PUBLISHED_PRODUCT_REQUIRES_IMAGE",
                    "Published product requires a primary image",
                    "A published product must retain at least one ready gallery image.");

            var uploads = new List<ProductImageUploads>(request.Images.Count);
            for (var index = 0; index < request.Images.Count; index++)
            {
                var upload = await _uploadRepository.GetProductImageUploadAsync(
                    productId,
                    uploadIds[index],
                    cancellationToken);
                if (upload is null)
                    throw CatalogAdminException.NotFound("product image upload");
                if (upload.Status != ProductImageUploadStatus.Ready || string.IsNullOrWhiteSpace(upload.AssetKey))
                    throw new ProductMediaException("UPLOAD_NOT_READY", "Every gallery image must have a completed upload.");
                if (!assetKeys.Add(upload.AssetKey))
                    throw new ProductMediaException("DUPLICATE_ASSET_KEY", "The gallery cannot contain the same asset more than once.");
                upload.NextAttemptAt = null;
                _uploadRepository.Update(upload);
                uploads.Add(upload);
            }

            var existing = await _dbContext.ProductImages
                .Where(image => image.ProductId == productId)
                .OrderBy(image => image.Position)
                .ThenBy(image => image.Id)
                .ToListAsync(cancellationToken);
            var oldAssetKeys = existing.Select(image => image.AssetKey).ToHashSet(StringComparer.Ordinal);
            var removedAssetKeys = oldAssetKeys.Except(assetKeys, StringComparer.Ordinal).ToArray();

            _dbContext.ProductImages.RemoveRange(existing);
            await _dbContext.SaveChangesAsync(cancellationToken);

            for (var position = 0; position < uploads.Count; position++)
            {
                _dbContext.ProductImages.Add(new ProductImages
                {
                    ProductId = productId,
                    AssetKey = uploads[position].AssetKey!,
                    AltText = request.Images[position].AltText?.Trim() ?? string.Empty,
                    Position = position
                });
            }

            var now = DateTimeOffset.UtcNow;
            foreach (var key in removedAssetKeys)
            {
                await _uploadRepository.MarkAssetUnreferencedForCleanupAsync(
                    key,
                    now.AddMinutes(_options.CleanupRetentionMinutes),
                    cancellationToken);
            }

            var previousVersion = product.Version;
            product.Version++;
            _dbContext.Products.Update(product);
            _dbContext.CatalogAudit.Add(new CatalogAudit
            {
                EntityType = Const.PRODUCTENTITYTYPE,
                EntityId = productId,
                Action = "ImagesReplaced",
                ActorId = auditContext.ActorId,
                CorrelationId = auditContext.CorrelationId,
                ChangedFieldsJson = JsonSerializer.Serialize(new
                {
                    previousVersion,
                    product.Version,
                    removedAssetCount = removedAssetKeys.Length,
                    imageCount = uploads.Count
                }),
                OccurredAt = now
            });
            _dbContext.CatalogOutbox.Add(new CatalogOutbox
            {
                Topic = "catalog.product.changed.v1",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    eventId = Guid.NewGuid().ToString("N"),
                    occurredAt = now,
                    productId,
                    productVersion = product.Version,
                    changeType = "ImagesReplaced",
                    affectedSkus = Array.Empty<string>(),
                    correlationId = auditContext.CorrelationId
                }),
                Attempts = 0,
                NextAttemptAt = now,
                OccurredAt = now
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var result = await _imageRepository.GetProductImagesAsync(productId, cancellationToken);
            return new ProductImageGalleryResult(product.Version, result);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw CatalogAdminException.PreconditionFailed();
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}

internal static class CollectionExtensions
{
    public static bool AddDistinct<T>(this ICollection<T> collection, T value)
    {
        if (collection.Contains(value))
            return false;
        collection.Add(value);
        return true;
    }
}
