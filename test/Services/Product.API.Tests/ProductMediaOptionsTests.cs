using Product.API.Options;
using Product.API.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Product.API.Tests;

public sealed class ProductMediaOptionsTests
{
    [Fact]
    public void Configured_content_types_replace_defaults_and_pass_validation()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ProductMedia:AllowedContentTypes:0"] = "image/jpeg",
                ["ProductMedia:AllowedContentTypes:1"] = "image/png",
                ["ProductMedia:AllowedContentTypes:2"] = "image/webp"
            }).Build();
        var services = new ServiceCollection();
        services.AddProductImageServices(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<ProductMediaOptions>>().Value;

        Assert.Equal(new[] { "image/jpeg", "image/png", "image/webp" }, options.AllowedContentTypes);
        Assert.True(options.IsValid());
    }

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
