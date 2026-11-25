namespace ObjectStorage.S3.Configuration;

public sealed class S3StorageOptions
{
    public const string SectionName = "ObjectStorage:S3";

    public string ServiceUrl { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public bool ForcePathStyle { get; set; } = true;

    public bool IsValid()
    {
        return IsValidServiceUrl(ServiceUrl)
            && !string.IsNullOrWhiteSpace(Region)
            && !string.IsNullOrWhiteSpace(AccessKey)
            && !string.IsNullOrWhiteSpace(SecretKey);
    }

    private static bool IsValidServiceUrl(string serviceUrl)
    {
        return Uri.TryCreate(serviceUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && string.IsNullOrEmpty(uri.UserInfo)
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment);
    }
}
