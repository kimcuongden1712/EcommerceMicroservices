using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ObjectStorage.Abstractions.Interfaces;
using ObjectStorage.Abstractions.Models;
using ObjectStorage.S3.Configuration;
using ObjectStorage.S3.Extensions;
using Xunit;

namespace ObjectStorage.S3.Tests;

public sealed class GarageIntegrationTests
{
    [Fact]
    public async Task Garage_supports_storage_and_presigned_url_workflow()
    {
        var settings = GarageSettings.FromEnvironment();
        if (settings is null)
        {
            Assert.Skip(
                "Set the dedicated OBJECT_STORAGE_TEST_* environment variables and provision an isolated test bucket to run Garage integration tests.");
            return;
        }

        if (string.Equals(settings.BucketName, "product-media", StringComparison.OrdinalIgnoreCase)
            || string.Equals(settings.BucketName, "product-staging", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Garage integration tests must not use Product.API buckets.");
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{S3StorageOptions.SectionName}:ServiceUrl"] = settings.ServiceUrl,
                [$"{S3StorageOptions.SectionName}:Region"] = settings.Region,
                [$"{S3StorageOptions.SectionName}:AccessKey"] = settings.AccessKey,
                [$"{S3StorageOptions.SectionName}:SecretKey"] = settings.SecretKey,
                [$"{S3StorageOptions.SectionName}:ForcePathStyle"] = "true"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddS3ObjectStorage(configuration);
        using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<IObjectStorage>();

        var prefix = $"object-storage-integration/{Guid.NewGuid():N}";
        var sourceKey = $"{prefix}/source +% #.txt";
        var copyKey = $"{prefix}/copy.txt";
        var presignedPutKey = $"{prefix}/presigned-put.txt";
        var payload = Encoding.UTF8.GetBytes("Garage S3 integration test");
        var sourceCreated = false;
        var copyCreated = false;
        var presignedPutCreated = false;

        try
        {
            sourceCreated = true;
            await using (var uploadStream = new MemoryStream(payload))
            {
                var descriptor = await storage.UploadAsync(
                    new ObjectUploadRequest
                    {
                        BucketName = settings.BucketName,
                        ObjectKey = sourceKey,
                        ContentType = "text/plain",
                        Metadata = new Dictionary<string, string> { ["integration-test"] = "true" }
                    },
                    uploadStream,
                    payload.Length,
                    TestContext.Current.CancellationToken);

                Assert.Equal(sourceKey, descriptor.ObjectKey);
                Assert.Equal(payload.Length, descriptor.ContentLength);
            }

            Assert.True(await storage.ExistsAsync(
                settings.BucketName,
                sourceKey,
                TestContext.Current.CancellationToken));

            var metadata = await storage.GetMetadataAsync(
                settings.BucketName,
                sourceKey,
                TestContext.Current.CancellationToken);
            Assert.NotNull(metadata);
            Assert.Equal(payload.Length, metadata.ContentLength);
            Assert.Equal("true", metadata.CustomMetadata["integration-test"]);

            await using (var downloadStream = new MemoryStream())
            {
                await storage.DownloadAsync(
                    settings.BucketName,
                    sourceKey,
                    downloadStream,
                    TestContext.Current.CancellationToken);
                Assert.Equal(payload, downloadStream.ToArray());
            }

            copyCreated = true;
            await storage.CopyAsync(
                settings.BucketName,
                sourceKey,
                settings.BucketName,
                copyKey,
                TestContext.Current.CancellationToken);
            Assert.True(await storage.ExistsAsync(
                settings.BucketName,
                copyKey,
                TestContext.Current.CancellationToken));

            var getUrl = await storage.CreatePresignedDownloadUrlAsync(
                settings.BucketName,
                sourceKey,
                TimeSpan.FromMinutes(5),
                TestContext.Current.CancellationToken);
            using var httpClient = new HttpClient();
            using var getResponse = await httpClient.GetAsync(
                getUrl.Url,
                TestContext.Current.CancellationToken);
            getResponse.EnsureSuccessStatusCode();
            Assert.Equal(
                payload,
                await getResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));

            var putUrl = await storage.CreatePresignedUploadUrlAsync(
                settings.BucketName,
                presignedPutKey,
                TimeSpan.FromMinutes(5),
                "text/plain",
                TestContext.Current.CancellationToken);
            using var putRequest = new HttpRequestMessage(HttpMethod.Put, putUrl.Url)
            {
                Content = new ByteArrayContent(payload)
            };
            foreach (var header in putUrl.RequiredHeaders)
            {
                if (!putRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Value)
                    && !putRequest.Headers.TryAddWithoutValidation(header.Key, header.Value))
                {
                    throw new InvalidOperationException(
                        $"Could not apply required presigned header '{header.Key}'.");
                }
            }

            presignedPutCreated = true;
            using var putResponse = await httpClient.SendAsync(
                putRequest,
                TestContext.Current.CancellationToken);
            putResponse.EnsureSuccessStatusCode();
            Assert.True(await storage.ExistsAsync(
                settings.BucketName,
                presignedPutKey,
                TestContext.Current.CancellationToken));

            await storage.DeleteAsync(
                settings.BucketName,
                sourceKey,
                TestContext.Current.CancellationToken);
            sourceCreated = false;
            Assert.False(await storage.ExistsAsync(
                settings.BucketName,
                sourceKey,
                TestContext.Current.CancellationToken));

            await storage.DeleteAsync(
                settings.BucketName,
                copyKey,
                TestContext.Current.CancellationToken);
            copyCreated = false;

            await storage.DeleteAsync(
                settings.BucketName,
                presignedPutKey,
                TestContext.Current.CancellationToken);
            presignedPutCreated = false;
        }
        finally
        {
            await DeleteIfCreatedAsync(storage, settings.BucketName, sourceKey, sourceCreated);
            await DeleteIfCreatedAsync(storage, settings.BucketName, copyKey, copyCreated);
            await DeleteIfCreatedAsync(storage, settings.BucketName, presignedPutKey, presignedPutCreated);
        }
    }

    private static Task DeleteIfCreatedAsync(
        IObjectStorage storage,
        string bucketName,
        string objectKey,
        bool created)
    {
        // Cleanup must still run if the test runner cancels the test.
        return created
            ? storage.DeleteAsync(bucketName, objectKey, CancellationToken.None)
            : Task.CompletedTask;
    }

    private sealed record GarageSettings(
        string ServiceUrl,
        string Region,
        string AccessKey,
        string SecretKey,
        string BucketName)
    {
        public static GarageSettings? FromEnvironment()
        {
            var values = new[]
            {
                Environment.GetEnvironmentVariable("OBJECT_STORAGE_TEST_SERVICE_URL"),
                Environment.GetEnvironmentVariable("OBJECT_STORAGE_TEST_REGION"),
                Environment.GetEnvironmentVariable("OBJECT_STORAGE_TEST_ACCESS_KEY"),
                Environment.GetEnvironmentVariable("OBJECT_STORAGE_TEST_SECRET_KEY"),
                Environment.GetEnvironmentVariable("OBJECT_STORAGE_TEST_BUCKET")
            };

            return values.Any(string.IsNullOrWhiteSpace)
                ? null
                : new GarageSettings(values[0]!, values[1]!, values[2]!, values[3]!, values[4]!);
        }
    }
}
