using Product.API.BackgroundServices;
using Product.API.Options;
using Product.API.Services.V1;
using Product.API.Services.V1.Interfaces;

namespace Product.API.Extensions;

public static class ProductImageServiceCollectionExtensions
{
    public static IServiceCollection AddProductImageServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ProductMediaOptions>()
            .Bind(configuration.GetSection(ProductMediaOptions.SectionName))
            .Configure(options =>
            {
                // Binding extends initialized collections; replace configured types to avoid duplicates.
                options.AllowedContentTypes = configuration
                    .GetSection($"{ProductMediaOptions.SectionName}:AllowedContentTypes")
                    .Get<string[]>() ?? new ProductMediaOptions().AllowedContentTypes;
            })
            .Validate(options => options.IsValid(), "ProductMedia configuration is invalid.")
            .ValidateOnStart();

        services.AddScoped<IProductImageUploadService, ProductImageUploadService>();
        services.AddScoped<IProductImageGalleryService, ProductImageGalleryService>();
        services.AddScoped<IProductImageDeliveryService, ProductImageDeliveryService>();
        services.AddScoped<IProductImageUploadProcessingService, ProductImageUploadProcessingService>();
        services.AddHostedService<ProductImageUploadProcessor>();

        return services;
    }
}
