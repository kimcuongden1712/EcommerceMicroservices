using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Product.API.Extensions;
using Product.API.Repositories.Interfaces;
using Product.API.Services.V1.Interfaces;
using Shared.Configurations;
using Swashbuckle.AspNetCore.Swagger;

namespace Product.API.Tests;

public sealed class ProductInfrastructureRegistrationTests
{
    private static IConfiguration Configuration()=>new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
        ["DatabaseSettings:ConnectionString"]="Server=localhost;Database=registration_only",
        ["ApiConfiguration:ApiName"]="product",["ApiConfiguration:ApiVersion"]="v1",
        ["ApiConfiguration:IssuerUri"]="https://identity.test",["ApiConfiguration:IdentityServerBaseUrl"]="https://identity.test",
        ["JwtSettings:Key"]=new string('x',32)
    }).Build();

    [Fact]
    public void Infrastructure_registers_resolvable_services_and_problem_details_for_model_errors()
    {
        var configuration=Configuration();
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Testing",ApplicationName=typeof(ServiceExtensions).Assembly.FullName});
        var services=builder.Services;services.AddSingleton(configuration);
        var configure=typeof(ServiceExtensions).GetMethod("AddConfigurationSettings",BindingFlags.NonPublic|BindingFlags.Static)!;
        configure.Invoke(null,[services,configuration]);
        services.AddInfrastructure(configuration);
        using var provider=services.BuildServiceProvider();using var scope=provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IProductRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAdminCatalogService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPublicCatalogService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IProductImageGalleryService>());
        Assert.True(provider.GetRequiredService<IOptions<RouteOptions>>().Value.LowercaseUrls);
        var model=new ModelStateDictionary();model.AddModelError("name","Required");model.AddModelError("size","");model.SetModelValue("valid", "1", "1");
        var action=new ActionContext(new DefaultHttpContext(),new RouteData(),new ActionDescriptor(),model);
        var result=Assert.IsType<BadRequestObjectResult>(provider.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value.InvalidModelStateResponseFactory(action));
        Assert.Equal(400,result.StatusCode);Assert.Contains("application/problem+json",result.ContentTypes);
        var swagger=provider.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        Assert.Equal("Product API V1",swagger.Info.Title);Assert.NotEmpty(swagger.Security!);
    }

    [Fact]
    public void Missing_database_configuration_fails_before_registration()
    {
        var services=new ServiceCollection();var configuration=new ConfigurationBuilder().Build();
        var error=Assert.Throws<TargetInvocationException>(()=>typeof(ServiceExtensions).GetMethod("AddConfigurationSettings",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[services,configuration]));
        Assert.IsType<ArgumentNullException>(error.InnerException);
    }

    [Theory]
    [InlineData("database_only")] [InlineData("missing_issuer")] [InlineData("missing_api_name")]
    public void Incomplete_api_configuration_is_rejected(string scenario)
    {
        var values=new Dictionary<string,string?>{["DatabaseSettings:ConnectionString"]="Server=localhost;Database=registration_only"};
        if(scenario=="missing_issuer")values["ApiConfiguration:ApiName"]="product";
        if(scenario=="missing_api_name")values["ApiConfiguration:IssuerUri"]="https://identity.test";
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services=new ServiceCollection();services.AddSingleton<IConfiguration>(configuration);
        typeof(ServiceExtensions).GetMethod("AddConfigurationSettings",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[services,configuration]);
        Assert.Throws<Exception>(()=>services.AddInfrastructure(configuration));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Jwt_registration_requires_key_and_configures_bearer_options(bool hasKey)
    {
        var values=new Dictionary<string,string?>();if(hasKey)values["JwtSettings:Key"]=new string('x',32);
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(values).Build();var services=new ServiceCollection();services.AddLogging();services.AddSingleton<IConfiguration>(configuration);
        var register=typeof(ServiceExtensions).GetMethod("AddJwtAuthentication",BindingFlags.NonPublic|BindingFlags.Static)!;
        if(!hasKey){Assert.IsType<ArgumentNullException>(Assert.Throws<TargetInvocationException>(()=>register.Invoke(null,[services])).InnerException);return;}
        register.Invoke(null,[services]);using var provider=services.BuildServiceProvider();
        var options=provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
        Assert.True(options.SaveToken);Assert.NotNull(options.TokenValidationParameters.IssuerSigningKey);
    }
}
