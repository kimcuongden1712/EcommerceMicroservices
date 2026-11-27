using Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Product.API.Commons;
using Product.API.Commons.Enums;
using Product.API.Commons.Exceptions;
using Product.API.Contracts.V1;
using Product.API.Entities;
using Product.API.Extensions;
using Product.API.Options;
using Product.API.Repositories.Interfaces;
using Product.API.Services.V1.Interfaces;
using System.Globalization;
using System.Text.Json;

namespace Product.API.Services.V1;

public sealed class ProductImageGalleryService : IProductImageGalleryService
{
    private readonly IProductRepository _productRepository;
    private readonly IProductImageRepository _productImageRepository;
    private readonly IProductImageUploadRepository _productImageUploadRepository;
    private readonly ProductMediaOptions _productMediaOptions;
    private readonly ICatalogAuditRepository _catalogAuditRepository;
    private readonly ICatalogOutboxRepository _catalogOutboxRepository;

    public ProductImageGalleryService (
        IProductRepository productRepository,
        IProductImageRepository productImageRepository,
        IProductImageUploadRepository productImageUploadRepository,
        ICatalogAuditRepository catalogAuditRepository,
        ICatalogOutboxRepository catalogOutboxRepository,
        IOptions<ProductMediaOptions> productMediaOptions)
    {
        _productRepository = productRepository;
        _productImageRepository = productImageRepository;
        _productImageUploadRepository = productImageUploadRepository;
        _catalogAuditRepository = catalogAuditRepository;
        _catalogOutboxRepository = catalogOutboxRepository;
        _productMediaOptions = productMediaOptions.Value;
    }

    #region Image gallery

    public async Task<ProductImageGalleryResult> ReplaceGalleryAsync (
        long productId,
        long expectedVersion,
        ReplaceProductImagesRequest request,
        CatalogAuditContext auditContext,
        CancellationToken cancellationToken)
    {
        if (request.Images.Count > _productMediaOptions.MaxImagesPerProduct)
            throw new ProductMediaException("IMAGE_LIMIT_EXCEEDED", "The gallery exceeds the maximum image count.");

        var uploadIds = new List<long>(request.Images.Count);
        var assetKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var image in request.Images) {
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

        await using var transaction = await _productImageRepository.BeginTransactionAsync(cancellationToken);
        try {
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
            for (var index = 0; index < request.Images.Count; index++) {
                var upload = await _productImageUploadRepository.GetProductImageUploadAsync(
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
                _productImageUploadRepository.Update(upload);
                uploads.Add(upload);
            }

            var existing = await _productImageRepository.FindByCondition(image => image.ProductId == productId)
                .OrderBy(image => image.Position)
                .ThenBy(image => image.Id)
                .ToListAsync(cancellationToken);
            var oldAssetKeys = existing.Select(image => image.AssetKey).ToHashSet(StringComparer.Ordinal);
            var removedAssetKeys = oldAssetKeys.Except(assetKeys, StringComparer.Ordinal).ToArray();

            await _productImageRepository.DeleteListAsync(existing, cancellationToken);

            for (var position = 0; position < uploads.Count; position++) {
                _productImageRepository.Create(new ProductImages
                {
                    ProductId = productId,
                    AssetKey = uploads[position].AssetKey!,
                    AltText = request.Images[position].AltText?.Trim() ?? string.Empty,
                    Position = position
                });
            }

            var now = DateTimeOffset.UtcNow;
            foreach (var key in removedAssetKeys) {
                await _productImageUploadRepository.MarkAssetUnreferencedForCleanupAsync(
                    key,
                    now.AddMinutes(_productMediaOptions.CleanupRetentionMinutes),
                    cancellationToken);
            }

            var previousVersion = product.Version;
            product.Version++;
            _productRepository.Update(product);
            _catalogAuditRepository.Create(new CatalogAudit
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
            _catalogOutboxRepository.Create(new CatalogOutbox
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

            await _productImageRepository.EndTransactionAsync(cancellationToken);

            var result = await _productImageRepository.GetProductImagesAsync(productId, cancellationToken);
            return new ProductImageGalleryResult(product.Version, result);
        }
        catch (DbUpdateConcurrencyException) {
            await _productImageRepository.RollbackTransactionAsync(CancellationToken.None);
            throw CatalogAdminException.PreconditionFailed();
        }
        catch {
            await _productImageRepository.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }

    #endregion
}
