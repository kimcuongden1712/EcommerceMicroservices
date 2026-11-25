using ObjectStorage.Abstractions.Interfaces;
using Product.API.Commons.Enums;
using Product.API.Commons.Exceptions;
using Product.API.Contracts.V1;
using Product.API.Entities;
using Product.API.Options;
using Product.API.Persistence;
using Product.API.Repositories.Interfaces;
using Product.API.Services.V1.Interfaces;

namespace Product.API.Services.V1;

public sealed class ProductImageUploadService : IProductImageUploadService
{
    private readonly ProductContext _dbContext;
    private readonly IProductRepository _productRepository;
    private readonly IProductImageUploadRepository _uploadRepository;
    private readonly IObjectStorage _objectStorage;
    private readonly ProductMediaOptions _options;

    public ProductImageUploadService(
        ProductContext dbContext,
        IProductRepository productRepository,
        IProductImageUploadRepository uploadRepository,
        IObjectStorage objectStorage,
        Microsoft.Extensions.Options.IOptions<ProductMediaOptions> options)
    {
        _dbContext = dbContext;
        _productRepository = productRepository;
        _uploadRepository = uploadRepository;
        _objectStorage = objectStorage;
        _options = options.Value;
    }

    public async Task<ProductImageUploadResponse> CreateUploadSessionAsync(
        long productId,
        CreateProductImageUploadRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var contentType = request.ContentType?.Trim();
        if (string.IsNullOrWhiteSpace(contentType) ||
            !_options.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            throw new ProductMediaException("INVALID_CONTENT_TYPE", "Only JPEG, PNG, and WebP images are allowed.");

        if (request.ContentLength <= 0 || request.ContentLength > _options.MaxFileSizeBytes)
            throw new ProductMediaException(
                "INVALID_CONTENT_LENGTH",
                $"Image size must be between 1 and {_options.MaxFileSizeBytes} bytes.");

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var product = await _productRepository.LockProductAsync(productId, cancellationToken);
        if (product is null)
            throw CatalogAdminException.NotFound("product");

        var activeUploadSlots = await _uploadRepository.CountActiveUploadSlotsAsync(
            productId,
            DateTimeOffset.UtcNow,
            cancellationToken);
        if (activeUploadSlots >= _options.MaxImagesPerProduct)
            throw new ProductMediaException("IMAGE_LIMIT_EXCEEDED", "Too many active image uploads are already pending for this product.");

        var now = DateTimeOffset.UtcNow;
        var upload = new ProductImageUploads
        {
            ProductId = productId,
            StagingKey = $"{productId}/{Guid.NewGuid():N}",
            AssetKey = Guid.NewGuid().ToString("N"),
            ExpectedContentType = contentType.ToLowerInvariant(),
            ExpectedSize = request.ContentLength,
            Status = ProductImageUploadStatus.Pending,
            ExpiresAt = now.AddMinutes(_options.PendingUploadExpirationMinutes),
            CreatedAt = now
        };

        try
        {
            _uploadRepository.Create(upload);
            await _dbContext.SaveChangesAsync(cancellationToken);
            var presigned = await _objectStorage.CreatePresignedUploadUrlAsync(
                _options.StagingBucket,
                upload.StagingKey,
                TimeSpan.FromMinutes(_options.UploadUrlExpirationMinutes),
                upload.ExpectedContentType,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new ProductImageUploadResponse(
                upload.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                presigned.Url,
                presigned.RequiredHeaders,
                presigned.ExpiresAt);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<ProductImageUploadStatusResponse?> GetUploadSessionAsync(
        long productId,
        long uploadId,
        CancellationToken cancellationToken)
    {
        if (await _productRepository.GetProductAsync(productId, false, cancellationToken) is null)
            return null;

        var upload = await _uploadRepository.GetProductImageUploadAsync(productId, uploadId, cancellationToken);
        if (upload is null)
            return null;

        var status = upload.Status == ProductImageUploadStatus.Cleaned
            ? ProductImageUploadStatus.Expired
            : upload.Status;
        return new ProductImageUploadStatusResponse(
            upload.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            status,
            upload.ExpiresAt);
    }
}
