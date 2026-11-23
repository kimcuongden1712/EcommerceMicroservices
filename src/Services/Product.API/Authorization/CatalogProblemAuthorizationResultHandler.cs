using Infrastructure.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Product.API.Authorization
{
    public sealed class CatalogProblemAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
    {
        private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

        public Task HandleAsync (
            RequestDelegate next,
            HttpContext context,
            AuthorizationPolicy policy,
            PolicyAuthorizationResult authorizeResult)
        {
            if (!authorizeResult.Challenged && !authorizeResult.Forbidden)
                return _defaultHandler.HandleAsync(next, context, policy, authorizeResult);

            var statusCode = authorizeResult.Challenged
                ? StatusCodes.Status401Unauthorized
                : StatusCodes.Status403Forbidden;
            var code = authorizeResult.Challenged
                ? "AUTHENTICATION_REQUIRED"
                : "FORBIDDEN";
            var problem = ApiExceptionProblemDetails.Create(
                statusCode,
                code,
                authorizeResult.Challenged ? "Unauthorized" : "Forbidden",
                authorizeResult.Challenged
                    ? "Authentication is required to access this resource."
                    : "The authenticated identity does not have permission to access this resource.",
                context.TraceIdentifier);

            context.Response.StatusCode = statusCode;
            if (authorizeResult.Challenged)
                context.Response.Headers.WWWAuthenticate = "Bearer";

            return ApiExceptionProblemDetails.WriteAsync(
                context.Response,
                problem,
                context.RequestAborted);
        }
    }

}
