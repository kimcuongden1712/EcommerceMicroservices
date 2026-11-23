using FluentValidation;
using ApiValidationException = Infrastructure.Exceptions.ValidationException;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Product.API.Validation;

public sealed class ProductRequestValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        string? code = null;
        string? title = null;

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            var validators = context.HttpContext.RequestServices
                .GetServices(validatorType)
                .OfType<IValidator>();

            foreach (var validator in validators)
            {
                var result = await validator.ValidateAsync(
                    new ValidationContext<object>(argument),
                    context.HttpContext.RequestAborted);
                if (result.IsValid)
                    continue;

                failures.AddRange(result.Errors);
                if (validator is IProductRequestValidator requestValidator)
                {
                    code ??= requestValidator.ErrorCode;
                    title ??= requestValidator.ErrorTitle;
                }
            }
        }

        if (failures.Count > 0)
            throw new ApiValidationException(
                failures,
                code ?? "VALIDATION_FAILED",
                title ?? "Request validation failed");

        await next();
    }
}
