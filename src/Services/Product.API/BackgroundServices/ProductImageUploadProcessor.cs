using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Product.API.Commons;
using Product.API.Repositories.Interfaces;
using Product.API.Services.V1.Interfaces;

namespace Product.API.BackgroundServices;

public sealed class ProductImageUploadProcessor : BackgroundService
{
    private const int BatchSize = 10;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProductImageUploadProcessor> _logger;

    public ProductImageUploadProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<ProductImageUploadProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Product image upload worker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Product image upload worker cycle failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
        _logger.LogInformation("Product image upload worker stopped.");
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IProductImageUploadRepository>();
        var processor = scope.ServiceProvider.GetRequiredService<IProductImageUploadProcessingService>();
        var now = DateTimeOffset.UtcNow;
        var pending = await repository.GetPendingUploadsAsync(
            now,
            now - ProductImageUploadDefaults.ProcessingLease,
            BatchSize,
            cancellationToken);

        foreach (var upload in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await processor.ProcessUploadAsync(upload, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Image upload processing failed for upload {UploadId}.", upload.Id);
            }
        }

        await processor.ProcessExpiredUploadsAsync(cancellationToken);
        await processor.ProcessCleanupAsync(cancellationToken);
    }
}
