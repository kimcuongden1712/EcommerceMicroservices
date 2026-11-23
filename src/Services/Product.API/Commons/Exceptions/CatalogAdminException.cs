using Infrastructure.Exceptions;

namespace Product.API.Commons.Exceptions;

public sealed class CatalogAdminException : ApiException
{
    public CatalogAdminException (
        int statusCode,
        string code,
        string title,
        string detail,
        IReadOnlyDictionary<string, string[]>? errors = null,
        Exception? innerException = null)
        : base(statusCode, code, title, detail, errors, innerException)
    {
    }

    public IReadOnlyDictionary<string, string[]>? Errors => FieldErrors;

    public static CatalogAdminException NotFound (string resource)
    {
        return new CatalogAdminException(
            StatusCodes.Status404NotFound,
            "CATALOG_RESOURCE_NOT_FOUND",
            "Catalog resource not found",
            $"The requested {resource} was not found.");
    }

    public static CatalogAdminException Conflict (string code, string detail)
    {
        return new CatalogAdminException(
            StatusCodes.Status409Conflict,
            code,
            "Catalog conflict",
            detail);
    }

    public static CatalogAdminException PreconditionFailed ()
    {
        return new CatalogAdminException(
            StatusCodes.Status412PreconditionFailed,
            "VERSION_MISMATCH",
            "Catalog version mismatch",
            "The resource changed after it was read. Fetch it again and retry with its current ETag.");
    }
}
