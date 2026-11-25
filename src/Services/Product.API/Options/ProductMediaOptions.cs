using System.Text.RegularExpressions;

namespace Product.API.Options;

public sealed class ProductMediaOptions
{
    public const string SectionName = "ProductMedia";

    public string StagingBucket { get; set; } = "product-staging";
    public string MediaBucket { get; set; } = "product-media";
    public int UploadUrlExpirationMinutes { get; set; } = 10;
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;
    public int MaxImagesPerProduct { get; set; } = 10;
    public int PendingUploadExpirationMinutes { get; set; } = 30;
    public int CleanupRetentionMinutes { get; set; } = 30;
    public IReadOnlyList<string> AllowedContentTypes { get; set; } =
        ["image/jpeg", "image/png", "image/webp"];

    public static bool IsValidBucketName(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length is >= 3 and <= 63 &&
        Regex.IsMatch(value, "^[a-z0-9]+(?:[.-][a-z0-9]+)*$") &&
        !Regex.IsMatch(value, @"^\d{1,3}(\.\d{1,3}){3}$");

    public bool IsValid() =>
        IsValidBucketName(StagingBucket) &&
        IsValidBucketName(MediaBucket) &&
        !string.Equals(StagingBucket, MediaBucket, StringComparison.Ordinal) &&
        UploadUrlExpirationMinutes is > 0 and <= 60 &&
        MaxFileSizeBytes is > 0 and <= 5 * 1024 * 1024 &&
        MaxImagesPerProduct is > 0 and <= 10 &&
        PendingUploadExpirationMinutes > 0 &&
        CleanupRetentionMinutes > 0 &&
        AllowedContentTypes is { Count: > 0 } &&
        AllowedContentTypes.All(type => type is "image/jpeg" or "image/png" or "image/webp") &&
        AllowedContentTypes.Distinct(StringComparer.OrdinalIgnoreCase).Count() == AllowedContentTypes.Count;
}
