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

    private static IConfiguration CreateConfiguration(
        string accessKey = "local-test-access-key",
        string secretKey = "local-test-secret-key")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{S3StorageOptions.SectionName}:ServiceUrl"] = "http://localhost:3900",
                [$"{S3StorageOptions.SectionName}:Region"] = "garage",
                [$"{S3StorageOptions.SectionName}:AccessKey"] = accessKey,
                [$"{S3StorageOptions.SectionName}:SecretKey"] = secretKey,
                [$"{S3StorageOptions.SectionName}:ForcePathStyle"] = "true"
            })
            .Build();
    }
}
