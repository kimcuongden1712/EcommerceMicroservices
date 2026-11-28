using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Product.API.Extensions;
using Product.API.Options;
using Shared.Configurations;
using Serilog;

namespace Product.API.Tests;

public sealed class ProductCorsTests
{
    [Fact]
    public void Origin_parser_rejects_wildcards_and_paths()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ProductApiCorsOptions.ParseAllowedOrigins(["*"]));
        Assert.Throws<InvalidOperationException>(() =>
            ProductApiCorsOptions.ParseAllowedOrigins(["https://mfe.example/app"]));
    }

    [Fact]
    public void Configuration_section_provides_the_allowlist_when_no_environment_override_exists()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ProductApiCors:AllowedOrigins:0"] = "https://mfe.example/"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddProductApiCors(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ProductApiCorsOptions>>().Value;

        Assert.Equal(["https://mfe.example"], options.AllowedOrigins);
    }

    [Fact]
    public async Task Configured_origin_is_allowed_for_a_browser_preflight()
    {
        const string allowedOrigin = "https://mfe.example";
        await using var app = await CreateAppAsync(allowedOrigin);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/admin/products");
        request.Headers.TryAddWithoutValidation("Origin", allowedOrigin);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "POST");
        request.Headers.TryAddWithoutValidation(
            "Access-Control-Request-Headers",
            "authorization, content-type");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(allowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("POST", response.Headers.GetValues("Access-Control-Allow-Methods").Single());
        Assert.Contains("authorization", response.Headers.GetValues("Access-Control-Allow-Headers").Single(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Origin_outside_the_allowlist_does_not_receive_cors_headers()
    {
        await using var app = await CreateAppAsync("https://mfe.example");
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/admin/products");
        request.Headers.TryAddWithoutValidation("Origin", "https://attacker.example");
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "POST");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("Access-Control-Allow-Origin", response.Headers.Select(header => header.Key));
    }

    private static async Task<WebApplication> CreateAppAsync(string allowedOrigin)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseSettings:ConnectionString"] = "Server=localhost;Database=cors_only",
                ["ApiConfiguration:ApiName"] = "product",
                ["ApiConfiguration:ApiVersion"] = "v1",
                ["ApiConfiguration:IssuerUri"] = "https://identity.test",
                ["ApiConfiguration:IdentityServerBaseUrl"] = "https://identity.test",
                ["PRODUCT_API_CORS_ALLOWED_ORIGINS"] = allowedOrigin
            })
            .Build();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ApplicationName = typeof(ServiceExtensions).Assembly.FullName
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddConfiguration(configuration);
        builder.Services.AddSingleton(builder.Configuration.GetSection("DatabaseSettings").Get<DatabaseSettings>()!);
        builder.Services.AddSingleton(builder.Configuration.GetSection("ApiConfiguration").Get<ApiConfiguration>()!);
        using var logger = new LoggerConfiguration().CreateLogger();
        builder.Services.AddSingleton<Serilog.ILogger>(logger);
        builder.Services.AddInfrastructure(builder.Configuration);

        var app = builder.Build();
        app.UseInfrastructure();
        app.Run(_ => Task.CompletedTask);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
