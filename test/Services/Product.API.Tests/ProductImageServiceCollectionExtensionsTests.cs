using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Product.API.BackgroundServices;
using Product.API.Extensions;
using Product.API.Services.V1;
using Product.API.Services.V1.Interfaces;

namespace Product.API.Tests;

public sealed class ProductImageServiceCollectionExtensionsTests
{
    [Fact]
    public void AddProductImageServices_registers_scoped_use_cases_and_single_hosted_worker()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddProductImageServices(configuration);

        AssertScoped<IProductImageUploadService, ProductImageUploadService>(services);
        AssertScoped<IProductImageGalleryService, ProductImageGalleryService>(services);
        AssertScoped<IProductImageDeliveryService, ProductImageDeliveryService>(services);
        AssertScoped<IProductImageUploadProcessingService, ProductImageUploadProcessingService>(services);
        Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(ProductImageUploadProcessor));
    }

    private static void AssertScoped<TService, TImplementation>(IServiceCollection services)
    {
        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(TService));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(TImplementation), descriptor.ImplementationType);
    }
}
