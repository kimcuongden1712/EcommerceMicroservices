using Infrastructure.Exceptions;
using Microsoft.AspNetCore.Http;

namespace Product.API.Commons.Exceptions;

public sealed class ProductMediaException : ApiException
{
    public ProductMediaException (
        string code,
        string detail,
        CancellationToken cancellationToken = default)
        : base(StatusCodes.Status400BadRequest, code, "Product media error", detail)
    {
    }

    public ProductMediaException(
        int statusCode,
        string code,
        string title,
        string detail,
        Exception? innerException = null)
        : base(statusCode, code, title, detail, innerException: innerException)
    {
    }
}
