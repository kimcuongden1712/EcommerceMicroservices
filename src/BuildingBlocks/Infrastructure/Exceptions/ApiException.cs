namespace Infrastructure.Exceptions;

public abstract class ApiException(
    int statusCode,
    string code,
    string title,
    string message,
    IReadOnlyDictionary<string, string[]>? fieldErrors = null,
    Exception? innerException = null)
    : ApplicationException(message, innerException)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public string Title { get; } = title;
    public IReadOnlyDictionary<string, string[]>? FieldErrors { get; } = fieldErrors;
}
