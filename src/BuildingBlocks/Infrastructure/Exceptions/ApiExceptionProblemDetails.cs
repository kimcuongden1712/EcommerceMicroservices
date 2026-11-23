using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Infrastructure.Exceptions;

public static class ApiExceptionProblemDetails
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ProblemDetails Create(ApiException exception, string traceId)
    {
        return Create(
            exception.StatusCode,
            exception.Code,
            exception.Title,
            exception.Message,
            traceId,
            exception.FieldErrors);
    }

    public static ProblemDetails Create(
        int statusCode,
        string code,
        string title,
        string detail,
        string traceId,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = "about:blank"
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = traceId;
        if (errors is not null)
            problem.Extensions["errors"] = errors;

        return problem;
    }

    public static Task WriteAsync(
        HttpResponse response,
        ProblemDetails problem,
        CancellationToken cancellationToken)
    {
        response.ContentType = "application/problem+json";
        return JsonSerializer.SerializeAsync(
            response.Body,
            problem,
            JsonOptions,
            cancellationToken);
    }
}
