using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ObjectStorage.Abstractions.Exceptions;
using ObjectStorage.Abstractions.Interfaces;
using ObjectStorage.Abstractions.Models;
using Product.API.Commons.Enums;
using Product.API.Entities;
using Product.API.Options;
using Product.API.Repositories.Interfaces;
using Product.API.Services.V1;
using SkiaSharp;

namespace Product.API.Tests;

public sealed class ProductImageUploadProcessingServiceTests
{
    private static readonly CancellationToken TestToken = CancellationToken.None;
    private static readonly byte[] OnePixelPng = CreateOnePixelPng();

    [Theory]
    [InlineData("download")]
    [InlineData("storage_failure")]
    [InlineData("invalid_metadata")]
    [InlineData("missing_metadata")]
    public async Task Cancellation_still_persists_rejection_or_retry_before_stopping(string scenario)
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var upload = CreateUpload(OnePixelPng.Length);
        upload.ProcessingStartedAt = DateTimeOffset.UtcNow;
        var repository = new Mock<IProductImageUploadRepository>(MockBehavior.Strict);
        repository.Setup(store => store.TryClaimPendingUploadAsync(upload.Id, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), token))
            .ReturnsAsync(true);
        repository.Setup(store => store.GetUploadByIdAsync(upload.Id, true, token)).ReturnsAsync(upload);
        repository.Setup(store => store.GetUploadByIdAsync(upload.Id, true, CancellationToken.None)).ReturnsAsync(upload);
        repository.Setup(store => store.UpdateAsync(upload, CancellationToken.None))
            .Callback(() => {
                Assert.True(token.IsCancellationRequested);
                Assert.Null(upload.ProcessingStartedAt);
                Assert.NotNull(upload.NextAttemptAt);
                Assert.Equal(scenario == "invalid_metadata" ? ProductImageUploadStatus.Rejected : ProductImageUploadStatus.Pending, upload.Status);
            })
            .Returns(Task.CompletedTask);
        var storage = new Mock<IObjectStorage>(MockBehavior.Strict);
        storage.Setup(store => store.GetMetadataAsync("product-staging", upload.StagingKey, token))
            .Returns(() => {
                if (scenario != "download")
                    cancellation.Cancel();
                if (scenario == "storage_failure")
                    return Task.FromException<ObjectMetadata?>(new ObjectStorageException("GetMetadata", "product-staging", upload.StagingKey, "Unavailable"));
                return Task.FromResult<ObjectMetadata?>(scenario == "missing_metadata" ? null : new ObjectMetadata
                {
                    Key = upload.StagingKey,
                    ContentType = "image/png",
                    ContentLength = scenario == "invalid_metadata" ? upload.ExpectedSize + 1 : upload.ExpectedSize
                });
            });
        if (scenario == "download")
            storage.Setup(store => store.DownloadAsync("product-staging", upload.StagingKey, It.IsAny<Stream>(), token))
                .Returns(() => {
                    cancellation.Cancel();
                    return Task.FromCanceled(token);
                });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService(repository, storage).ProcessUploadAsync(upload, token));

        repository.Verify(store => store.UpdateAsync(upload, CancellationToken.None), Times.Once);
        if (scenario != "invalid_metadata")
            repository.Verify(store => store.GetUploadByIdAsync(upload.Id, true, CancellationToken.None), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cleanup_rolls_back_with_an_independent_token_and_preserves_original_failure(bool cancelledOperation)
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var upload = CreateUpload(OnePixelPng.Length);
        upload.Status = ProductImageUploadStatus.Ready;
        var uploads = new Mock<IProductImageUploadRepository>(MockBehavior.Strict);
        uploads.Setup(store => store.GetUnreferencedReadyUploadsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<int>(), token))
            .ReturnsAsync([upload]);
        uploads.Setup(store => store.TryClaimOrphanedReadyUploadAsync(upload.Id, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), token))
            .ReturnsAsync(true);
        uploads.Setup(store => store.GetUploadByIdAsync(upload.Id, true, token)).ReturnsAsync(upload);
        var products = new Mock<IProductRepository>(MockBehavior.Strict);
        products.Setup(store => store.BeginTransactionAsync(token))
            .ReturnsAsync(Mock.Of<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>());
        Exception original = cancelledOperation ? new OperationCanceledException(token) : new IOException("Lock failed.");
        products.Setup(store => store.LockProductAsync(upload.ProductId, token))
            .Returns(() => {
                cancellation.Cancel();
                return Task.FromException<Products?>(original);
            });
        products.Setup(store => store.RollbackTransactionAsync(CancellationToken.None)).Returns(Task.CompletedTask);
        var service = new ProductImageUploadProcessingService(
            products.Object, Mock.Of<IProductImageRepository>(), uploads.Object, Mock.Of<IObjectStorage>(),
            Microsoft.Extensions.Options.Options.Create(new ProductMediaOptions()), NullLogger<ProductImageUploadProcessingService>.Instance);

        var exception = await Record.ExceptionAsync(() => service.ProcessCleanupAsync(token));

        Assert.Same(original, exception);
        products.Verify(store => store.RollbackTransactionAsync(CancellationToken.None), Times.Once);
        products.Verify(store => store.EndTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cleanup_schedules_retry_even_when_worker_token_is_cancelled(bool cancelledOperation)
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var upload = CreateUpload(OnePixelPng.Length);
        upload.Status = ProductImageUploadStatus.Ready;
        var uploads = new Mock<IProductImageUploadRepository>(MockBehavior.Strict);
        uploads.Setup(store => store.GetUnreferencedReadyUploadsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<int>(), token))
            .ReturnsAsync(Array.Empty<ProductImageUploads>());
        uploads.Setup(store => store.GetCleanupUploadsAsync(It.IsAny<int>(), token)).ReturnsAsync([upload]);
        uploads.Setup(store => store.TryClaimCleanupUploadAsync(upload.Id, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), token))
            .ReturnsAsync(true);
        uploads.Setup(store => store.GetUploadByIdAsync(upload.Id, true, token)).ReturnsAsync(upload);
        uploads.Setup(store => store.ScheduleCleanupRetryAsync(upload.Id, It.IsAny<DateTimeOffset>(), CancellationToken.None))
            .Callback(() => Assert.True(token.IsCancellationRequested)).Returns(Task.CompletedTask);
        var storage = new Mock<IObjectStorage>(MockBehavior.Strict);
        storage.Setup(store => store.ExistsAsync("product-staging", upload.StagingKey, token))
            .Returns(() => {
                cancellation.Cancel();
                return Task.FromException<bool>(cancelledOperation ? new OperationCanceledException(token) : new IOException("Storage failed."));
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService(uploads, storage).ProcessCleanupAsync(token));

        uploads.Verify(store => store.ScheduleCleanupRetryAsync(upload.Id, It.IsAny<DateTimeOffset>(), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Validated_image_bytes_are_written_to_media_and_session_becomes_ready()
    {
        var upload = CreateUpload(OnePixelPng.Length);
        var repository = new Mock<IProductImageUploadRepository>();
        repository.Setup(store => store.TryClaimPendingUploadAsync(upload.Id, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), TestToken))
            .ReturnsAsync(true);
        repository.Setup(store => store.GetUploadByIdAsync(upload.Id, true, TestToken)).ReturnsAsync(upload);
        repository.Setup(store => store.UpdateAsync(upload, CancellationToken.None)).Returns(Task.CompletedTask);

        var storage = new Mock<IObjectStorage>();
        storage.Setup(store => store.GetMetadataAsync("product-staging", upload.StagingKey, TestToken))
            .ReturnsAsync(new ObjectMetadata
            {
                Key = upload.StagingKey,
                ContentType = "image/png",
                ContentLength = OnePixelPng.Length
            });
        storage.Setup(store => store.DownloadAsync("product-staging", upload.StagingKey, It.IsAny<Stream>(), TestToken))
            .Returns((string _, string _, Stream destination, CancellationToken token) =>
                destination.WriteAsync(OnePixelPng.AsMemory(), token).AsTask());
        storage.SetupSequence(store => store.GetMetadataAsync("product-media", upload.AssetKey!, TestToken))
            .ReturnsAsync((ObjectMetadata?)null)
            .ReturnsAsync(new ObjectMetadata
            {
                Key = upload.AssetKey!,
                ContentType = "image/png",
                ContentLength = OnePixelPng.Length,
                CustomMetadata = new Dictionary<string, string>
                {
                    ["content-sha256"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(OnePixelPng)).ToLowerInvariant()
                }
            });

        ObjectUploadRequest? storedRequest = null;
        byte[]? storedBytes = null;
        storage.Setup(store => store.UploadAsync(
                It.IsAny<ObjectUploadRequest>(),
                It.IsAny<Stream>(),
                OnePixelPng.Length,
                TestToken))
            .Returns(async (ObjectUploadRequest request, Stream content, long contentLength, CancellationToken _) =>
            {
                storedRequest = request;
                using var buffer = new MemoryStream();
                await content.CopyToAsync(buffer);
                storedBytes = buffer.ToArray();
                return new ObjectDescriptor
                {
                    BucketName = request.BucketName,
                    ObjectKey = request.ObjectKey,
                    ContentLength = contentLength,
                    ContentType = request.ContentType
                };
            });

        var service = CreateService(repository, storage);
        await service.ProcessUploadAsync(upload, TestToken);

        Assert.Equal(ProductImageUploadStatus.Ready, upload.Status);
        Assert.Equal("product-media", storedRequest?.BucketName);
        Assert.Equal(upload.AssetKey, storedRequest?.ObjectKey);
        Assert.Equal(
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(OnePixelPng)).ToLowerInvariant(),
            storedRequest?.Metadata["content-sha256"]);
        Assert.Equal(OnePixelPng, storedBytes);
        storage.Verify(store => store.CopyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), TestToken), Times.Never);
        repository.Verify(store => store.UpdateAsync(upload, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Invalid_image_bytes_are_rejected_without_creating_a_media_object()
    {
        var invalidBytes = new byte[] { 0, 1, 2, 3, 4 };
        var upload = CreateUpload(invalidBytes.Length);
        var repository = new Mock<IProductImageUploadRepository>();
        repository.Setup(store => store.TryClaimPendingUploadAsync(upload.Id, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), TestToken))
            .ReturnsAsync(true);
        repository.Setup(store => store.GetUploadByIdAsync(upload.Id, true, TestToken)).ReturnsAsync(upload);
        repository.Setup(store => store.UpdateAsync(upload, CancellationToken.None)).Returns(Task.CompletedTask);

        var storage = new Mock<IObjectStorage>();
        storage.Setup(store => store.GetMetadataAsync("product-staging", upload.StagingKey, TestToken))
            .ReturnsAsync(new ObjectMetadata
            {
                Key = upload.StagingKey,
                ContentType = "image/png",
                ContentLength = invalidBytes.Length
            });
        storage.Setup(store => store.DownloadAsync("product-staging", upload.StagingKey, It.IsAny<Stream>(), TestToken))
            .Returns((string _, string _, Stream destination, CancellationToken token) =>
                destination.WriteAsync(invalidBytes.AsMemory(), token).AsTask());

        var service = CreateService(repository, storage);
        await service.ProcessUploadAsync(upload, TestToken);

        Assert.Equal(ProductImageUploadStatus.Rejected, upload.Status);
        repository.Verify(store => store.UpdateAsync(upload, CancellationToken.None), Times.Once);
        storage.Verify(store => store.UploadAsync(It.IsAny<ObjectUploadRequest>(), It.IsAny<Stream>(), It.IsAny<long>(), TestToken), Times.Never);
        storage.Verify(store => store.GetMetadataAsync("product-media", It.IsAny<string>(), TestToken), Times.Never);
    }

    [Fact]
    public async Task A_session_claimed_by_another_worker_is_not_read_from_storage()
    {
        var upload = CreateUpload(OnePixelPng.Length);
        var repository = new Mock<IProductImageUploadRepository>();
        repository.Setup(store => store.TryClaimPendingUploadAsync(upload.Id, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), TestToken))
            .ReturnsAsync(false);
        var storage = new Mock<IObjectStorage>(MockBehavior.Strict);

        var service = CreateService(repository, storage);
        await service.ProcessUploadAsync(upload, TestToken);

        storage.VerifyNoOtherCalls();
        repository.Verify(store => store.GetUploadByIdAsync(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_missing_staging_object_keeps_the_upload_pending_for_client_retry()
    {
        var upload = CreateUpload(OnePixelPng.Length);
        var repository = new Mock<IProductImageUploadRepository>();
        repository.Setup(store => store.TryClaimPendingUploadAsync(upload.Id, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), TestToken))
            .ReturnsAsync(true);
        repository.Setup(store => store.GetUploadByIdAsync(upload.Id, true, TestToken)).ReturnsAsync(upload);
        repository.Setup(store => store.UpdateAsync(upload, CancellationToken.None)).Returns(Task.CompletedTask);
        var storage = new Mock<IObjectStorage>();
        storage.Setup(store => store.GetMetadataAsync("product-staging", upload.StagingKey, TestToken))
            .ReturnsAsync((ObjectMetadata?)null);

        var service = CreateService(repository, storage);
        await service.ProcessUploadAsync(upload, TestToken);

        Assert.Equal(ProductImageUploadStatus.Pending, upload.Status);
        Assert.True(upload.NextAttemptAt > DateTimeOffset.UtcNow);
        repository.Verify(store => store.UpdateAsync(upload, CancellationToken.None), Times.Once);
        storage.Verify(store => store.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), TestToken), Times.Never);
        storage.Verify(store => store.UploadAsync(It.IsAny<ObjectUploadRequest>(), It.IsAny<Stream>(), It.IsAny<long>(), TestToken), Times.Never);
    }

    [Fact]
    public async Task Cleanup_removes_staging_object_and_schedules_retention_before_final_cleanup()
    {
        var upload = CreateUpload(OnePixelPng.Length);
        upload.Status = ProductImageUploadStatus.Ready;
        upload.NextAttemptAt = null;
        var repository = new Mock<IProductImageUploadRepository>();
        repository.Setup(store => store.GetUnreferencedReadyUploadsAsync(
                It.IsAny<DateTimeOffset>(),
                It.IsAny<int>(),
                TestToken))
            .ReturnsAsync(Array.Empty<ProductImageUploads>());
        repository.Setup(store => store.GetCleanupUploadsAsync(It.IsAny<int>(), TestToken))
            .ReturnsAsync([upload]);
        repository.Setup(store => store.TryClaimCleanupUploadAsync(
                upload.Id,
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                TestToken))
            .ReturnsAsync(true);
        repository.Setup(store => store.GetUploadByIdAsync(upload.Id, true, TestToken))
            .ReturnsAsync(upload);
        repository.Setup(store => store.UpdateAsync(upload, CancellationToken.None)).Returns(Task.CompletedTask);

        var storage = new Mock<IObjectStorage>();
        storage.Setup(store => store.ExistsAsync("product-staging", upload.StagingKey, TestToken))
            .ReturnsAsync(true);
        storage.Setup(store => store.DeleteAsync("product-staging", upload.StagingKey, TestToken))
            .Returns(Task.CompletedTask);

        var service = CreateService(repository, storage);
        await service.ProcessCleanupAsync(TestToken);

        Assert.NotNull(upload.StagingDeletedAt);
        Assert.True(upload.NextAttemptAt > DateTimeOffset.UtcNow);
        repository.Verify(store => store.UpdateAsync(upload, CancellationToken.None), Times.Once);
        storage.Verify(store => store.DeleteAsync("product-staging", upload.StagingKey, TestToken), Times.Once);
        storage.Verify(store => store.DeleteAsync("product-media", It.IsAny<string>(), TestToken), Times.Never);
    }

    [Fact]
    public async Task Unreferenced_ready_asset_is_removed_after_retention()
    {
        var upload = CreateUpload(OnePixelPng.Length);
        upload.Status = ProductImageUploadStatus.Ready;
        upload.StagingDeletedAt = DateTimeOffset.UtcNow.AddHours(-1);
        upload.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        var uploadRepository = new Mock<IProductImageUploadRepository>();
        uploadRepository.Setup(store => store.GetUnreferencedReadyUploadsAsync(
                It.IsAny<DateTimeOffset>(),
                It.IsAny<int>(),
                TestToken))
            .ReturnsAsync([upload]);
        uploadRepository.Setup(store => store.TryClaimOrphanedReadyUploadAsync(
                upload.Id,
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                TestToken))
            .ReturnsAsync(true);
        uploadRepository.Setup(store => store.GetCleanupUploadsAsync(It.IsAny<int>(), TestToken))
            .ReturnsAsync([upload]);
        uploadRepository.Setup(store => store.TryClaimCleanupUploadAsync(
                upload.Id,
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                TestToken))
            .ReturnsAsync(true);
        uploadRepository.Setup(store => store.GetUploadByIdAsync(upload.Id, true, TestToken))
            .ReturnsAsync(upload);
        uploadRepository.Setup(store => store.UpdateAsync(upload, CancellationToken.None)).Returns(Task.CompletedTask);

        var productRepository = new Mock<IProductRepository>();
        productRepository.Setup(store => store.BeginTransactionAsync(TestToken))
            .ReturnsAsync(Mock.Of<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>());
        productRepository.Setup(store => store.LockProductAsync(upload.ProductId, TestToken))
            .ReturnsAsync(new Products { Id = upload.ProductId, Name = "Test", Slug = "test" });
        productRepository.Setup(store => store.EndTransactionAsync(CancellationToken.None)).Returns(Task.CompletedTask);

        var imageRepository = new Mock<IProductImageRepository>();
        imageRepository.Setup(store => store.HasAssetReferenceAsync(upload.AssetKey!, TestToken))
            .ReturnsAsync(false);
        var storage = new Mock<IObjectStorage>();
        storage.Setup(store => store.DeleteAsync("product-media", upload.AssetKey!, TestToken))
            .Returns(Task.CompletedTask);

        var service = new ProductImageUploadProcessingService(
            productRepository.Object,
            imageRepository.Object,
            uploadRepository.Object,
            storage.Object,
            Microsoft.Extensions.Options.Options.Create(new ProductMediaOptions()),
            NullLogger<ProductImageUploadProcessingService>.Instance);
        await service.ProcessCleanupAsync(TestToken);

        Assert.Equal(ProductImageUploadStatus.Cleaned, upload.Status);
        Assert.Null(upload.AssetKey);
        uploadRepository.Verify(store => store.UpdateAsync(upload, CancellationToken.None), Times.Exactly(2));
        storage.Verify(store => store.DeleteAsync("product-media", It.IsAny<string>(), TestToken), Times.Once);
        storage.Verify(store => store.DeleteAsync("product-staging", It.IsAny<string>(), TestToken), Times.Never);
        productRepository.Verify(store => store.BeginTransactionAsync(TestToken), Times.Once);
        productRepository.Verify(store => store.EndTransactionAsync(CancellationToken.None), Times.Once);
    }

    private static ProductImageUploads CreateUpload(long contentLength) => new()
    {
        Id = 42,
        ProductId = 12,
        StagingKey = "12/staging-key",
        AssetKey = Guid.NewGuid().ToString("N"),
        ExpectedContentType = "image/png",
        ExpectedSize = contentLength,
        Status = ProductImageUploadStatus.Pending,
        ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
    };

    private static byte[] CreateOnePixelPng()
    {
        using var bitmap = new SKBitmap(1, 1);
        bitmap.Erase(SKColors.Red);
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private static ProductImageUploadProcessingService CreateService(
        Mock<IProductImageUploadRepository> repository,
        Mock<IObjectStorage> storage) => new(
        Mock.Of<IProductRepository>(),
        Mock.Of<IProductImageRepository>(),
        repository.Object,
        storage.Object,
        Microsoft.Extensions.Options.Options.Create(new ProductMediaOptions()),
        NullLogger<ProductImageUploadProcessingService>.Instance);
}
