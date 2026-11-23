using HealthChecks.UI.Client;
using Infrastructure.Exceptions;
using Infrastructure.Middlewares;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Product.API.Extensions;

public static class ApplicationExtensions
{
    public static void UseInfrastructure (this IApplicationBuilder app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.OAuthClientId("microservices_swagger");
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Product API");
            c.DisplayRequestDuration();
        });
        app.UseMiddleware<ErrorWrappingMiddleware>();
        app.UseStatusCodePages(async statusCodeContext =>
        {
            var context = statusCodeContext.HttpContext;
            var (code, title, detail) = context.Response.StatusCode switch
            {
                StatusCodes.Status400BadRequest =>
                    ("BAD_REQUEST", "Bad request", "The request could not be processed."),
                StatusCodes.Status401Unauthorized =>
                    ("AUTHENTICATION_REQUIRED", "Unauthorized", "Authentication is required."),
                StatusCodes.Status403Forbidden =>
                    ("FORBIDDEN", "Forbidden", "The request is not permitted."),
                StatusCodes.Status404NotFound =>
                    ("NOT_FOUND", "Not found", "The requested resource was not found."),
                StatusCodes.Status405MethodNotAllowed =>
                    ("METHOD_NOT_ALLOWED", "Method not allowed", "The HTTP method is not supported for this resource."),
                _ => ("HTTP_ERROR", "Request failed", "The request could not be completed.")
            };
            var problem = ApiExceptionProblemDetails.Create(
                context.Response.StatusCode,
                code,
                title,
                detail,
                context.TraceIdentifier);
            await ApiExceptionProblemDetails.WriteAsync(
                context.Response,
                problem,
                context.RequestAborted);
        });
        app.UseRouting();
        app.UseAuthentication();
        // app.UseHttpsRedirection(); //for production only
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("live")
            });
            endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready"),
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });
            endpoints.MapHealthChecks("/hc", new HealthCheckOptions
            {
                Predicate = _ => true,
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });
            endpoints.MapDefaultControllerRoute();
        });
    }
}