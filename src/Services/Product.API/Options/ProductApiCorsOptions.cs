namespace Product.API.Options;

public sealed class ProductApiCorsOptions
{
    public const string SectionName = "ProductApiCors";
    public const string PolicyName = "ProductApiBrowser";

    public string[] AllowedOrigins { get; set; } = [];

    public bool IsValid() => AllowedOrigins is not null && AllowedOrigins.All(IsValidOrigin);

    public static string[] ParseAllowedOrigins(IEnumerable<string>? configuredValues)
    {
        if (configuredValues is null)
            return [];

        var origins = new List<string>();
        foreach (var configuredValue in configuredValues)
        {
            if (configuredValue is null)
                continue;

            foreach (var value in configuredValue.Split(',', StringSplitOptions.None))
            {
                var origin = value.Trim();
                if (origin.Length == 0)
                    continue;

                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                    uri.Scheme is not ("http" or "https") ||
                    string.IsNullOrWhiteSpace(uri.Host) ||
                    uri.UserInfo.Length > 0 ||
                    uri.Query.Length > 0 ||
                    uri.Fragment.Length > 0 ||
                    uri.AbsolutePath is not ("" or "/") ||
                    origin.Contains('*', StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Product API CORS origin '{origin}' is invalid. Use an exact http(s) origin without a path or wildcard.");
                }

                var normalized = uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
                if (!origins.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    origins.Add(normalized);
            }
        }

        return origins.ToArray();
    }

    private static bool IsValidOrigin(string? origin) =>
        !string.IsNullOrWhiteSpace(origin) &&
        Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https" &&
        !string.IsNullOrWhiteSpace(uri.Host) &&
        uri.UserInfo.Length == 0 &&
        uri.Query.Length == 0 &&
        uri.Fragment.Length == 0 &&
        uri.AbsolutePath is "" or "/" &&
        !origin.Contains('*', StringComparison.Ordinal);
}
