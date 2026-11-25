using Contracts.Domains.Interfaces;
using Product.API.Commons.Enums;
using Product.API.Entities;
using Product.API.Persistence;

namespace Product.API.Repositories.Interfaces;

public interface IProductImageUploadRepository : IRepositoryBase<ProductImageUploads, long, ProductContext>
{
    Task<IReadOnlyList<ProductImageUploads>> GetPendingUploadsAsync(
        DateTimeOffset now,
        DateTimeOffset staleClaimBefore,
        int batchSize,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductImageUploads>> GetExpiredUploadsAsync(int batchSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductImageUploads>> GetCleanupUploadsAsync(int batchSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductImageUploads>> GetUnreferencedReadyUploadsAsync(
        DateTimeOffset olderThan,
        int batchSize,
        CancellationToken cancellationToken);
    Task<ProductImageUploads?> GetProductImageUploadAsync(long productId, long uploadId, CancellationToken cancellationToken);
    Task<ProductImageUploads?> GetUploadByIdAsync(long uploadId, bool trackChanges, CancellationToken cancellationToken);
    Task<bool> MarkAssetUnreferencedForCleanupAsync(
        string assetKey,
        DateTimeOffset deleteAfter,
        CancellationToken cancellationToken);
    Task<int> CountActiveUploadSlotsAsync(long productId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<bool> TryClaimPendingUploadAsync(long uploadId, DateTimeOffset now, DateTimeOffset staleClaimBefore, CancellationToken cancellationToken);
    Task<bool> TryClaimOrphanedReadyUploadAsync(
        long uploadId,
        DateTimeOffset claimedAt,
        DateTimeOffset staleClaimBefore,
        CancellationToken cancellationToken);
    Task<bool> TryClaimCleanupUploadAsync(
        long uploadId,
        DateTimeOffset claimedAt,
        DateTimeOffset staleClaimBefore,
        CancellationToken cancellationToken);
    Task<bool> MarkExpiredAsync(long uploadId, DateTimeOffset now, CancellationToken cancellationToken);
    Task ScheduleCleanupRetryAsync(long uploadId, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken);
}
