using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using ObjectStorage.Abstractions.Interfaces;
using Product.API.Commons.Exceptions;
using Product.API.Contracts.V1;
using Product.API.Entities;
using Product.API.Options;
using Product.API.Repositories.Interfaces;
using Product.API.Services.V1;

namespace Product.API.Tests;

public sealed class ProductImageUploadServiceTests
{
    [Theory]
    [InlineData("missing_product")]
    [InlineData("upload_limit")]
    [InlineData("lock_failure")]
    [InlineData("count_failure")]
    [InlineData("cancellation")]
    public async Task Early_upload_failure_rolls_back_and_disposes_transaction (string failure)
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var products = new Mock<IProductRepository>(MockBehavior.Strict);
        var uploads = new Mock<IProductImageUploadRepository>(MockBehavior.Strict);
        var storage = new Mock<IObjectStorage>(MockBehavior.Strict);
        var transaction = new Mock<IDbContextTransaction>(MockBehavior.Strict);
        transaction.Setup(value => value.DisposeAsync()).Returns(ValueTask.CompletedTask);
        products.Setup(value => value.BeginTransactionAsync(token)).ReturnsAsync(transaction.Object);
        products.Setup(value => value.RollbackTransactionAsync(CancellationToken.None)).Returns(Task.CompletedTask);
        var injectedFailure = new IOException("Database operation failed.");

        if (failure == "lock_failure") {
            products.Setup(value => value.LockProductAsync(10, token)).ThrowsAsync(injectedFailure);
        }
        else if (failure == "cancellation") {
            products.Setup(value => value.LockProductAsync(10, token))
                .Returns(() => {
                    cancellation.Cancel();
                    return Task.FromCanceled<Products?>(token);
                });
        }
        else {
            products.Setup(value => value.LockProductAsync(10, token))
                .ReturnsAsync(failure == "missing_product"
                    ? null
                    : new Products { Id = 10, Name = "Test", Slug = "test" });
            if (failure == "count_failure")
                uploads.Setup(value => value.CountActiveUploadSlotsAsync(10, It.IsAny<DateTimeOffset>(), token))
                    .ThrowsAsync(injectedFailure);
            else if (failure == "upload_limit")
                uploads.Setup(value => value.CountActiveUploadSlotsAsync(10, It.IsAny<DateTimeOffset>(), token))
                    .ReturnsAsync(10);
        }

        var service = new ProductImageUploadService(
            products.Object, uploads.Object, storage.Object,
            Microsoft.Extensions.Options.Options.Create(new ProductMediaOptions()));
        var exception = await Record.ExceptionAsync(() => service.CreateUploadSessionAsync(
            10, new CreateProductImageUploadRequest { ContentType = "image/png", ContentLength = 1024 }, token));

        Assert.NotNull(exception);
        if (failure == "missing_product")
            Assert.IsType<CatalogAdminException>(exception);
        else if (failure == "upload_limit")
            Assert.IsType<ProductMediaException>(exception);
        else if (failure == "cancellation")
            Assert.IsAssignableFrom<OperationCanceledException>(exception);
        else
            Assert.Same(injectedFailure, exception);

        products.Verify(value => value.RollbackTransactionAsync(CancellationToken.None), Times.Once);
        products.Verify(value => value.EndTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        uploads.Verify(value => value.CreateAsync(It.IsAny<ProductImageUploads>(), It.IsAny<CancellationToken>()), Times.Never);
        transaction.Verify(value => value.DisposeAsync(), Times.Once);
        storage.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("image/gif", 1024)]
    [InlineData("image/png", 0)]
    [InlineData("image/jpeg", 5242881)]
    public async Task Invalid_upload_metadata_is_rejected_before_database_or_storage_access (
        string contentType,
        long contentLength)
    {
        var products = new Mock<IProductRepository>(MockBehavior.Strict);
        var uploads = new Mock<IProductImageUploadRepository>(MockBehavior.Strict);
        var storage = new Mock<IObjectStorage>(MockBehavior.Strict);
        var service = new ProductImageUploadService(
            products.Object,
            uploads.Object,
            storage.Object,
            Microsoft.Extensions.Options.Options.Create(new ProductMediaOptions()));

        var exception = await Assert.ThrowsAsync<ProductMediaException>(() => service.CreateUploadSessionAsync(
            10,
            new CreateProductImageUploadRequest { ContentType = contentType, ContentLength = contentLength },
            CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        products.VerifyNoOtherCalls();
        uploads.VerifyNoOtherCalls();
        storage.VerifyNoOtherCalls();
    }
}
