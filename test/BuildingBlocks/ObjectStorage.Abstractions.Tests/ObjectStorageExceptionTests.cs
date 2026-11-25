using ObjectStorage.Abstractions.Exceptions;
using Xunit;

namespace ObjectStorage.Abstractions.Tests;

public sealed class ObjectStorageExceptionTests
{
    [Fact]
    public void Constructor_preserves_operation_location_message_and_inner_exception()
    {
        var innerException = new InvalidOperationException("Provider failure");
        var exception = new ObjectStorageException(
            "Copy",
            "catalog",
            "images/item.png",
            "Copy failed.",
            innerException);

        Assert.Equal("Copy", exception.Operation);
        Assert.Equal("catalog", exception.BucketName);
        Assert.Equal("images/item.png", exception.ObjectKey);
        Assert.Equal("Copy failed.", exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void Constructor_allows_bucket_level_errors_without_an_object_key_or_inner_exception()
    {
        var exception = new ObjectStorageException(
            "List",
            "catalog",
            null,
            "Bucket operation failed.");

        Assert.Equal("List", exception.Operation);
        Assert.Equal("catalog", exception.BucketName);
        Assert.Null(exception.ObjectKey);
        Assert.Null(exception.InnerException);
    }
}
