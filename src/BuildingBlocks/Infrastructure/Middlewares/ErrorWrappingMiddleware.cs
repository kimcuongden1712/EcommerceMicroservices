using Infrastructure.Exceptions;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Infrastructure.Middlewares;

public class ErrorWrappingMiddleware
{
    private readonly ILogger _logger;
    private readonly RequestDelegate _next;

    public ErrorWrappingMiddleware(RequestDelegate next, ILogger logger)
    {
        _next = next;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next.Invoke(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            return;
        }
        catch (ApiException ex)
        {
            _logger.Warning(ex, "API request failed with {Code}", ex.Code);
            if (context.Response.HasStarted)
                throw;

            context.Response.Clear();
            context.Response.StatusCode = ex.StatusCode;
            var problem = ApiExceptionProblemDetails.Create(ex, context.TraceIdentifier);
            await ApiExceptionProblemDetails.WriteAsync(
                context.Response,
                problem,
                context.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, ex.Message);
            if (context.Response.HasStarted)
                throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            var problem = ApiExceptionProblemDetails.Create(
                StatusCodes.Status500InternalServerError,
                "INTERNAL_SERVER_ERROR",
                "Internal server error",
                "An unexpected error occurred.",
                context.TraceIdentifier);
            await ApiExceptionProblemDetails.WriteAsync(
                context.Response,
                problem,
                context.RequestAborted);
        }
    }
}