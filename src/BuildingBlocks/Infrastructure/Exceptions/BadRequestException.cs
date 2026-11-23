using Microsoft.AspNetCore.Http;

namespace Infrastructure.Exceptions;

public class BadRequestException : ApiException
{
    public BadRequestException (string message)
        : this(message, "BAD_REQUEST")
    {
    }

    public BadRequestException (
        string message,
        string code,
        IReadOnlyDictionary<string, string[]>? errors = null,
        string title = "Bad request")
        : base(StatusCodes.Status400BadRequest, code, title, message, errors)
    {
    }

    public IReadOnlyDictionary<string, string[]>? Errors => FieldErrors;
}
