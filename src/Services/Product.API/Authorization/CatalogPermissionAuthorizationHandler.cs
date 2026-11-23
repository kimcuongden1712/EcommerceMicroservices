using Microsoft.AspNetCore.Authorization;
using Shared.Common.Constants;
using System.Text.Json;

namespace Product.API.Authorization
{
    public sealed class CatalogPermissionAuthorizationHandler
    : AuthorizationHandler<CatalogPermissionRequirement>
    {
        protected override Task HandleRequirementAsync (
            AuthorizationHandlerContext context,
            CatalogPermissionRequirement requirement)
        {
            foreach (var claim in context.User.FindAll(SystemConstants.Claims.Permissions)) {
                if (string.Equals(claim.Value, requirement.Permission, StringComparison.Ordinal)) {
                    context.Succeed(requirement);
                    return Task.CompletedTask;
                }

                try {
                    var permissions = JsonSerializer.Deserialize<HashSet<string>>(claim.Value);
                    if (permissions?.Contains(requirement.Permission) == true) {
                        context.Succeed(requirement);
                        return Task.CompletedTask;
                    }
                }
                catch (JsonException) {
                    // Invalid permission claim values do not grant access.
                }
            }

            return Task.CompletedTask;
        }
    }

}
