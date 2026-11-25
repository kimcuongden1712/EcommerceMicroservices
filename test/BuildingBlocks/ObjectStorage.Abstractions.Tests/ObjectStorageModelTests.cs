using ObjectStorage.Abstractions.Models;
using Xunit;

namespace ObjectStorage.Abstractions.Tests;

public sealed class ObjectStorageModelTests
{
    [Fact]
    public void Metadata_collections_default_to_empty_and_are_not_shared_between_instances()
    {
        var firstUpload = new ObjectUploadRequest
        {
            BucketName = "catalog",
            ObjectKey = "images/one.png"
        };
        var secondUpload = new ObjectUploadRequest
        {
            BucketName = "catalog",
            ObjectKey = "images/two.png"
        };
        var firstMetadata = new ObjectMetadata { Key = "images/one.png" };
        var secondMetadata = new ObjectMetadata { Key = "images/two.png" };
        var firstPresignedUrl = new PresignedUrlResult { Url = "https://storage.example/one" };
        var secondPresignedUrl = new PresignedUrlResult { Url = "https://storage.example/two" };

        Assert.Empty(firstUpload.Metadata);
        Assert.Empty(firstMetadata.CustomMetadata);
        Assert.Empty(firstPresignedUrl.RequiredHeaders);
        Assert.NotSame(firstUpload.Metadata, secondUpload.Metadata);
        Assert.NotSame(firstMetadata.CustomMetadata, secondMetadata.CustomMetadata);
        Assert.NotSame(firstPresignedUrl.RequiredHeaders, secondPresignedUrl.RequiredHeaders);
    }
}
