using System.Linq.Expressions;
using FluentValidation;
using Infrastructure.Exceptions;
using Product.API.Commons;
using Product.API.Commons.Enums;
using Product.API.Contracts.V1;

namespace Product.API.Validation;

public interface IProductRequestValidator
{
    string ErrorCode { get; }
    string ErrorTitle { get; }
}

public abstract class ProductRequestValidator<T> : AbstractValidator<T>, IProductRequestValidator
{
    protected ProductRequestValidator(string errorCode, string errorTitle)
    {
        ErrorCode = errorCode;
        ErrorTitle = errorTitle;
    }

    public string ErrorCode { get; }
    public string ErrorTitle { get; }

    protected void AddRequiredText(
        Expression<Func<T, string?>> property,
        string field,
        int maxLength)
    {
        RuleFor(property)
            .Must(value => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength)
            .WithMessage($"{field} is required and must not exceed {maxLength} characters.")
            .OverridePropertyName(field);
    }

    protected void AddSlug(
        Expression<Func<T, string?>> property,
        string field,
        int maxLength)
    {
        RuleFor(property)
            .Must(value =>
            {
                var normalized = value?.Trim().ToLowerInvariant();
                return !string.IsNullOrWhiteSpace(normalized) &&
                    normalized.Length <= maxLength &&
                    Const.SLUGPATTERN.IsMatch(normalized);
            })
            .WithMessage($"{field} must be lowercase ASCII letters, digits, and single hyphens.")
            .OverridePropertyName(field);
    }
}

public sealed class CreateCategoryRequestValidator : ProductRequestValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
        : base("VALIDATION_FAILED", "Catalog validation failed")
    {
        AddRequiredText(request => request.Name, "name", 120);
        AddSlug(request => request.Slug, "slug", 120);
        RuleFor(request => request.SortOrder)
            .Must(value => value.HasValue)
            .WithMessage("sortOrder is required.")
            .OverridePropertyName("sortOrder");
        RuleFor(request => request.SortOrder)
            .Must(value => !value.HasValue || value.Value >= 0)
            .WithMessage("sortOrder must be non-negative.")
            .OverridePropertyName("sortOrder");
        RuleFor(request => request.ParentId)
            .Must(value => !value.HasValue || value.Value > 0)
            .WithMessage("parentId must be a positive ID.")
            .OverridePropertyName("parentId");
    }
}

public sealed class UpdateCategoryRequestValidator : ProductRequestValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
        : base("VALIDATION_FAILED", "Catalog validation failed")
    {
        AddRequiredText(request => request.Name, "name", 120);
        AddSlug(request => request.Slug, "slug", 120);
        RuleFor(request => request.SortOrder)
            .Must(value => value.HasValue)
            .WithMessage("sortOrder is required.")
            .OverridePropertyName("sortOrder");
        RuleFor(request => request.SortOrder)
            .Must(value => !value.HasValue || value.Value >= 0)
            .WithMessage("sortOrder must be non-negative.")
            .OverridePropertyName("sortOrder");
    }
}

public sealed class UpdateCategoryStatusRequestValidator : ProductRequestValidator<UpdateCategoryStatusRequest>
{
    public UpdateCategoryStatusRequestValidator()
        : base("INVALID_CATEGORY_STATUS", "Catalog validation failed")
    {
        RuleFor(request => request.Status)
            .Must(status => Enum.TryParse<CategoryStatus>(status, true, out var parsed) &&
                Enum.IsDefined(parsed))
            .WithMessage("status must be Active or Inactive.")
            .OverridePropertyName("status");
    }
}

public sealed class CreateProductRequestValidator : ProductRequestValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
        : base("VALIDATION_FAILED", "Catalog validation failed")
    {
        AddRequiredText(request => request.Name, "name", 160);
        AddSlug(request => request.Slug, "slug", 160);
        RuleFor(request => request.CategoryId)
            .Must(value => value.HasValue && value.Value > 0)
            .WithMessage("categoryId must be a positive ID.")
            .OverridePropertyName("categoryId");
        RuleFor(request => request.Description)
            .Must(description => description is null || description.Length <= 10_000)
            .WithMessage("description must not exceed 10000 characters.")
            .OverridePropertyName("description");
    }
}

public sealed class UpdateProductRequestValidator : ProductRequestValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
        : base("VALIDATION_FAILED", "Catalog validation failed")
    {
        AddRequiredText(request => request.Name, "name", 160);
        AddSlug(request => request.Slug, "slug", 160);
        RuleFor(request => request.CategoryId)
            .Must(value => value.HasValue && value.Value > 0)
            .WithMessage("categoryId must be a positive ID.")
            .OverridePropertyName("categoryId");
        RuleFor(request => request.Description)
            .Must(description => description is null || description.Length <= 10_000)
            .WithMessage("description must not exceed 10000 characters.")
            .OverridePropertyName("description");
    }
}

public sealed class UpdateProductStatusRequestValidator : ProductRequestValidator<UpdateProductStatusRequest>
{
    public UpdateProductStatusRequestValidator()
        : base("INVALID_PRODUCT_STATUS", "Catalog validation failed")
    {
        RuleFor(request => request.Status)
            .Must(status => Enum.TryParse<ProductStatus>(status, true, out var parsed) &&
                Enum.IsDefined(parsed) &&
                parsed != ProductStatus.Draft)
            .WithMessage("status must be Published or Unpublished.")
            .OverridePropertyName("status");
    }
}

public sealed class CreateProductVariantRequestValidator : ProductRequestValidator<CreateProductVariantRequest>
{
    public CreateProductVariantRequestValidator()
        : base("VALIDATION_FAILED", "Catalog validation failed")
    {
        RuleFor(request => request.Sku)
            .Must(value =>
            {
                var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
                return normalized.Length <= 64 && Const.SKUPATTERN.IsMatch(normalized);
            })
            .WithMessage("sku must contain 1-64 uppercase letters, digits, hyphens, or underscores.")
            .OverridePropertyName("sku");
        RuleFor(request => request.ColorCode)
            .Must(value =>
            {
                var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
                return normalized.Length <= 48 && Const.CODEPATTERN.IsMatch(normalized);
            })
            .WithMessage("colorCode must contain up to 48 uppercase letters, digits, hyphens, or underscores.")
            .OverridePropertyName("colorCode");
        RuleFor(request => request.SizeCode)
            .Must(value =>
            {
                var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
                return normalized.Length <= 32 && Const.CODEPATTERN.IsMatch(normalized);
            })
            .WithMessage("sizeCode must contain up to 32 uppercase letters, digits, hyphens, or underscores.")
            .OverridePropertyName("sizeCode");
        AddRequiredText(request => request.ColorLabel, "colorLabel", 80);
        AddRequiredText(request => request.SizeLabel, "sizeLabel", 80);
        this.AddSellableFieldRules(
            request => request.PriceMinor,
            request => request.WeightG,
            request => request.LengthMm,
            request => request.WidthMm,
            request => request.HeightMm);
    }
}

public sealed class UpdateProductVariantRequestValidator : ProductRequestValidator<UpdateProductVariantRequest>
{
    public UpdateProductVariantRequestValidator()
        : base("VALIDATION_FAILED", "Catalog validation failed")
    {
        AddRequiredText(request => request.ColorLabel, "colorLabel", 80);
        AddRequiredText(request => request.SizeLabel, "sizeLabel", 80);
        this.AddSellableFieldRules(
            request => request.PriceMinor,
            request => request.WeightG,
            request => request.LengthMm,
            request => request.WidthMm,
            request => request.HeightMm);
    }
}

public sealed class UpdateProductVariantStatusRequestValidator : ProductRequestValidator<UpdateProductVariantStatusRequest>
{
    public UpdateProductVariantStatusRequestValidator()
        : base("INVALID_VARIANT_STATUS", "Catalog validation failed")
    {
        RuleFor(request => request.Status)
            .Must(status => Enum.TryParse<ProductVariantStatus>(status, true, out var parsed) &&
                Enum.IsDefined(parsed) &&
                parsed is ProductVariantStatus.Active or ProductVariantStatus.Retired)
            .WithMessage("status must be Active or Retired.")
            .OverridePropertyName("status");
    }
}

public sealed class ProductListQueryRequestValidator : ProductRequestValidator<ProductListQueryRequest>
{
    public ProductListQueryRequestValidator()
        : base("INVALID_CATALOG_QUERY", "Invalid catalog query")
    {
        RuleFor(request => request.Q)
            .Must(value => value is null || value.Trim().Length <= 100)
            .WithMessage("The search term must not exceed 100 characters.")
            .OverridePropertyName("q");
        RuleFor(request => request.CategorySlug)
            .Must(value => value is null || value.Trim().Length <= 120)
            .WithMessage("categorySlug must not exceed 120 characters.")
            .OverridePropertyName("categorySlug");
        RuleFor(request => request.Color)
            .Must(value => value is null || value.Trim().Length <= 48)
            .WithMessage("color must not exceed 48 characters.")
            .OverridePropertyName("color");
        RuleFor(request => request.Size)
            .Must(value => value is null || value.Trim().Length <= 32)
            .WithMessage("size must not exceed 32 characters.")
            .OverridePropertyName("size");
        RuleFor(request => request.MinPriceMinor)
            .Must(value => !value.HasValue || value.Value >= 0)
            .WithMessage("minPriceMinor must be a non-negative integer.")
            .OverridePropertyName("minPriceMinor");
        RuleFor(request => request.MaxPriceMinor)
            .Must(value => !value.HasValue || value.Value >= 0)
            .WithMessage("maxPriceMinor must be a non-negative integer.")
            .OverridePropertyName("maxPriceMinor");
        RuleFor(request => request)
            .Must(request => !request.MinPriceMinor.HasValue ||
                !request.MaxPriceMinor.HasValue ||
                request.MinPriceMinor <= request.MaxPriceMinor)
            .WithMessage("minPriceMinor must not be greater than maxPriceMinor.")
            .OverridePropertyName("minPriceMinor");
        RuleFor(request => request.Sort)
            .Must(value => string.IsNullOrWhiteSpace(value) ||
                value.Trim().Equals("newest", StringComparison.OrdinalIgnoreCase) ||
                value.Trim().Equals("priceAsc", StringComparison.OrdinalIgnoreCase) ||
                value.Trim().Equals("priceDesc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("sort must be one of: newest, priceAsc, priceDesc.")
            .OverridePropertyName("sort");
        RuleFor(request => request.Page)
            .Must(value => !value.HasValue || value.Value > 0)
            .WithMessage("page must be a positive integer.")
            .OverridePropertyName("page");
        RuleFor(request => request.PageSize)
            .Must(value => !value.HasValue || value.Value is > 0 and <= 100)
            .WithMessage("pageSize must be between 1 and 100.")
            .OverridePropertyName("pageSize");
        RuleFor(request => request)
            .Must(request => ((long)(request.Page ?? 1) - 1) * (request.PageSize ?? 24) <= 10_000)
            .WithMessage("The requested page exceeds the maximum offset of 10000.")
            .OverridePropertyName("page");
    }
}

public sealed class AdminProductListQueryRequestValidator : ProductRequestValidator<AdminProductListQueryRequest>
{
    public AdminProductListQueryRequestValidator()
        : base("INVALID_ADMIN_PRODUCT_QUERY", "Invalid catalog query")
    {
        RuleFor(request => request.Q)
            .Must(value => value is null || value.Trim().Length <= 100)
            .WithMessage("q must not exceed 100 characters.")
            .OverridePropertyName("q");
        RuleFor(request => request.CategoryId)
            .Must(value => !value.HasValue || value.Value > 0)
            .WithMessage("categoryId must be a positive integer.")
            .OverridePropertyName("categoryId");
        RuleFor(request => request.Status)
            .Must(value => string.IsNullOrWhiteSpace(value) ||
                (Enum.TryParse<ProductStatus>(value, true, out var parsed) && Enum.IsDefined(parsed)))
            .WithMessage("status must be Draft, Published, or Unpublished.")
            .OverridePropertyName("status");
        RuleFor(request => request.Page)
            .Must(value => !value.HasValue || value.Value > 0)
            .WithMessage("page must be a positive integer.")
            .OverridePropertyName("page");
        RuleFor(request => request.PageSize)
            .Must(value => !value.HasValue || value.Value is > 0 and <= 100)
            .WithMessage("pageSize must be between 1 and 100.")
            .OverridePropertyName("pageSize");
        RuleFor(request => request)
            .Must(request => ((long)(request.Page ?? 1) - 1) * (request.PageSize ?? 24) <= 10_000)
            .WithMessage("The requested page exceeds the maximum offset of 10000.")
            .OverridePropertyName("page");
    }
}

public static class ProductListQueryRequestMapping
{
    public static ProductListQuery ToProductListQuery(this ProductListQueryRequest request)
    {
        var sort = request.Sort?.Trim().ToLowerInvariant() switch
        {
            null or "" or "newest" => ProductSortOrder.Newest,
            "priceasc" => ProductSortOrder.PriceAscending,
            "pricedesc" => ProductSortOrder.PriceDescending,
            _ => throw new BadRequestException(
                "sort must be one of: newest, priceAsc, priceDesc.",
                "INVALID_CATALOG_QUERY",
                new Dictionary<string, string[]> { ["sort"] = ["sort must be one of: newest, priceAsc, priceDesc."] },
                "Invalid catalog query")
        };

        return new ProductListQuery(
            string.IsNullOrWhiteSpace(request.Q) ? null : request.Q.Trim(),
            Normalize(request.CategorySlug, true),
            Normalize(request.Color, false),
            Normalize(request.Size, false),
            request.MinPriceMinor,
            request.MaxPriceMinor,
            sort,
            request.Page ?? 1,
            request.PageSize ?? 24);
    }

    public static AdminProductListQuery ToAdminProductListQuery(this AdminProductListQueryRequest request)
    {
        ProductStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status)) {
            if (!Enum.TryParse<ProductStatus>(request.Status, true, out var parsedStatus) ||
                !Enum.IsDefined(parsedStatus))
                throw new BadRequestException(
                    "status must be Draft, Published, or Unpublished.",
                    "INVALID_ADMIN_PRODUCT_QUERY",
                    new Dictionary<string, string[]> { ["status"] = ["status must be Draft, Published, or Unpublished."] },
                    "Invalid catalog query");
            status = parsedStatus;
        }

        return new AdminProductListQuery(
            string.IsNullOrWhiteSpace(request.Q) ? null : request.Q.Trim(),
            request.CategoryId,
            status,
            request.Page ?? 1,
            request.PageSize ?? 24);
    }

    private static string? Normalize(string? value, bool lowercase)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return lowercase ? trimmed.ToLowerInvariant() : trimmed.ToUpperInvariant();
    }
}

public static class ProductRequestValidationRules
{
    public static void AddSellableFieldRules<T>(
        this ProductRequestValidator<T> validator,
        Expression<Func<T, int?>> priceMinor,
        Expression<Func<T, int?>> weightG,
        Expression<Func<T, int?>> lengthMm,
        Expression<Func<T, int?>> widthMm,
        Expression<Func<T, int?>> heightMm)
    {
        var getPrice = priceMinor.Compile();
        var getWeight = weightG.Compile();
        var getLength = lengthMm.Compile();
        var getWidth = widthMm.Compile();
        var getHeight = heightMm.Compile();
        bool HasSellableField(T request) =>
            getPrice(request).HasValue ||
            getWeight(request).HasValue ||
            getLength(request).HasValue ||
            getWidth(request).HasValue ||
            getHeight(request).HasValue;

        validator.RuleFor(priceMinor)
            .Must((request, value) => !HasSellableField(request) ||
                !value.HasValue ||
                value.Value is > 0 and <= Const.MAXIMUMPRICEMINOR)
            .WithMessage($"priceMinor must be between 1 and {Const.MAXIMUMPRICEMINOR}.")
            .OverridePropertyName("priceMinor");
        AddPositiveIfSellable(validator, weightG, "weightG", HasSellableField);
        AddPositiveIfSellable(validator, lengthMm, "lengthMm", HasSellableField);
        AddPositiveIfSellable(validator, widthMm, "widthMm", HasSellableField);
        AddPositiveIfSellable(validator, heightMm, "heightMm", HasSellableField);
    }

    private static void AddPositiveIfSellable<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, int?>> property,
        string field,
        Func<T, bool> hasSellableField)
    {
        validator.RuleFor(property)
            .Must((request, value) => !hasSellableField(request) || !value.HasValue || value.Value > 0)
            .WithMessage($"{field} must be positive.")
            .OverridePropertyName(field);
    }

}
