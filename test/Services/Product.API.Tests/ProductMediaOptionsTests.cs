using Product.API.Options;

namespace Product.API.Tests;

public sealed class ProductMediaOptionsTests
{
    [Fact]
    public void Defaults_are_valid_for_the_local_product_media_buckets()
    {
        var options = new ProductMediaOptions();

        Assert.True(options.IsValid());
    }

    [Theory]
    [InlineData("Product-Media")]
    [InlineData("product..media")]
    [InlineData("-product-media")]
    [InlineData("192.168.1.10")]
    public void Invalid_bucket_names_are_rejected(string bucketName)
    {
        Assert.False(ProductMediaOptions.IsValidBucketName(bucketName));
    }

    [Fact]
    public void Duplicate_content_types_are_case_insensitive()
    {
        var options = new ProductMediaOptions
        {
            AllowedContentTypes = ["image/jpeg", "IMAGE/JPEG"]
        };

        Assert.False(options.IsValid());
    }
}
