using Infrastructure.Extensions;
using Microsoft.AspNetCore.Authorization;
using Product.API.Commons;
using Shared.Common.Constants;

namespace Product.API.Authorization;

public static class CatalogAdminAuthorization
{
    public static IServiceCollection AddCatalogAdminAuthorization (this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, CatalogPermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, CatalogProblemAuthorizationResultHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(CatalogPermissionPolicies.View, policy =>
                policy.RequireAuthenticatedUser()
                    .AddRequirements(new CatalogPermissionRequirement(
                        PermissionHelper.GetPermission(FunctionCode.PRODUCT, CommandCode.VIEW))));
            options.AddPolicy(CatalogPermissionPolicies.Create, policy =>
                policy.RequireAuthenticatedUser()
                    .AddRequirements(new CatalogPermissionRequirement(
                        PermissionHelper.GetPermission(FunctionCode.PRODUCT, CommandCode.CREATE))));
            options.AddPolicy(CatalogPermissionPolicies.Update, policy =>
                policy.RequireAuthenticatedUser()
                    .AddRequirements(new CatalogPermissionRequirement(
                        PermissionHelper.GetPermission(FunctionCode.PRODUCT, CommandCode.UPDATE))));
            options.AddPolicy(CatalogPermissionPolicies.Delete, policy =>
                policy.RequireAuthenticatedUser()
                    .AddRequirements(new CatalogPermissionRequirement(
                        PermissionHelper.GetPermission(FunctionCode.PRODUCT, CommandCode.DELETE))));
        });

        return services;
    }
}

public sealed record CatalogPermissionRequirement (string Permission) : IAuthorizationRequirement;
