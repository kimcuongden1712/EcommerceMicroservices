using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ObjectStorage.Abstractions.Interfaces;
using ObjectStorage.S3.Configuration;
using ObjectStorage.S3.Services;

namespace ObjectStorage.S3.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddS3ObjectStorage (
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<S3StorageOptions>()
            .Bind(configuration.GetSection(S3StorageOptions.SectionName))
            .Validate(
                options =>
                    Uri.TryCreate(
                        options.ServiceUrl,
                        UriKind.Absolute,
                        out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp ||
                     uri.Scheme == Uri.UriSchemeHttps),
                "A valid HTTP/HTTPS S3 ServiceUrl is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Region),
                "S3 Region is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.AccessKey),
                "S3 AccessKey is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.SecretKey),
                "S3 SecretKey is required.")
            .ValidateOnStart();

        services.AddSingleton<IAmazonS3>(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<S3StorageOptions>>()
                .Value;

            var credentials = new BasicAWSCredentials(
                options.AccessKey,
                options.SecretKey);

            var clientConfig = new AmazonS3Config
            {
                ServiceURL = options.ServiceUrl,
                AuthenticationRegion = options.Region,
                ForcePathStyle = options.ForcePathStyle
            };

            return new AmazonS3Client(credentials, clientConfig);
        });

        services.AddSingleton<IObjectStorage, S3ObjectStorage>();

        return services;
    }
}
