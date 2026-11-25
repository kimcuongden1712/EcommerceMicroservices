using FluentValidation;
using Microsoft.Extensions.Options;
using Product.API.Contracts.V1;
using Product.API.Options;

namespace Product.API.Validation;

public sealed class CreateProductImageUploadRequestValidator : AbstractValidator<CreateProductImageUploadRequest>
{
    public CreateProductImageUploadRequestValidator (IOptions<ProductMediaOptions> options)
    {
        var opts = options.Value;

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("Content type is required.")
            .Must(contentType => contentType is not null &&
                opts.AllowedContentTypes.Contains(contentType.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Content type is not allowed.");

        RuleFor(x => x.ContentLength)
            .GreaterThan(0).WithMessage("Content length must be greater than zero.")
            .LessThanOrEqualTo(opts.MaxFileSizeBytes).WithMessage($"Content length must not exceed {opts.MaxFileSizeBytes} bytes.");
    }
}

public sealed class ReplaceProductImagesRequestValidator : AbstractValidator<ReplaceProductImagesRequest>
{
    public ReplaceProductImagesRequestValidator ()
    {
        RuleFor(x => x.Images)
            .NotNull().WithMessage("Images are required.")
            .Must(images => images is not null && images.Count <= 10)
                .WithMessage("Cannot attach more than 10 images.")
            .Must(images => images is not null && images.Select(image => image.UploadId).Distinct().Count() == images.Count)
                .WithMessage("Duplicate upload IDs are not allowed.");

        RuleForEach(x => x.Images)
            .ChildRules(image => image.RuleFor(item => item.AltText)
                .MaximumLength(160));
    }
}
