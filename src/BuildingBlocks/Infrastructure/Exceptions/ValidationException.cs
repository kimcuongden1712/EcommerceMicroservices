using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Exceptions;

public class ValidationException : ApiException
{
    public ValidationException()
        : this(Array.Empty<ValidationFailure>())
    {
    }

    public ValidationException(IEnumerable<ValidationFailure> failures)
        : this(failures, "VALIDATION_FAILED", "Validation failed")
    {
    }

    public ValidationException(
        IEnumerable<ValidationFailure> failures,
        string code,
        string title)
        : this(
            failures
                .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
                .ToDictionary(failureGroup => failureGroup.Key, failureGroup => failureGroup.ToArray()),
            code,
            title)
    {
    }

    private ValidationException(IDictionary<string, string[]> errors, string code, string title)
        : base(
            StatusCodes.Status400BadRequest,
            code,
            title,
            "One or more validation failures have occurred.",
            new Dictionary<string, string[]>(errors))
    {
        Errors = errors;
    }

    public IDictionary<string, string[]> Errors { get; }
}