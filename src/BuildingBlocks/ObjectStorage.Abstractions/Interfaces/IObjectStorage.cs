using ObjectStorage.Abstractions.Models;

namespace ObjectStorage.Abstractions.Interfaces;

public interface IObjectStorage
{
    Task<ObjectDescriptor> UploadAsync(
        ObjectUploadRequest request,
        Stream content,
        long contentLength,
        CancellationToken cancellationToken = default);

    Task DownloadAsync(
        string bucketName,
        string objectKey,
        Stream destination,
        CancellationToken cancellationToken = default);

    Task<ObjectMetadata?> GetMetadataAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default);

    Task CopyAsync(
        string sourceBucketName,
        string sourceObjectKey,
        string destinationBucketName,
        string destinationObjectKey,
        CancellationToken cancellationToken = default);

    Task<PresignedUrlResult> CreatePresignedUploadUrlAsync(
        string bucketName,
        string objectKey,
        TimeSpan expiresIn,
        string? contentType = null,
        CancellationToken cancellationToken = default);

    Task<PresignedUrlResult> CreatePresignedDownloadUrlAsync(
        string bucketName,
        string objectKey,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default);
}
