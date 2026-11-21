using Contracts.Domains.Interfaces;
using Infrastructure.Common;
using Infrastructure.Extensions;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Product.API.Persistence;
using Product.API.Repositories;
using Product.API.Repositories.Interfaces;
using Shared.Configurations;
using System.Text;

namespace Product.API.Extensions;

public static class ServiceExtensions
{
    internal static IServiceCollection AddConfigurationSettings (this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection(nameof(JwtSettings))
            .Get<JwtSettings>();
        if (jwtSettings is not null)
            services.AddSingleton(jwtSettings);

        var databaseSettings = configuration.GetSection(nameof(DatabaseSettings))
            .Get<DatabaseSettings>();
        if (databaseSettings is null || string.IsNullOrEmpty(databaseSettings.ConnectionString))
            throw new ArgumentNullException("Connection string is not configured.");

        services.AddSingleton(databaseSettings);

        var apiConfiguration = configuration.GetSection(nameof(ApiConfiguration))
            .Get<ApiConfiguration>();
        if (apiConfiguration is not null)
            services.AddSingleton(apiConfiguration);

        return services;
    }

    public static IServiceCollection AddInfrastructure (this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.Configure<RouteOptions>(options => options.LowercaseUrls = true);
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        services.AddEndpointsApiExplorer();
        services.ConfigureSwagger();
        services.ConfigureProductDbContext(configuration);
        services.AddInfrastructureServices();
        // services.AddJwtAuthentication();
        services.ConfigureAuthenticationHandler();
        services.ConfigureAuthorization();
        services.ConfigureHealthChecks();
        return services;
    }

    internal static IServiceCollection AddJwtAuthentication (this IServiceCollection services)
    {
        var settings = services.GetOptions<JwtSettings>(nameof(JwtSettings));
        if (settings == null || string.IsNullOrEmpty(settings.Key))
            throw new ArgumentNullException($"{nameof(JwtSettings)} is not configured properly");

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key));

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            ClockSkew = TimeSpan.Zero,
            RequireExpirationTime = false
        };
        services.AddAuthentication(o =>
        {
            o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(x =>
        {
            x.SaveToken = true;
            x.RequireHttpsMetadata = false;
            x.TokenValidationParameters = tokenValidationParameters;
        });

        return services;
    }

    private static IServiceCollection ConfigureProductDbContext (this IServiceCollection services,
        IConfiguration configuration)
    {
        var databaseSettings = configuration.GetSection(nameof(DatabaseSettings)).Get<DatabaseSettings>();
        if (databaseSettings == null || string.IsNullOrEmpty(databaseSettings.ConnectionString))
            throw new ArgumentNullException($"{nameof(databaseSettings)} is not configured properly");

        services.AddDbContext<ProductContext>(options => options.UseMySQL(databaseSettings.ConnectionString, mysqlOptions =>
            {
                mysqlOptions.MigrationsAssembly("Product.API");
                mysqlOptions.CommandTimeout(30);
            }));

        return services;
    }

    private static IServiceCollection AddInfrastructureServices (this IServiceCollection services)
    {
        return services.AddScoped(typeof(IRepositoryBase<,,>), typeof(RepositoryBase<,,>))
                .AddScoped(typeof(IUnitOfWork<>), typeof(UnitOfWork<>))
                .AddScoped<IProductRepository, ProductRepository>()
            ;
    }

    private static void ConfigureHealthChecks (this IServiceCollection services)
    {
        var databaseSettings = services.GetOptions<DatabaseSettings>(nameof(DatabaseSettings));
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
            .AddMySql(
            healthQuery: "SELECT 1;",
            connectionString: databaseSettings.ConnectionString,
            name: "MySql Health",
            failureStatus: HealthStatus.Unhealthy,
            tags: new[] { "ready" });
    }

    public static void ConfigureSwagger (this IServiceCollection services)
    {
        var configuration = services.GetOptions<ApiConfiguration>("ApiConfiguration");
        if (configuration == null || string.IsNullOrEmpty(configuration.IssuerUri) ||
            string.IsNullOrEmpty(configuration.ApiName))
            throw new Exception("ApiConfiguration is not configured!");

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1",
                new OpenApiInfo
                {
                    Title = "Product API V1",
                    Version = configuration.ApiVersion
                });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows
                {
                    Implicit = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri($"{configuration.IdentityServerBaseUrl}/connect/authorize"),
                        Scopes = new Dictionary<string, string>
                        {
                            { "microservices_api.read", "Microservices API Read Scope" },
                            { "microservices_api.write", "Microservices API Write Scope" }
                        }
                    }
                }
            });
            c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecuritySchemeReference("Bearer", document),
                    new List<string>
                    {
                        "microservices_api.read",
                        "microservices_api.write"
                    }
                }
            });
        });
    }
}
