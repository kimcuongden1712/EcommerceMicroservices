using Microsoft.Extensions.Options;
using ObjectStorage.Abstractions.Exceptions;
using ObjectStorage.Abstractions.Interfaces;
using ObjectStorage.Abstractions.Models;
using Product.API.Commons;
using Product.API.Commons.Enums;
using Product.API.Entities;
using Product.API.Options;
using Product.API.Repositories.Interfaces;
using Product.API.Services.V1.Interfaces;
using System.Security.Cryptography;

namespace Product.API.Services.V1;

public sealed class ProductImageUploadProcessingService : IProductImageUploadProcessingService
{
    private const int BatchSize = 10;

    private readonly IProductRepository _productRepository;
    private readonly IProductImageRepository _productImageRepository;
    private readonly IProductImageUploadRepository _productImageUploadRepository;
    private readonly IObjectStorage _objectStorage;
    private readonly ProductMediaOptions _productMediaOptions;
    private readonly ILogger<ProductImageUploadProcessingService> _logger;

    public ProductImageUploadProcessingService (
        IProductRepository productRepository,
        IProductImageRepository imageRepository,
        IProductImageUploadRepository uploadRepository,
        IObjectStorage objectStorage,
        IOptions<ProductMediaOptions> options,
        ILogger<ProductImageUploadProcessingService> logger)
    {
        _productRepository = productRepository;
        _productImageRepository = imageRepository;
        _productImageUploadRepository = uploadRepository;
        _objectStorage = objectStorage;
        _productMediaOptions = options.Value;
        _logger = logger;
    }

    #region Upload processing

    public async Task ProcessUploadAsync (ProductImageUploads candidate, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (!await _productImageUploadRepository.TryClaimPendingUploadAsync(
                candidate.Id,
                now,
                now - ProductImageUploadDefaults.ProcessingLease,
                cancellationToken))
            return;

        var upload = await _productImageUploadRepository.GetUploadByIdAsync(candidate.Id, true, cancellationToken)
            ?? throw new InvalidOperationException("The claimed image upload no longer exists.");

        if (upload.Status != ProductImageUploadStatus.Pending)
            return;

        string? temporaryFile = null;
        try {
            var source = await _objectStorage.GetMetadataAsync(
                _productMediaOptions.StagingBucket,
                upload.StagingKey,
                cancellationToken);
            if (source is null) {
                await ReleaseForRetryAsync(upload.Id, null, cancellationToken);
                return;
            }

            if (source.ContentLength <= 0 || source.ContentLength != upload.ExpectedSize ||
                source.ContentLength > _productMediaOptions.MaxFileSizeBytes) {
                await RejectAsync(upload, "SIZE_MISMATCH", cancellationToken);
                return;
            }

            if (!IsAllowedContentType(source.ContentType) ||
                !string.Equals(source.ContentType, upload.ExpectedContentType, StringComparison.OrdinalIgnoreCase)) {
                await RejectAsync(upload, "CONTENT_TYPE_MISMATCH", cancellationToken);
                return;
            }

            temporaryFile = Path.Combine(Path.GetTempPath(), $"product-image-{Guid.NewGuid():N}.tmp");
            long written;
            await using (var output = new FileStream(
                temporaryFile,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var limited = new SizeLimitedWriteStream(output, upload.ExpectedSize)) {
                await _objectStorage.DownloadAsync(
                    _productMediaOptions.StagingBucket,
                    upload.StagingKey,
                    limited,
                    cancellationToken);
                await output.FlushAsync(cancellationToken);
                written = limited.BytesWritten;
            }

            if (written != upload.ExpectedSize) {
                await RejectAsync(upload, "SIZE_MISMATCH", cancellationToken);
                return;
            }

            string? actualContentType;
            await using (var input = new FileStream(
                temporaryFile,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan)) {
                actualContentType = ProductImageInspector.TryDecode(input, out _, out _);
            }

            if (!string.Equals(actualContentType, upload.ExpectedContentType, StringComparison.OrdinalIgnoreCase)) {
                await RejectAsync(upload, "INVALID_IMAGE_CONTENT", cancellationToken);
                return;
            }

            string contentSha256;
            await using (var validatedInput = new FileStream(
                temporaryFile,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan)) {
                contentSha256 = Convert.ToHexString(await SHA256.HashDataAsync(validatedInput, cancellationToken))
                    .ToLowerInvariant();
            }

            var destination = await _objectStorage.GetMetadataAsync(
                _productMediaOptions.MediaBucket,
                upload.AssetKey!,
                cancellationToken);
            if (destination is null) {
                await using var validatedImage = new FileStream(
                    temporaryFile,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    64 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await _objectStorage.UploadAsync(
                    new ObjectUploadRequest
                    {
                        BucketName = _productMediaOptions.MediaBucket,
                        ObjectKey = upload.AssetKey!,
                        ContentType = actualContentType,
                        Metadata = new Dictionary<string, string> { ["content-sha256"] = contentSha256 }
                    },
                    validatedImage,
                    upload.ExpectedSize,
                    cancellationToken);
                destination = await _objectStorage.GetMetadataAsync(
                    _productMediaOptions.MediaBucket,
                    upload.AssetKey!,
                    cancellationToken);
            }

            if (destination is null || destination.ContentLength != upload.ExpectedSize ||
                !string.Equals(destination.ContentType, actualContentType, StringComparison.OrdinalIgnoreCase) ||
                !destination.CustomMetadata.TryGetValue("content-sha256", out var storedDigest) ||
                !string.Equals(storedDigest, contentSha256, StringComparison.Ordinal)) {
                await RejectAsync(upload, "DESTINATION_VERIFICATION_FAILED", cancellationToken);
                return;
            }

            // Persist completed work even when the worker is stopping.
            upload.Status = ProductImageUploadStatus.Ready;
            upload.ProcessingStartedAt = null;
            upload.NextAttemptAt = null;
            await _productImageUploadRepository.UpdateAsync(upload, CancellationToken.None);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            if (upload.Status == ProductImageUploadStatus.Pending && upload.ProcessingStartedAt is not null)
                await ReleaseForRetryAsync(upload.Id, null, CancellationToken.None);
            throw;
        }
        catch (SizeLimitExceededException) {
            await RejectAsync(upload, "SIZE_MISMATCH", cancellationToken);
        }
        catch (ObjectStorageException exception) {
            await ReleaseForRetryAsync(upload.Id, exception, cancellationToken);
        }
        catch (IOException exception) {
            await ReleaseForRetryAsync(upload.Id, exception, cancellationToken);
        }
        finally {
            if (temporaryFile is not null) {
                try {
                    File.Delete(temporaryFile);
                }
                catch (IOException exception) {
                    _logger.LogWarning(exception, "Temporary image cleanup failed for upload {UploadId}.", candidate.Id);
                }
            }
        }
    }

    #endregion

    #region Expired uploads

    public async Task ProcessExpiredUploadsAsync (CancellationToken cancellationToken)
    {
        var expired = await _productImageUploadRepository.GetExpiredUploadsAsync(BatchSize, cancellationToken);
        foreach (var upload in expired) {
            cancellationToken.ThrowIfCancellationRequested();
            await _productImageUploadRepository.MarkExpiredAsync(upload.Id, DateTimeOffset.UtcNow, cancellationToken);
        }
    }

    #endregion

    #region Cleanup

    public async Task ProcessCleanupAsync (CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var oldReady = await _productImageUploadRepository.GetUnreferencedReadyUploadsAsync(now, BatchSize, cancellationToken);
        foreach (var candidate in oldReady) {
            cancellationToken.ThrowIfCancellationRequested();
            var claimedAt = DateTimeOffset.UtcNow;
            if (!await _productImageUploadRepository.TryClaimOrphanedReadyUploadAsync(
                    candidate.Id,
                    claimedAt,
                    now - ProductImageUploadDefaults.ProcessingLease,
                    cancellationToken))
                continue;

            var upload = await _productImageUploadRepository.GetUploadByIdAsync(candidate.Id, true, cancellationToken);
            if (upload is null)
                continue;

            await _productRepository.BeginTransactionAsync(cancellationToken);
            try {
                var product = await _productRepository.LockProductAsync(upload.ProductId, cancellationToken);
                if (product is not null && upload.Status == ProductImageUploadStatus.Ready &&
                    upload.NextAttemptAt is { } cleanupEligibleAt && cleanupEligibleAt <= now &&
                    !await _productImageRepository.HasAssetReferenceAsync(upload.AssetKey!, cancellationToken)) {
                    upload.Status = ProductImageUploadStatus.Cleaned;
                    upload.ProcessingStartedAt = null;
                    upload.NextAttemptAt = null;
                    await _productImageUploadRepository.UpdateAsync(upload, CancellationToken.None);
                }
                else {
                    upload.ProcessingStartedAt = null;
                    await _productImageUploadRepository.UpdateAsync(upload, CancellationToken.None);
                }

                // Finish the transaction once the cleanup decision has been made.
                await _productRepository.EndTransactionAsync(CancellationToken.None);
            }
            catch {
                await _productRepository.RollbackTransactionAsync(CancellationToken.None);
                throw;
            }
        }

        var cleanupUploads = await _productImageUploadRepository.GetCleanupUploadsAsync(BatchSize, cancellationToken);
        foreach (var upload in cleanupUploads) {
            cancellationToken.ThrowIfCancellationRequested();
            var claimedAt = DateTimeOffset.UtcNow;
            if (!await _productImageUploadRepository.TryClaimCleanupUploadAsync(
                    upload.Id,
                    claimedAt,
                    claimedAt - ProductImageUploadDefaults.ProcessingLease,
                    cancellationToken))
                continue;

            upload.ProcessingStartedAt = claimedAt;
            try {
                await CleanupUploadAsync(upload, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                await _productImageUploadRepository.ScheduleCleanupRetryAsync(
                    upload.Id,
                    DateTimeOffset.UtcNow.Add(Backoff(upload.CleanupAttempts + 1)),
                    CancellationToken.None);
                throw;
            }
            catch (Exception exception) when (exception is ObjectStorageException or IOException) {
                await _productImageUploadRepository.ScheduleCleanupRetryAsync(
                    upload.Id,
                    DateTimeOffset.UtcNow.Add(Backoff(upload.CleanupAttempts + 1)),
                    CancellationToken.None);
                _logger.LogWarning(exception, "Storage cleanup will retry for upload {UploadId}.", upload.Id);
            }
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    #region Private

    private async Task CleanupUploadAsync (ProductImageUploads candidate, CancellationToken cancellationToken)
    {
        var upload = await _productImageUploadRepository.GetUploadByIdAsync(candidate.Id, true, cancellationToken);
        if (upload is null)
            return;

        if (upload.Status is ProductImageUploadStatus.Rejected or ProductImageUploadStatus.Expired or ProductImageUploadStatus.Cleaned &&
            upload.NextAttemptAt is { } cleanupNotBefore && cleanupNotBefore > DateTimeOffset.UtcNow) {
            upload.ProcessingStartedAt = null;
            await _productImageUploadRepository.UpdateAsync(upload, CancellationToken.None);
            return;
        }

        if (upload.Status == ProductImageUploadStatus.Ready &&
            upload.NextAttemptAt is { } retentionNotBefore && retentionNotBefore > DateTimeOffset.UtcNow) {
            if (upload.StagingDeletedAt is null) {
                if (await _objectStorage.ExistsAsync(_productMediaOptions.StagingBucket, upload.StagingKey, cancellationToken))
                    await _objectStorage.DeleteAsync(_productMediaOptions.StagingBucket, upload.StagingKey, cancellationToken);
                upload.StagingDeletedAt = DateTimeOffset.UtcNow;
                await _productImageUploadRepository.UpdateAsync(upload, CancellationToken.None);
            }
            else {
                upload.ProcessingStartedAt = null;
                await _productImageUploadRepository.UpdateAsync(upload, CancellationToken.None);
            }
            return;
        }

        if (upload.Status is ProductImageUploadStatus.Pending or ProductImageUploadStatus.Ready) {
            if (upload.StagingDeletedAt is null) {
                if (await _objectStorage.ExistsAsync(_productMediaOptions.StagingBucket, upload.StagingKey, cancellationToken))
                    await _objectStorage.DeleteAsync(_productMediaOptions.StagingBucket, upload.StagingKey, cancellationToken);

                upload.StagingDeletedAt = DateTimeOffset.UtcNow;
                upload.ProcessingStartedAt = null;
                upload.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(_productMediaOptions.CleanupRetentionMinutes);
                await _productImageUploadRepository.UpdateAsync(upload, CancellationToken.None);
            }
            return;
        }

        if (upload.Status is not (ProductImageUploadStatus.Rejected or ProductImageUploadStatus.Expired or ProductImageUploadStatus.Cleaned))
            return;

        if (upload.StagingDeletedAt is null) {
            if (await _objectStorage.ExistsAsync(_productMediaOptions.StagingBucket, upload.StagingKey, cancellationToken))
                await _objectStorage.DeleteAsync(_productMediaOptions.StagingBucket, upload.StagingKey, cancellationToken);
            upload.StagingDeletedAt = DateTimeOffset.UtcNow;
        }

        if (upload.AssetKey is not null) {
            if (await _productImageRepository.HasAssetReferenceAsync(upload.AssetKey, cancellationToken))
                return;

            await _objectStorage.DeleteAsync(_productMediaOptions.MediaBucket, upload.AssetKey, cancellationToken);
            upload.AssetKey = null;
        }

        upload.CleanupAttempts++;
        upload.ProcessingStartedAt = null;
        upload.NextAttemptAt = null;
        await _productImageUploadRepository.UpdateAsync(upload, CancellationToken.None);
    }

    #endregion

    #endregion

    #region Shared

    #region Private

    private async Task RejectAsync (ProductImageUploads upload, string reasonCode, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Rejecting image upload {UploadId} with reason {ReasonCode}.", upload.Id, reasonCode);
        upload.Status = ProductImageUploadStatus.Rejected;
        upload.ProcessingStartedAt = null;
        upload.NextAttemptAt = DateTimeOffset.UtcNow;
        await _productImageUploadRepository.UpdateAsync(upload, CancellationToken.None);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task ReleaseForRetryAsync (long uploadId, Exception? exception, CancellationToken cancellationToken)
    {
        var upload = await _productImageUploadRepository.GetUploadByIdAsync(uploadId, true, CancellationToken.None);
        if (upload is null || upload.Status != ProductImageUploadStatus.Pending)
            return;

        upload.ProcessingStartedAt = null;
        upload.NextAttemptAt = DateTimeOffset.UtcNow.Add(Backoff(upload.AttemptCount));
        await _productImageUploadRepository.UpdateAsync(upload, CancellationToken.None);
        if (exception is null)
            _logger.LogDebug("Staging object for upload {UploadId} is not available yet; the upload remains pending.", uploadId);
        else
            _logger.LogWarning(exception, "Image upload {UploadId} will retry after a storage or I/O failure.", uploadId);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private bool IsAllowedContentType (string? value) =>
        value is not null && _productMediaOptions.AllowedContentTypes.Contains(value, StringComparer.OrdinalIgnoreCase);

    private static TimeSpan Backoff (int attempts) =>
        TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, Math.Clamp(attempts, 1, 8))));

    #endregion

    #endregion
}
