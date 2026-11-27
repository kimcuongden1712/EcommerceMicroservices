using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Product.API.Extensions;
using Shared.Configurations;
using Serilog;

namespace Product.API.Tests;

public sealed class ProductPipelineTests
{
    [Theory]
    [InlineData(400,"BAD_REQUEST")] [InlineData(401,"AUTHENTICATION_REQUIRED")] [InlineData(403,"FORBIDDEN")]
    [InlineData(404,"NOT_FOUND")] [InlineData(405,"METHOD_NOT_ALLOWED")] [InlineData(418,"HTTP_ERROR")]
    public async Task Empty_error_responses_are_converted_to_problem_json(int status,string code)
    {
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Testing",ApplicationName=typeof(ServiceExtensions).Assembly.FullName});
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{
            ["DatabaseSettings:ConnectionString"]="Server=localhost;Database=pipeline_only",
            ["ApiConfiguration:ApiName"]="product",["ApiConfiguration:ApiVersion"]="v1",
            ["ApiConfiguration:IssuerUri"]="https://identity.test",["ApiConfiguration:IdentityServerBaseUrl"]="https://identity.test"
        });
        builder.Services.AddSingleton(builder.Configuration.GetSection("DatabaseSettings").Get<DatabaseSettings>()!);
        builder.Services.AddSingleton(builder.Configuration.GetSection("ApiConfiguration").Get<ApiConfiguration>()!);
        using var logger=new LoggerConfiguration().CreateLogger();builder.Services.AddSingleton<Serilog.ILogger>(logger);
        builder.Services.AddInfrastructure(builder.Configuration);
        await using var app=builder.Build();app.UseInfrastructure();app.Run(context=>{context.Response.StatusCode=status;return Task.CompletedTask;});
        var token=TestContext.Current.CancellationToken;await app.StartAsync(token);
        using var client=app.GetTestClient();using var response=await client.GetAsync("/test-status",token);
        Assert.Equal(status,(int)response.StatusCode);Assert.Equal("application/problem+json",response.Content.Headers.ContentType!.MediaType);
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        Assert.Equal(status,json.RootElement.GetProperty("status").GetInt32());Assert.Contains(code,json.RootElement.ToString());
    }
}
