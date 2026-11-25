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

    [Fact]
    public async Task Validated_image_bytes_are_written_to_media_and_session_becomes_ready()
    {
        var upload = CreateUpload(OnePixelPng.Length);
        var repository = new Mock<IProductImageUploadRepository>();
        repository.Setup(store => store.TryClaimPendingUploadAsync(upload.Id, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), TestToken))
            .ReturnsAsync(true);
        repository.Setup(store => store.GetUploadByIdAsync(upload.Id, true, TestToken)).ReturnsAsync(upload);
        repository.Setup(store => store.SaveChangesAsync()).ReturnsAsync(1);

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
        repository.Verify(store => store.SaveChangesAsync(), Times.Once);
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
        repository.Setup(store => store.SaveChangesAsync()).ReturnsAsync(1);

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
        repository.Setup(store => store.SaveChangesAsync()).ReturnsAsync(1);
        var storage = new Mock<IObjectStorage>();
        storage.Setup(store => store.GetMetadataAsync("product-staging", upload.StagingKey, TestToken))
            .ReturnsAsync((ObjectMetadata?)null);

        var service = CreateService(repository, storage);
        await service.ProcessUploadAsync(upload, TestToken);

        Assert.Equal(ProductImageUploadStatus.Pending, upload.Status);
        Assert.True(upload.NextAttemptAt > DateTimeOffset.UtcNow);
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
        repository.Setup(store => store.SaveChangesAsync()).ReturnsAsync(1);

        var storage = new Mock<IObjectStorage>();
        storage.Setup(store => store.ExistsAsync("product-staging", upload.StagingKey, TestToken))
            .ReturnsAsync(true);
        storage.Setup(store => store.DeleteAsync("product-staging", upload.StagingKey, TestToken))
            .Returns(Task.CompletedTask);

        var service = CreateService(repository, storage);
        await service.ProcessCleanupAsync(TestToken);

        Assert.NotNull(upload.StagingDeletedAt);
        Assert.True(upload.NextAttemptAt > DateTimeOffset.UtcNow);
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
        uploadRepository.Setup(store => store.SaveChangesAsync()).ReturnsAsync(1);

        var productRepository = new Mock<IProductRepository>();
        productRepository.Setup(store => store.BeginTransactionAsync())
            .ReturnsAsync(Mock.Of<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>());
        productRepository.Setup(store => store.LockProductAsync(upload.ProductId, TestToken))
            .ReturnsAsync(new Products { Id = upload.ProductId, Name = "Test", Slug = "test" });
        productRepository.Setup(store => store.EndTransactionAsync()).Returns(Task.CompletedTask);

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
        storage.Verify(store => store.DeleteAsync("product-media", It.IsAny<string>(), TestToken), Times.Once);
        storage.Verify(store => store.DeleteAsync("product-staging", It.IsAny<string>(), TestToken), Times.Never);
        productRepository.Verify(store => store.EndTransactionAsync(), Times.Once);
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
