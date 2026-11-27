using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Product.API.Authorization;
using Shared.Common.Constants;

namespace Product.API.Tests;

public sealed class CatalogAuthorizationTests
{
    [Theory]
    [InlineData(null,false)] [InlineData("PRODUCT.VIEW",true)] [InlineData("product.view",false)]
    [InlineData("[\"PRODUCT.VIEW\"]",true)] [InlineData("[\"OTHER\"]",false)]
    [InlineData("null",false)] [InlineData("broken-json",false)] [InlineData("{}",false)]
    public async Task Permissions_require_exact_plain_or_json_claim(string? value, bool allowed)
    {
        var requirement = new CatalogPermissionRequirement("PRODUCT.VIEW");
        var claims = value is null ? Array.Empty<Claim>() : [new Claim(SystemConstants.Claims.Permissions,value)];
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(new ClaimsIdentity(claims,"test")), null);
        await new CatalogPermissionAuthorizationHandler().HandleAsync(context);
        Assert.Equal(allowed,context.HasSucceeded);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Unauthorized_response_is_problem_json_and_does_not_invoke_endpoint(bool challenge)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.TraceIdentifier = "test-correlation";
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        await new CatalogProblemAuthorizationResultHandler().HandleAsync(
            _ => throw new InvalidOperationException("Endpoint must not execute"), context,policy,
            challenge ? PolicyAuthorizationResult.Challenge() : PolicyAuthorizationResult.Forbid());
        Assert.Equal(challenge ? 401 : 403, context.Response.StatusCode);
        Assert.Equal(challenge ? "Bearer" : "",context.Response.Headers.WWWAuthenticate.ToString());
        context.Response.Body.Position=0;
        using var body=await JsonDocument.ParseAsync(context.Response.Body,cancellationToken:TestContext.Current.CancellationToken);
        Assert.Equal(challenge ? 401 : 403,body.RootElement.GetProperty("status").GetInt32());
        Assert.Contains(challenge ? "AUTHENTICATION_REQUIRED" : "FORBIDDEN",body.RootElement.ToString());
    }

    [Fact]
    public async Task Successful_authorization_invokes_endpoint()
    {
        var invoked=false;
        await new CatalogProblemAuthorizationResultHandler().HandleAsync(_ => { invoked=true;return Task.CompletedTask; },
            new DefaultHttpContext(),new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build(),PolicyAuthorizationResult.Success());
        Assert.True(invoked);
    }

    [Fact]
    public async Task Every_catalog_policy_requires_an_authenticated_user_and_permission()
    {
        var services=new ServiceCollection(); services.AddLogging(); services.AddCatalogAdminAuthorization();
        using var provider=services.BuildServiceProvider();
        var policies=provider.GetRequiredService<IAuthorizationPolicyProvider>();
        foreach(var field in typeof(Product.API.Commons.CatalogPermissionPolicies).GetFields()) {
            var policy=await policies.GetPolicyAsync((string)field.GetValue(null)!);
            Assert.NotNull(policy);
            Assert.Contains(policy.Requirements,r=>r is CatalogPermissionRequirement);
            Assert.Contains(policy.Requirements,r=>r is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);
        }
    }
}
