using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ObjectStorage.Abstractions.Interfaces;
using ObjectStorage.S3.Configuration;
using ObjectStorage.S3.Extensions;
using ObjectStorage.S3.Services;
using Xunit;

namespace ObjectStorage.S3.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddS3ObjectStorage_registers_singleton_client_and_storage_service()
    {
        var services = new ServiceCollection();
        services.AddS3ObjectStorage(CreateConfiguration());
        using var provider = services.BuildServiceProvider();

        var firstClient = provider.GetRequiredService<IAmazonS3>();
        var secondClient = provider.GetRequiredService<IAmazonS3>();
        var firstStorage = provider.GetRequiredService<IObjectStorage>();
        var secondStorage = provider.GetRequiredService<IObjectStorage>();

        Assert.Same(firstClient, secondClient);
        Assert.Same(firstStorage, secondStorage);
        Assert.IsType<S3ObjectStorage>(firstStorage);
    }

    [Fact]
    public void AddS3ObjectStorage_validates_configuration_and_does_not_echo_credentials()
    {
        const string accessKey = "access-key-sentinel";
        var services = new ServiceCollection();
        services.AddS3ObjectStorage(CreateConfiguration(accessKey, string.Empty));
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<S3StorageOptions>>().Value);

        Assert.DoesNotContain(accessKey, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Presigned_urls_use_the_public_endpoint_while_storage_operations_keep_the_internal_endpoint()
    {
        var services = new ServiceCollection();
        services.AddS3ObjectStorage(CreateConfiguration(presignedServiceUrl: "http://127.0.0.1:3900"));
        using var provider = services.BuildServiceProvider();

        var internalClient = provider.GetRequiredService<IAmazonS3>();
        var storage = provider.GetRequiredService<IObjectStorage>();
        var upload = await storage.CreatePresignedUploadUrlAsync(
            "product-staging",
            "12/test-image",
            TimeSpan.FromMinutes(5),
            "image/png");

        Assert.Contains("localhost:3900", internalClient.Config.ServiceURL, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("127.0.0.1", new Uri(upload.Url).Host);
    }

    private static IConfiguration CreateConfiguration(
        string accessKey = "local-test-access-key",
        string secretKey = "local-test-secret-key",
        string? presignedServiceUrl = null)
    {
        var values = new Dictionary<string, string?>
        {
            [$"{S3StorageOptions.SectionName}:ServiceUrl"] = "http://localhost:3900",
            [$"{S3StorageOptions.SectionName}:Region"] = "garage",
            [$"{S3StorageOptions.SectionName}:AccessKey"] = accessKey,
            [$"{S3StorageOptions.SectionName}:SecretKey"] = secretKey,
            [$"{S3StorageOptions.SectionName}:ForcePathStyle"] = "true"
        };
        if (presignedServiceUrl is not null)
            values[$"{S3StorageOptions.SectionName}:PresignedServiceUrl"] = presignedServiceUrl;

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
