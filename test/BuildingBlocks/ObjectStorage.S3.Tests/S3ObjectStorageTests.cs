using System.Net;
using System.Net.Http;
using Amazon.S3;
using Amazon.S3.Model;
using Moq;
using ObjectStorage.Abstractions.Exceptions;
using ObjectStorage.Abstractions.Models;
using ObjectStorage.S3.Services;
using Xunit;

namespace ObjectStorage.S3.Tests;

public sealed class S3ObjectStorageTests
{
    private readonly Mock<IAmazonS3> _s3Client = new(MockBehavior.Strict);

    [Fact]
    public async Task UploadAsync_returns_descriptor_and_keeps_caller_stream_open()
    {
        var content = new TrackingMemoryStream([1, 2, 3]);
        PutObjectRequest? observedRequest = null;
        _s3Client
            .Setup(client => client.PutObjectAsync(
                It.IsAny<PutObjectRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((request, _) => observedRequest = request)
            .ReturnsAsync(new PutObjectResponse { ETag = "\"etag\"" });

        var descriptor = await CreateStorage().UploadAsync(
            new ObjectUploadRequest
            {
                BucketName = "catalog",
                ObjectKey = "images/item.png",
                ContentType = "image/png",
                Metadata = new Dictionary<string, string> { ["source"] = "test" }
            },
            content,
            3,
            TestContext.Current.CancellationToken);

        Assert.NotNull(observedRequest);
        Assert.Equal("catalog", observedRequest.BucketName);
        Assert.Equal("images/item.png", observedRequest.Key);
        Assert.Equal(3, observedRequest.Headers.ContentLength);
        Assert.Equal("image/png", observedRequest.ContentType);
        Assert.False(observedRequest.AutoCloseStream);
        Assert.False(observedRequest.AutoResetStreamPosition);
        Assert.Equal("test", observedRequest.Metadata["x-amz-meta-source"]);
        Assert.False(content.IsDisposed);
        Assert.Equal("catalog", descriptor.BucketName);
        Assert.Equal("images/item.png", descriptor.ObjectKey);
        Assert.Equal("\"etag\"", descriptor.ETag);
        Assert.Equal(3, descriptor.ContentLength);
    }

    [Fact]
    public async Task UploadAsync_rejects_a_length_that_does_not_match_a_seekable_stream()
    {
        var content = new TrackingMemoryStream([1, 2, 3]) { Position = 1 };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateStorage().UploadAsync(
                new ObjectUploadRequest { BucketName = "catalog", ObjectKey = "key" },
                content,
                3,
                TestContext.Current.CancellationToken));

        Assert.False(content.IsDisposed);
        _s3Client.Verify(
            client => client.PutObjectAsync(
                It.IsAny<PutObjectRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DownloadAsync_copies_content_and_disposes_only_the_response_stream()
    {
        var source = new TrackingMemoryStream([4, 5, 6]);
        var destination = new TrackingMemoryStream();
        _s3Client
            .Setup(client => client.GetObjectAsync(
                It.IsAny<GetObjectRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectResponse { ResponseStream = source });

        await CreateStorage().DownloadAsync(
            "catalog",
            "file.bin",
            destination,
            TestContext.Current.CancellationToken);

        Assert.Equal([4, 5, 6], destination.ToArray());
        Assert.True(source.IsDisposed);
        Assert.False(destination.IsDisposed);
    }

    [Fact]
    public async Task GetMetadataAsync_maps_headers_and_custom_metadata()
    {
        var response = new GetObjectMetadataResponse
        {
            ContentLength = 12,
            ETag = "etag",
            LastModified = DateTime.UtcNow
        };
        response.Headers.ContentType = "text/plain";
        response.Metadata.Add("x-amz-meta-color", "blue");
        _s3Client
            .Setup(client => client.GetObjectMetadataAsync(
                It.IsAny<GetObjectMetadataRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var metadata = await CreateStorage().GetMetadataAsync(
            "catalog",
            "notes.txt",
            TestContext.Current.CancellationToken);

        Assert.NotNull(metadata);
        Assert.Equal("notes.txt", metadata.Key);
        Assert.Equal("text/plain", metadata.ContentType);
        Assert.Equal(12, metadata.ContentLength);
        Assert.Equal("etag", metadata.ETag);
        Assert.Equal("blue", metadata.CustomMetadata["color"]);
        Assert.NotNull(metadata.LastModified);
    }

    [Fact]
    public async Task GetMetadataAsync_returns_null_for_a_missing_object()
    {
        _s3Client
            .Setup(client => client.GetObjectMetadataAsync(
                It.IsAny<GetObjectMetadataRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Missing object")
            {
                StatusCode = HttpStatusCode.NotFound,
                ErrorCode = "NoSuchKey"
            });

        Assert.Null(await CreateStorage().GetMetadataAsync(
            "catalog",
            "missing",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExistsAsync_returns_false_only_for_a_missing_object()
    {
        _s3Client
            .Setup(client => client.GetObjectMetadataAsync(
                It.IsAny<GetObjectMetadataRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Missing object")
            {
                StatusCode = HttpStatusCode.NotFound,
                ErrorCode = "NoSuchKey"
            });

        Assert.False(await CreateStorage().ExistsAsync(
            "catalog",
            "missing",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExistsAsync_wraps_access_denied_instead_of_returning_false()
    {
        var denied = new AmazonS3Exception("Forbidden")
        {
            StatusCode = HttpStatusCode.Forbidden,
            ErrorCode = "AccessDenied"
        };
        _s3Client
            .Setup(client => client.GetObjectMetadataAsync(
                It.IsAny<GetObjectMetadataRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(denied);

        var exception = await Assert.ThrowsAsync<ObjectStorageException>(
            () => CreateStorage().ExistsAsync(
                "catalog",
                "restricted",
                TestContext.Current.CancellationToken));

        Assert.Same(denied, exception.InnerException);
        Assert.Equal("Exists", exception.Operation);
    }

    [Fact]
    public async Task ExistsAsync_does_not_treat_an_access_denied_error_code_as_not_found()
    {
        var denied = new AmazonS3Exception("Forbidden")
        {
            StatusCode = HttpStatusCode.NotFound,
            ErrorCode = "AccessDenied"
        };
        _s3Client
            .Setup(client => client.GetObjectMetadataAsync(
                It.IsAny<GetObjectMetadataRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(denied);

        var exception = await Assert.ThrowsAsync<ObjectStorageException>(
            () => CreateStorage().ExistsAsync(
                "catalog",
                "restricted",
                TestContext.Current.CancellationToken));

        Assert.Same(denied, exception.InnerException);
    }

    [Fact]
    public async Task GetMetadataAsync_does_not_treat_a_missing_bucket_as_a_missing_object()
    {
        var missingBucket = new AmazonS3Exception("Missing bucket")
        {
            StatusCode = HttpStatusCode.NotFound,
            ErrorCode = "NoSuchBucket"
        };
        _s3Client
            .Setup(client => client.GetObjectMetadataAsync(
                It.IsAny<GetObjectMetadataRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(missingBucket);

        var exception = await Assert.ThrowsAsync<ObjectStorageException>(
            () => CreateStorage().GetMetadataAsync(
                "missing-bucket",
                "key",
                TestContext.Current.CancellationToken));

        Assert.Same(missingBucket, exception.InnerException);
    }

    [Fact]
    public async Task Network_errors_are_wrapped_without_losing_the_inner_exception()
    {
        var networkError = new HttpRequestException("Connection failed");
        _s3Client
            .Setup(client => client.DeleteObjectAsync(
                It.IsAny<DeleteObjectRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(networkError);

        var exception = await Assert.ThrowsAsync<ObjectStorageException>(
            () => CreateStorage().DeleteAsync(
                "catalog",
                "key",
                TestContext.Current.CancellationToken));

        Assert.Same(networkError, exception.InnerException);
        Assert.Equal("Delete", exception.Operation);
    }

    [Fact]
    public async Task Cancellation_is_not_wrapped()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        _s3Client
            .Setup(client => client.PutObjectAsync(
                It.IsAny<PutObjectRequest>(),
                cancellationSource.Token))
            .Returns(Task.FromCanceled<PutObjectResponse>(cancellationSource.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateStorage().UploadAsync(
                new ObjectUploadRequest { BucketName = "catalog", ObjectKey = "key" },
                new MemoryStream([1]),
                1,
                cancellationSource.Token));
    }

    [Fact]
    public async Task CopyAsync_preserves_source_and_destination_keys()
    {
        CopyObjectRequest? observedRequest = null;
        _s3Client
            .Setup(client => client.CopyObjectAsync(
                It.IsAny<CopyObjectRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<CopyObjectRequest, CancellationToken>((request, _) => observedRequest = request)
            .ReturnsAsync(new CopyObjectResponse());

        await CreateStorage().CopyAsync(
            "source",
            "a +% #.txt",
            "destination",
            "copied.txt",
            TestContext.Current.CancellationToken);

        Assert.NotNull(observedRequest);
        Assert.Equal("source", observedRequest.SourceBucket);
        Assert.Equal("a +% #.txt", observedRequest.SourceKey);
        Assert.Equal("destination", observedRequest.DestinationBucket);
        Assert.Equal("copied.txt", observedRequest.DestinationKey);
    }

    [Fact]
    public async Task DeleteAsync_sends_the_requested_bucket_and_key()
    {
        DeleteObjectRequest? observedRequest = null;
        _s3Client
            .Setup(client => client.DeleteObjectAsync(
                It.IsAny<DeleteObjectRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<DeleteObjectRequest, CancellationToken>((request, _) => observedRequest = request)
            .ReturnsAsync(new DeleteObjectResponse());

        await CreateStorage().DeleteAsync(
            "catalog",
            "image.png",
            TestContext.Current.CancellationToken);

        Assert.NotNull(observedRequest);
        Assert.Equal("catalog", observedRequest.BucketName);
        Assert.Equal("image.png", observedRequest.Key);
    }

    [Fact]
    public async Task Presigned_upload_includes_signed_content_type_header()
    {
        GetPreSignedUrlRequest? observedRequest = null;
        _s3Client
            .Setup(client => client.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
            .Callback<GetPreSignedUrlRequest>(request => observedRequest = request)
            .Returns("http://garage:3900/catalog/image.png?signature=generated");

        var result = await CreateStorage().CreatePresignedUploadUrlAsync(
            "catalog",
            "image.png",
            TimeSpan.FromMinutes(5),
            "image/png",
            TestContext.Current.CancellationToken);

        Assert.NotNull(observedRequest);
        Assert.Equal(HttpVerb.PUT, observedRequest.Verb);
        Assert.Equal("image/png", observedRequest.ContentType);
        Assert.Equal("image/png", result.RequiredHeaders["Content-Type"]);
        Assert.Equal("http://garage:3900/catalog/image.png?signature=generated", result.Url);
        Assert.InRange(result.ExpiresAt, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(6));
    }

    [Fact]
    public async Task Presigned_download_uses_get_and_rejects_expirations_over_seven_days()
    {
        GetPreSignedUrlRequest? observedRequest = null;
        _s3Client
            .Setup(client => client.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
            .Callback<GetPreSignedUrlRequest>(request => observedRequest = request)
            .Returns("http://garage:3900/catalog/image.png?signature=generated");
        var storage = CreateStorage();

        var result = await storage.CreatePresignedDownloadUrlAsync(
            "catalog",
            "image.png",
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        Assert.NotNull(observedRequest);
        Assert.Equal(HttpVerb.GET, observedRequest.Verb);
        Assert.Empty(result.RequiredHeaders);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            storage.CreatePresignedDownloadUrlAsync(
                "catalog",
                "image.png",
                TimeSpan.FromDays(8),
                TestContext.Current.CancellationToken));
    }

    private S3ObjectStorage CreateStorage() => new(_s3Client.Object);

    private sealed class TrackingMemoryStream : MemoryStream
    {
        public TrackingMemoryStream()
        {
        }

        public TrackingMemoryStream(byte[] buffer)
            : base(buffer)
        {
        }

        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
