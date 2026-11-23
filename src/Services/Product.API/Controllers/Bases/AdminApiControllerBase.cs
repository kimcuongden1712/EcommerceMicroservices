using Infrastructure.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Product.API.Commons.Exceptions;
using Product.API.Services.V1.Interfaces;
using Shared.Common.Constants;
using System.Diagnostics;
using System.Globalization;

namespace Product.API.Controllers.Bases;

[ApiController]
public abstract class AdminApiControllerBase : ControllerBase
{
    protected long ReadIfMatch (string resource, long id)
    {
        var value = Request.Headers["If-Match"].ToString();
        if (string.IsNullOrWhiteSpace(value))
            throw new CatalogAdminException(
                StatusCodes.Status428PreconditionRequired,
                "PRECONDITION_REQUIRED",
                "Precondition required",
                "If-Match is required.");

        var prefix = $"\"{resource}-{id}-v";
        if (!value.StartsWith(prefix, StringComparison.Ordinal) ||
            !value.EndsWith("\"", StringComparison.Ordinal) ||
            !long.TryParse(
                value.AsSpan(prefix.Length, value.Length - prefix.Length - 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var version) ||
            version < 1)
            throw new CatalogAdminException(
                StatusCodes.Status412PreconditionFailed,
                "VERSION_MISMATCH",
                "Precondition failed",
                "If-Match does not contain a valid current resource ETag.");

        return version;
    }

    protected CatalogAuditContext GetAuditContext ()
    {
        var actorId = User.FindFirst("sub")?.Value ??
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
            User.FindFirst(SystemConstants.Claims.UserId)?.Value;
        if (string.IsNullOrWhiteSpace(actorId) || actorId.Length > 128)
            throw new CatalogAdminException(
                StatusCodes.Status403Forbidden,
                "CATALOG_ACTOR_REQUIRED",
                "Forbidden",
                "The authenticated identity must provide a valid subject claim.");

        var correlationId = Request.Headers["X-Correlation-ID"].ToString();
        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 128)
            correlationId = Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;

        return new CatalogAuditContext(actorId, correlationId);
    }

    protected void SetCategoryETag (long id, long version) =>
        Response.Headers["ETag"] = $"\"category-{id}-v{version}\"";

    protected void SetProductETag (long id, long version) =>
        Response.Headers["ETag"] = $"\"product-{id}-v{version}\"";

    protected static TStatus ParseStatus<TStatus> (
        string? value,
        string code,
        string allowedValues)
        where TStatus : struct, Enum
    {
        if (Enum.TryParse<TStatus>(value, true, out var status) && Enum.IsDefined(status))
            return status;

        throw new BadRequestException(
            $"status must be one of: {allowedValues}.",
            code,
            new Dictionary<string, string[]> { ["status"] = [$"status must be one of: {allowedValues}."] },
            "Catalog validation failed");
    }
}
