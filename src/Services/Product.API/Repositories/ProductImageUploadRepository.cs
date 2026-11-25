using Contracts.Domains.Interfaces;
using Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using Product.API.Commons;
using Product.API.Commons.Enums;
using Product.API.Entities;
using Product.API.Persistence;
using Product.API.Repositories.Interfaces;

namespace Product.API.Repositories;

public sealed class ProductImageUploadRepository : RepositoryBase<ProductImageUploads, long, ProductContext>, IProductImageUploadRepository
{
    private readonly ProductContext _dbContext;

    public ProductImageUploadRepository(ProductContext dbContext, IUnitOfWork<ProductContext> unitOfWork)
        : base(dbContext, unitOfWork) => _dbContext = dbContext;

    public async Task<IReadOnlyList<ProductImageUploads>> GetPendingUploadsAsync(
        DateTimeOffset now,
        DateTimeOffset staleClaimBefore,
        int batchSize,
        CancellationToken cancellationToken)
    {
        return await FindByCondition(upload =>
                upload.Status == ProductImageUploadStatus.Pending &&
                upload.ExpiresAt > now &&
                (upload.NextAttemptAt == null || upload.NextAttemptAt <= now) &&
                (upload.ProcessingStartedAt == null || upload.ProcessingStartedAt < staleClaimBefore))
            .OrderBy(upload => upload.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductImageUploads>> GetExpiredUploadsAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var staleClaimBefore = now - ProductImageUploadDefaults.ProcessingLease;
        return await FindByCondition(upload =>
                upload.Status == ProductImageUploadStatus.Pending &&
                upload.ExpiresAt <= now &&
                (upload.ProcessingStartedAt == null || upload.ProcessingStartedAt < staleClaimBefore))
            .OrderBy(upload => upload.ExpiresAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductImageUploads>> GetCleanupUploadsAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return await FindByCondition(upload =>
                ((upload.Status == ProductImageUploadStatus.Ready && upload.StagingDeletedAt == null &&
                  (upload.NextAttemptAt == null || upload.NextAttemptAt <= now)) ||
                ((upload.Status == ProductImageUploadStatus.Rejected ||
                  upload.Status == ProductImageUploadStatus.Expired ||
                  upload.Status == ProductImageUploadStatus.Cleaned) &&
                 (upload.StagingDeletedAt == null || upload.AssetKey != null) &&
                 (upload.NextAttemptAt == null || upload.NextAttemptAt <= now)) ||
                (upload.Status == ProductImageUploadStatus.Ready &&
                 upload.StagingDeletedAt != null &&
                 upload.AssetKey != null &&
                 upload.NextAttemptAt != null && upload.NextAttemptAt <= now)) &&
                (upload.ProcessingStartedAt == null || upload.ProcessingStartedAt < now - ProductImageUploadDefaults.ProcessingLease))
            .OrderBy(upload => upload.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductImageUploads>> GetUnreferencedReadyUploadsAsync(
        DateTimeOffset olderThan,
        int batchSize,
        CancellationToken cancellationToken)
    {
        return await FindByCondition(upload =>
                upload.Status == ProductImageUploadStatus.Ready &&
                upload.StagingDeletedAt != null &&
                upload.AssetKey != null &&
                !_dbContext.ProductImages.Any(image => image.AssetKey == upload.AssetKey) &&
                upload.NextAttemptAt != null && upload.NextAttemptAt <= olderThan &&
                (upload.ProcessingStartedAt == null || upload.ProcessingStartedAt < olderThan - ProductImageUploadDefaults.ProcessingLease))
            .OrderBy(upload => upload.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public Task<ProductImageUploads?> GetUploadByIdAsync(long uploadId, bool trackChanges, CancellationToken cancellationToken) =>
        FindByCondition(upload => upload.Id == uploadId, trackChanges).SingleOrDefaultAsync(cancellationToken);

    public Task<ProductImageUploads?> GetProductImageUploadAsync(
        long productId,
        long uploadId,
        CancellationToken cancellationToken) =>
        FindByCondition(upload => upload.Id == uploadId && upload.ProductId == productId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> MarkAssetUnreferencedForCleanupAsync(
        string assetKey,
        DateTimeOffset deleteAfter,
        CancellationToken cancellationToken)
    {
        var affected = await _dbContext.ProductImageUploads
            .Where(upload => upload.AssetKey == assetKey && upload.Status == ProductImageUploadStatus.Ready)
            .ExecuteUpdateAsync(update => update
                .SetProperty(upload => upload.Status, ProductImageUploadStatus.Expired)
                .SetProperty(upload => upload.NextAttemptAt, deleteAfter), cancellationToken);
        return affected == 1;
    }

    public Task<int> CountActiveUploadSlotsAsync(
        long productId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        FindByCondition(upload =>
                upload.ProductId == productId &&
                (upload.Status == ProductImageUploadStatus.Ready ||
                 (upload.Status == ProductImageUploadStatus.Pending && upload.ExpiresAt > now)) &&
                upload.AssetKey != null &&
                !_dbContext.ProductImages.Any(image => image.AssetKey == upload.AssetKey))
            .CountAsync(cancellationToken);

    public async Task<bool> TryClaimPendingUploadAsync(
        long uploadId,
        DateTimeOffset now,
        DateTimeOffset staleClaimBefore,
        CancellationToken cancellationToken)
    {
        var affected = await _dbContext.ProductImageUploads
            .Where(upload =>
                upload.Id == uploadId &&
                upload.Status == ProductImageUploadStatus.Pending &&
                upload.ExpiresAt > now &&
                (upload.NextAttemptAt == null || upload.NextAttemptAt <= now) &&
                (upload.ProcessingStartedAt == null || upload.ProcessingStartedAt < staleClaimBefore))
            .ExecuteUpdateAsync(update => update
                .SetProperty(upload => upload.ProcessingStartedAt, now)
                .SetProperty(upload => upload.AttemptCount, upload => upload.AttemptCount + 1), cancellationToken);
        return affected == 1;
    }

    public async Task<bool> TryClaimOrphanedReadyUploadAsync(
        long uploadId,
        DateTimeOffset claimedAt,
        DateTimeOffset staleClaimBefore,
        CancellationToken cancellationToken)
    {
        var affected = await _dbContext.ProductImageUploads
            .Where(upload => upload.Id == uploadId && upload.Status == ProductImageUploadStatus.Ready &&
                upload.AssetKey != null &&
                !_dbContext.ProductImages.Any(image => image.AssetKey == upload.AssetKey) &&
                upload.NextAttemptAt != null && upload.NextAttemptAt <= claimedAt &&
                (upload.ProcessingStartedAt == null || upload.ProcessingStartedAt < staleClaimBefore))
            .ExecuteUpdateAsync(update => update.SetProperty(upload => upload.ProcessingStartedAt, claimedAt), cancellationToken);
        return affected == 1;
    }

    public async Task<bool> TryClaimCleanupUploadAsync(
        long uploadId,
        DateTimeOffset claimedAt,
        DateTimeOffset staleClaimBefore,
        CancellationToken cancellationToken)
    {
        var affected = await _dbContext.ProductImageUploads
            .Where(upload => upload.Id == uploadId &&
                ((upload.Status == ProductImageUploadStatus.Ready && upload.StagingDeletedAt == null &&
                  (upload.NextAttemptAt == null || upload.NextAttemptAt <= claimedAt)) ||
                 ((upload.Status == ProductImageUploadStatus.Rejected ||
                 upload.Status == ProductImageUploadStatus.Expired ||
                 upload.Status == ProductImageUploadStatus.Cleaned) &&
                (upload.StagingDeletedAt == null || upload.AssetKey != null) &&
                (upload.NextAttemptAt == null || upload.NextAttemptAt <= claimedAt)) ||
                (upload.Status == ProductImageUploadStatus.Ready &&
                 upload.StagingDeletedAt != null && upload.AssetKey != null &&
                 upload.NextAttemptAt != null && upload.NextAttemptAt <= claimedAt)) &&
                (upload.ProcessingStartedAt == null || upload.ProcessingStartedAt < staleClaimBefore))
            .ExecuteUpdateAsync(update => update
                .SetProperty(upload => upload.ProcessingStartedAt, claimedAt)
                .SetProperty(upload => upload.CleanupAttempts, upload => upload.CleanupAttempts + 1), cancellationToken);
        return affected == 1;
    }

    public async Task<bool> MarkExpiredAsync(long uploadId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var staleClaimBefore = now - ProductImageUploadDefaults.ProcessingLease;
        var affected = await _dbContext.ProductImageUploads
            .Where(upload => upload.Id == uploadId &&
                upload.Status == ProductImageUploadStatus.Pending &&
                upload.ExpiresAt <= now &&
                (upload.ProcessingStartedAt == null || upload.ProcessingStartedAt < staleClaimBefore))
            .ExecuteUpdateAsync(update => update
                .SetProperty(upload => upload.Status, ProductImageUploadStatus.Expired)
                .SetProperty(upload => upload.ProcessingStartedAt, (DateTimeOffset?)null)
                .SetProperty(upload => upload.NextAttemptAt, (DateTimeOffset?)null), cancellationToken);
        return affected == 1;
    }

    public async Task ScheduleCleanupRetryAsync(long uploadId, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken)
    {
        await _dbContext.ProductImageUploads
            .Where(upload => upload.Id == uploadId)
            .ExecuteUpdateAsync(update => update
                .SetProperty(upload => upload.NextAttemptAt, nextAttemptAt)
                .SetProperty(upload => upload.ProcessingStartedAt, (DateTimeOffset?)null), cancellationToken);
    }
}
