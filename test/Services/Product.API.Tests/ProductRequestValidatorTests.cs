using System.Text.Json;
using FluentValidation;
using Infrastructure.Exceptions;
using Product.API.Contracts.V1;
using Product.API.Options;
using Product.API.Validation;

namespace Product.API.Tests;

public sealed class ProductRequestValidatorTests
{
    public static IEnumerable<object[]> InvalidFields()
    {
        foreach (var type in new[] { typeof(CreateCategoryRequestValidator), typeof(UpdateCategoryRequestValidator), typeof(CreateProductRequestValidator), typeof(UpdateProductRequestValidator) }) {
            var category = type.Name.Contains("Category");
            var max = category ? 120 : 160;
            foreach (var value in new object?[] { null, " ", new string('a', max + 1) }) {
                yield return [type, "Name", value!];
                yield return [type, "Slug", value!];
            }
            foreach (var value in new[] { "bad slug", "-slug", "slug--slug", "slug-", "tiếng-việt" })
                yield return [type, "Slug", value];
            if (category) {
                yield return [type, "SortOrder", null!];
                yield return [type, "SortOrder", -1];
                if (type == typeof(CreateCategoryRequestValidator)) yield return [type, "ParentId", 0L];
            }
            else {
                foreach (var value in new object?[] { null, 0L, -1L }) yield return [type, "CategoryId", value!];
                yield return [type, "Description", new string('x', 10001)];
            }
        }
        foreach (var type in new[] { typeof(CreateProductVariantRequestValidator), typeof(UpdateProductVariantRequestValidator) }) {
            foreach (var field in new[] { "ColorLabel", "SizeLabel" })
                foreach (var value in new object?[] { null, " ", new string('x', 81) }) yield return [type, field, value!];
            foreach (var field in new[] { "PriceMinor", "WeightG", "LengthMm", "WidthMm", "HeightMm" })
                foreach (var value in new[] { 0, -1 }) yield return [type, field, value];
            yield return [type, "PriceMinor", 100000000];
        }
        foreach (var (field, max) in new[] { ("Sku", 64), ("ColorCode", 48), ("SizeCode", 32) })
            foreach (var value in new object?[] { null, "", "bad code!", new string('x', max + 1) }) yield return [typeof(CreateProductVariantRequestValidator), field, value!];
        foreach (var type in new[] { typeof(ProductListQueryRequestValidator), typeof(AdminProductListQueryRequestValidator) }) {
            yield return [type, "Q", new string('q', 101)];
            foreach (var value in new[] { 0, -1 }) {
                yield return [type, "Page", value];
                yield return [type, "PageSize", value];
            }
            yield return [type, "PageSize", 101];
            yield return [type, "Page", 10002];
        }
        foreach (var (field, max) in new[] { ("CategorySlug",120), ("Color",48), ("Size",32) })
            yield return [typeof(ProductListQueryRequestValidator), field, new string('x', max + 1)];
        yield return [typeof(ProductListQueryRequestValidator), "MinPriceMinor", -1];
        yield return [typeof(ProductListQueryRequestValidator), "MaxPriceMinor", -1];
        yield return [typeof(ProductListQueryRequestValidator), "Sort", "invalid"];
        yield return [typeof(AdminProductListQueryRequestValidator), "CategoryId", 0L];
        yield return [typeof(AdminProductListQueryRequestValidator), "Status", "unknown"];
    }

    private static readonly Dictionary<string, object?> Valid = new()
    {
        ["Name"] = " Example ", ["Slug"] = " Example-123 ", ["SortOrder"] = 0, ["CategoryId"] = 1,
        ["Sku"] = " sku-1 ", ["ColorCode"] = " red ", ["SizeCode"] = " m ", ["ColorLabel"] = "Red", ["SizeLabel"] = "Medium",
        ["PriceMinor"] = 1, ["WeightG"] = 1, ["LengthMm"] = 1, ["WidthMm"] = 1, ["HeightMm"] = 1
    };

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public void Invalid_field_is_rejected_without_invalidating_other_fields(Type validatorType, string field, object? value)
    {
        var validator = (IValidator)Activator.CreateInstance(validatorType)!;
        var requestType = validatorType.BaseType!.GenericTypeArguments[0];
        var baseline = JsonSerializer.Deserialize(JsonSerializer.Serialize(Valid), requestType)!;
        Assert.True(validator.Validate(new ValidationContext<object>(baseline)).IsValid);
        var values = new Dictionary<string, object?>(Valid) { [field] = value };
        var request = JsonSerializer.Deserialize(JsonSerializer.Serialize(values), requestType)!;
        var result = validator.Validate(new ValidationContext<object>(request));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName.Equals(field, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(null, false)] [InlineData("unknown", false)] [InlineData("999", false)]
    [InlineData("Active", true)] [InlineData("inactive", true)]
    public void Category_status_is_an_allowed_enum(string? status, bool valid) =>
        Assert.Equal(valid, new UpdateCategoryStatusRequestValidator().Validate(new UpdateCategoryStatusRequest() { Status = status }).IsValid);

    [Theory]
    [InlineData(null, false)] [InlineData("unknown", false)] [InlineData("999", false)] [InlineData("Draft", false)]
    [InlineData("Published", true)] [InlineData("Unpublished", true)]
    public void Product_status_disallows_draft(string? status, bool valid) =>
        Assert.Equal(valid, new UpdateProductStatusRequestValidator().Validate(new UpdateProductStatusRequest() { Status = status }).IsValid);

    [Theory]
    [InlineData(null, false)] [InlineData("unknown", false)] [InlineData("999", false)] [InlineData("Draft", false)]
    [InlineData("Active", true)] [InlineData("Retired", true)]
    public void Variant_status_allows_only_active_and_retired(string? status, bool valid) =>
        Assert.Equal(valid, new UpdateProductVariantStatusRequestValidator().Validate(new UpdateProductVariantStatusRequest() { Status = status }).IsValid);

    [Theory]
    [InlineData(null, ProductSortOrder.Newest)] [InlineData(" ", ProductSortOrder.Newest)] [InlineData("newest", ProductSortOrder.Newest)]
    [InlineData(" priceAsc ", ProductSortOrder.PriceAscending)] [InlineData("PriceDesc", ProductSortOrder.PriceDescending)]
    public void Public_query_normalizes_filters_and_defaults(string? sort, ProductSortOrder expected)
    {
        var request = new ProductListQueryRequest { Q = " shoes ", CategorySlug = " SHOES ", Color = " red ", Size = " xl ", Sort = sort, MinPriceMinor = 1, MaxPriceMinor = 2 };
        Assert.True(new ProductListQueryRequestValidator().Validate(request).IsValid);
        var query = request.ToProductListQuery();
        Assert.Equal(new ProductListQuery("shoes", "shoes", "RED", "XL", 1, 2, expected, 1, 24), query);
        Assert.Equal(new ProductListQuery(null, null, null, null, null, null, ProductSortOrder.Newest, 1, 24), new ProductListQueryRequest().ToProductListQuery());
    }

    [Fact]
    public void Invalid_sort_and_reversed_prices_are_rejected()
    {
        Assert.Throws<BadRequestException>(() => new ProductListQueryRequest { Sort = "other" }.ToProductListQuery());
        Assert.False(new ProductListQueryRequestValidator().Validate(new ProductListQueryRequest() { MinPriceMinor = 2, MaxPriceMinor = 1 }).IsValid);
    }

    [Theory]
    [InlineData(null)] [InlineData(" ")] [InlineData("Published")] [InlineData("Draft")] [InlineData("Unpublished")]
    public void Admin_query_preserves_valid_status_and_pagination(string? status)
    {
        var request = new AdminProductListQueryRequest { Status = status, Q = " shoes ", Page = 2, PageSize = 3, CategoryId = 1 };
        Assert.True(new AdminProductListQueryRequestValidator().Validate(request).IsValid);
        var query = request.ToAdminProductListQuery();
        Assert.Equal("shoes", query.SearchTerm); Assert.Equal(2, query.Page); Assert.Equal(3, query.PageSize);
        Assert.Equal(string.IsNullOrWhiteSpace(status) ? null : status, query.Status?.ToString());
        Assert.Equal(1, new AdminProductListQueryRequest().ToAdminProductListQuery().Page);
        Assert.Null(new AdminProductListQueryRequest { Q = " " }.ToAdminProductListQuery().SearchTerm);
    }

    [Theory]
    [InlineData("other")] [InlineData("999")]
    public void Admin_query_rejects_undefined_status(string status) =>
        Assert.Throws<BadRequestException>(() => new AdminProductListQueryRequest { Status = status }.ToAdminProductListQuery());

    [Theory]
    [InlineData(null, 1, false)] [InlineData("", 1, false)] [InlineData("image/gif",1,false)]
    [InlineData(" IMAGE/PNG ",1,true)] [InlineData("image/jpeg",5242880,true)]
    [InlineData("image/png",0,false)] [InlineData("image/png",5242881,false)]
    public void Upload_validator_checks_type_and_size(string? type, long size, bool valid) =>
        Assert.Equal(valid, new CreateProductImageUploadRequestValidator(Microsoft.Extensions.Options.Options.Create(new ProductMediaOptions()))
            .Validate(new CreateProductImageUploadRequest { ContentType = type, ContentLength = size }).IsValid);

    [Theory]
    [InlineData(0, false, 0, true)] [InlineData(10, false,160,true)] [InlineData(11,false,0,false)]
    [InlineData(2,true,0,false)] [InlineData(1,false,161,false)]
    public void Gallery_validator_checks_count_duplicates_and_alt_text(int count, bool duplicate, int altLength, bool valid)
    {
        var images = Enumerable.Range(0, count).Select(i => new ProductImageItemRequest { UploadId = duplicate ? "1" : (i + 1).ToString(), AltText = new string('a',altLength) }).ToArray();
        Assert.Equal(valid, new ReplaceProductImagesRequestValidator().Validate(new ReplaceProductImagesRequest() { Images = images }).IsValid);
    }

    [Theory]
    [InlineData(null)] [InlineData("PriceMinor")] [InlineData("WeightG")] [InlineData("LengthMm")] [InlineData("WidthMm")] [InlineData("HeightMm")]
    public void Draft_variant_sellable_fields_are_optional_and_may_be_filled_individually(string? field)
    {
        var values=new Dictionary<string,object?>(Valid);
        foreach(var name in new[]{"PriceMinor","WeightG","LengthMm","WidthMm","HeightMm"})values[name]=name==field?1:null;
        var json=JsonSerializer.Serialize(values);
        Assert.True(new CreateProductVariantRequestValidator().Validate(JsonSerializer.Deserialize<CreateProductVariantRequest>(json)!).IsValid);
        Assert.True(new UpdateProductVariantRequestValidator().Validate(JsonSerializer.Deserialize<UpdateProductVariantRequest>(json)!).IsValid);
    }

    [Fact]
    public void Null_gallery_is_rejected_and_query_price_bounds_can_be_independent()
    {
        Assert.False(new ReplaceProductImagesRequestValidator().Validate(new ReplaceProductImagesRequest{Images=null!}).IsValid);
        Assert.True(new ProductListQueryRequestValidator().Validate(new ProductListQueryRequest{MinPriceMinor=0}).IsValid);
        Assert.True(new ProductListQueryRequestValidator().Validate(new ProductListQueryRequest{MaxPriceMinor=100}).IsValid);
    }
}
