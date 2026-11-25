using Moq;
using Microsoft.AspNetCore.Http;
using ObjectStorage.Abstractions.Interfaces;
using Product.API.Commons.Exceptions;
using Product.API.Contracts.V1;
using Product.API.Options;
using Product.API.Persistence;
using Product.API.Repositories.Interfaces;
using Product.API.Services.V1;

namespace Product.API.Tests;

public sealed class ProductImageUploadServiceTests
{
    [Theory]
    [InlineData("image/gif", 1024)]
    [InlineData("image/png", 0)]
    [InlineData("image/jpeg", 5242881)]
    public async Task Invalid_upload_metadata_is_rejected_before_database_or_storage_access(
        string contentType,
        long contentLength)
    {
        var dbContext = new ProductContext(new Microsoft.EntityFrameworkCore.DbContextOptions<ProductContext>());
        var products = new Mock<IProductRepository>(MockBehavior.Strict);
        var uploads = new Mock<IProductImageUploadRepository>(MockBehavior.Strict);
        var storage = new Mock<IObjectStorage>(MockBehavior.Strict);
        var service = new ProductImageUploadService(
            dbContext,
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
        await dbContext.DisposeAsync();
    }
}
