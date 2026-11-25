using System.Net;
using System.Net.Http;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using ObjectStorage.Abstractions.Exceptions;
using ObjectStorage.Abstractions.Interfaces;
using ObjectStorage.Abstractions.Models;

namespace ObjectStorage.S3.Services;

public sealed class S3ObjectStorage : IObjectStorage
{
    private const string MetadataPrefix = "x-amz-meta-";
    private static readonly TimeSpan MaximumPresignedUrlLifetime = TimeSpan.FromDays(7);
    private readonly IAmazonS3 _s3Client;
    private readonly IAmazonS3 _presignedS3Client;

    public S3ObjectStorage(IAmazonS3 s3Client)
        : this(s3Client, s3Client)
    {
    }

    public S3ObjectStorage(IAmazonS3 s3Client, IAmazonS3 presignedS3Client)
    {
        ArgumentNullException.ThrowIfNull(s3Client);
        ArgumentNullException.ThrowIfNull(presignedS3Client);
        _s3Client = s3Client;
        _presignedS3Client = presignedS3Client;
    }

    public async Task<ObjectDescriptor> UploadAsync(
        ObjectUploadRequest request,
        Stream content,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(content);
        ValidateLocation(request.BucketName, request.ObjectKey);

        if (!content.CanRead)
        {
            throw new ArgumentException("The upload stream must be readable.", nameof(content));
        }

        if (contentLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(contentLength), "Content length cannot be negative.");
        }

        if (request.Metadata is null)
        {
            throw new ArgumentException("Metadata cannot be null.", nameof(request));
        }

        var putRequest = new PutObjectRequest
        {
            BucketName = request.BucketName,
            Key = request.ObjectKey,
            InputStream = content,
            ContentType = request.ContentType,
            AutoCloseStream = false,
            AutoResetStreamPosition = false
        };
        putRequest.Headers.ContentLength = contentLength;

        if (content.CanSeek && content.Length - content.Position != contentLength)
        {
            throw new ArgumentException(
                "Content length must match the upload stream's remaining length.",
                nameof(contentLength));
        }

        foreach (var (key, value) in request.Metadata)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Metadata keys cannot be empty.", nameof(request));
            }

            var metadataKey = key.StartsWith(MetadataPrefix, StringComparison.OrdinalIgnoreCase)
                ? key
                : MetadataPrefix + key;
            putRequest.Metadata.Add(metadataKey, value);
        }

        var response = await ExecuteAsync(
            "Upload",
            request.BucketName,
            request.ObjectKey,
            () => _s3Client.PutObjectAsync(putRequest, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        return new ObjectDescriptor
        {
            BucketName = request.BucketName,
            ObjectKey = request.ObjectKey,
            ETag = response.ETag,
            ContentLength = contentLength,
            ContentType = request.ContentType
        };
    }

    public Task DownloadAsync(
        string bucketName,
        string objectKey,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ValidateLocation(bucketName, objectKey);

        if (!destination.CanWrite)
        {
            throw new ArgumentException("The destination stream must be writable.", nameof(destination));
        }

        return ExecuteAsync(
            "Download",
            bucketName,
            objectKey,
            async () =>
            {
                using var response = await _s3Client.GetObjectAsync(
                    new GetObjectRequest { BucketName = bucketName, Key = objectKey },
                    cancellationToken).ConfigureAwait(false);

                await response.ResponseStream.CopyToAsync(destination, cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken);
    }

    public async Task<ObjectMetadata?> GetMetadataAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateLocation(bucketName, objectKey);

        try
        {
            var response = await ExecuteAsync(
                "GetMetadata",
                bucketName,
                objectKey,
                () => _s3Client.GetObjectMetadataAsync(
                    new GetObjectMetadataRequest { BucketName = bucketName, Key = objectKey },
                    cancellationToken),
                cancellationToken).ConfigureAwait(false);

            return new ObjectMetadata
            {
                Key = objectKey,
                ContentType = response.Headers.ContentType,
                ContentLength = response.ContentLength,
                ETag = response.ETag,
                LastModified = ToDateTimeOffset(response.LastModified),
                CustomMetadata = ReadCustomMetadata(response.Metadata)
            };
        }
        catch (ObjectStorageException exception) when (IsObjectNotFound(exception.InnerException))
        {
            return null;
        }
    }

    public async Task<bool> ExistsAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateLocation(bucketName, objectKey);

        try
        {
            await ExecuteAsync(
                "Exists",
                bucketName,
                objectKey,
                () => _s3Client.GetObjectMetadataAsync(
                    new GetObjectMetadataRequest { BucketName = bucketName, Key = objectKey },
                    cancellationToken),
                cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (ObjectStorageException exception) when (IsObjectNotFound(exception.InnerException))
        {
            return false;
        }
    }

    public Task DeleteAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateLocation(bucketName, objectKey);

        return ExecuteAsync(
            "Delete",
            bucketName,
            objectKey,
            () => _s3Client.DeleteObjectAsync(
                new DeleteObjectRequest { BucketName = bucketName, Key = objectKey },
                cancellationToken),
            cancellationToken);
    }

    public Task CopyAsync(
        string sourceBucketName,
        string sourceObjectKey,
        string destinationBucketName,
        string destinationObjectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateLocation(sourceBucketName, sourceObjectKey);
        ValidateLocation(destinationBucketName, destinationObjectKey);

        return ExecuteAsync(
            "Copy",
            destinationBucketName,
            destinationObjectKey,
            () => _s3Client.CopyObjectAsync(
                new CopyObjectRequest
                {
                    SourceBucket = sourceBucketName,
                    SourceKey = sourceObjectKey,
                    DestinationBucket = destinationBucketName,
                    DestinationKey = destinationObjectKey
                },
                cancellationToken),
            cancellationToken);
    }

    public Task<PresignedUrlResult> CreatePresignedUploadUrlAsync(
        string bucketName,
        string objectKey,
        TimeSpan expiresIn,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateLocation(bucketName, objectKey);
        ValidateExpiration(expiresIn);

        var expiresAt = DateTimeOffset.UtcNow.Add(expiresIn);
        var requiredHeaders = contentType is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Content-Type"] = contentType
            };

        var url = _presignedS3Client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = expiresAt.UtcDateTime
        });

        return Task.FromResult(new PresignedUrlResult
        {
            Url = url,
            ExpiresAt = expiresAt,
            RequiredHeaders = requiredHeaders
        });
    }

    public Task<PresignedUrlResult> CreatePresignedDownloadUrlAsync(
        string bucketName,
        string objectKey,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateLocation(bucketName, objectKey);
        ValidateExpiration(expiresIn);

        var expiresAt = DateTimeOffset.UtcNow.Add(expiresIn);
        var url = _presignedS3Client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = expiresAt.UtcDateTime
        });

        return Task.FromResult(new PresignedUrlResult
        {
            Url = url,
            ExpiresAt = expiresAt
        });
    }

    private static void ValidateLocation(string bucketName, string objectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
    }

    private static void ValidateExpiration(TimeSpan expiresIn)
    {
        if (expiresIn <= TimeSpan.Zero || expiresIn > MaximumPresignedUrlLifetime)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiresIn),
                $"Expiration must be greater than zero and no more than {MaximumPresignedUrlLifetime.TotalDays} days.");
        }
    }

    private static bool IsObjectNotFound(Exception? exception)
    {
        if (exception is not AmazonS3Exception s3Exception
            || string.Equals(s3Exception.ErrorCode, "NoSuchBucket", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(s3Exception.ErrorCode))
        {
            return string.Equals(s3Exception.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s3Exception.ErrorCode, "NotFound", StringComparison.OrdinalIgnoreCase);
        }

        return s3Exception.StatusCode == HttpStatusCode.NotFound;
    }

    private static IReadOnlyDictionary<string, string> ReadCustomMetadata(MetadataCollection metadata)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in metadata.Keys)
        {
            var normalizedKey = key.StartsWith(MetadataPrefix, StringComparison.OrdinalIgnoreCase)
                ? key[MetadataPrefix.Length..]
                : key;
            result[normalizedKey] = metadata[key];
        }

        return result;
    }

    private static DateTimeOffset? ToDateTimeOffset(DateTime? value)
    {
        return value is null || value.Value == default
            ? null
            : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
    }

    private static async Task ExecuteAsync(
        string operation,
        string bucketName,
        string objectKey,
        Func<Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (AmazonServiceException exception)
        {
            throw CreateStorageException(operation, bucketName, objectKey, exception);
        }
        catch (HttpRequestException exception)
        {
            throw CreateStorageException(operation, bucketName, objectKey, exception);
        }
        catch (TimeoutException exception)
        {
            throw CreateStorageException(operation, bucketName, objectKey, exception);
        }
        catch (OperationCanceledException exception)
        {
            throw CreateStorageException(operation, bucketName, objectKey, exception);
        }
    }

    private static async Task<T> ExecuteAsync<T>(
        string operation,
        string bucketName,
        string objectKey,
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (AmazonServiceException exception)
        {
            throw CreateStorageException(operation, bucketName, objectKey, exception);
        }
        catch (HttpRequestException exception)
        {
            throw CreateStorageException(operation, bucketName, objectKey, exception);
        }
        catch (TimeoutException exception)
        {
            throw CreateStorageException(operation, bucketName, objectKey, exception);
        }
        catch (OperationCanceledException exception)
        {
            throw CreateStorageException(operation, bucketName, objectKey, exception);
        }
    }

    private static ObjectStorageException CreateStorageException(
        string operation,
        string bucketName,
        string objectKey,
        Exception exception)
    {
        return new ObjectStorageException(
            operation,
            bucketName,
            objectKey,
            $"Object storage operation '{operation}' failed for bucket '{bucketName}' and key '{objectKey}'.",
            exception);
    }
}
