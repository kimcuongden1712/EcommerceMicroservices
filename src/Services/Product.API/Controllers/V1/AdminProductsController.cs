using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Product.API.Commons;
using Product.API.Commons.Enums;
using Product.API.Contracts.V1;
using Product.API.Controllers.Bases;
using Product.API.Services.V1.Interfaces;
using Product.API.Validation;
using System.Globalization;

namespace Product.API.Controllers.V1;

[Authorize]
[Route("api/v1/admin/products")]
[ApiExplorerSettings(GroupName = "v1")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
public sealed class AdminProductsController (IAdminCatalogService catalogService)
    : AdminApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = CatalogPermissionPolicies.View)]
    [ProducesResponseType<AdminProductListResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProducts (
        [FromQuery] AdminProductListQueryRequest request,
        CancellationToken cancellationToken)
    {
        var query = request.ToAdminProductListQuery();
        return Ok(await catalogService.GetProductsAsync(query, cancellationToken));
    }

    [HttpGet("{id:long}", Name = "AdminProductByIdV1")]
    [Authorize(Policy = CatalogPermissionPolicies.View)]
    [ProducesResponseType<AdminProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProduct (long id, CancellationToken cancellationToken)
    {
        var product = await catalogService.GetProductAsync(id, cancellationToken);
        SetProductETag(id, product.Version);
        return Ok(product);
    }

    [HttpPost]
    [Authorize(Policy = CatalogPermissionPolicies.Create)]
    [ProducesResponseType<AdminProductResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProduct (
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var auditContext = GetAuditContext();
        var product = await catalogService.CreateProductAsync(request, auditContext, cancellationToken);
        var id = long.Parse(product.Id, CultureInfo.InvariantCulture);
        SetProductETag(id, product.Version);
        return CreatedAtRoute("AdminProductByIdV1", new
        {
            id
        }, product);
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = CatalogPermissionPolicies.Update)]
    [ProducesResponseType<AdminProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> UpdateProduct (
        long id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var version = ReadIfMatch("product", id);
        var auditContext = GetAuditContext();
        var product = await catalogService.UpdateProductAsync(
            id,
            version,
            request,
            auditContext,
            cancellationToken);
        SetProductETag(id, product.Version);
        return Ok(product);
    }

    [HttpPut("{id:long}/status")]
    [Authorize(Policy = CatalogPermissionPolicies.Update)]
    [ProducesResponseType<AdminProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> UpdateProductStatus (
        long id,
        [FromBody] UpdateProductStatusRequest request,
        CancellationToken cancellationToken)
    {
        var version = ReadIfMatch("product", id);
        var status = ParseStatus<ProductStatus>(request.Status, "INVALID_PRODUCT_STATUS", "Published or Unpublished");
        var auditContext = GetAuditContext();
        var product = await catalogService.UpdateProductStatusAsync(
            id,
            version,
            status,
            auditContext,
            cancellationToken);
        SetProductETag(id, product.Version);
        return Ok(product);
    }

    [HttpPost("{id:long}/variants")]
    [Authorize(Policy = CatalogPermissionPolicies.Create)]
    [ProducesResponseType<AdminProductVariantDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> CreateVariant (
        long id,
        [FromBody] CreateProductVariantRequest request,
        CancellationToken cancellationToken)
    {
        var version = ReadIfMatch("product", id);
        var auditContext = GetAuditContext();
        var result = await catalogService.CreateVariantAsync(
            id,
            version,
            request,
            auditContext,
            cancellationToken);
        var variantId = long.Parse(result.Variant.Id, CultureInfo.InvariantCulture);
        SetProductETag(id, result.ProductVersion);
        return CreatedAtRoute(
            "AdminProductVariantByIdV1",
            new
            {
                id,
                variantId
            },
            result);
    }

    [HttpGet("{id:long}/variants/{variantId:long}", Name = "AdminProductVariantByIdV1")]
    [Authorize(Policy = CatalogPermissionPolicies.View)]
    [ProducesResponseType<AdminProductVariantDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVariant (
        long id,
        long variantId,
        CancellationToken cancellationToken)
    {
        var result = await catalogService.GetVariantAsync(id, variantId, cancellationToken);
        SetProductETag(id, result.ProductVersion);
        return Ok(result);
    }

    [HttpPut("{id:long}/variants/{variantId:long}")]
    [Authorize(Policy = CatalogPermissionPolicies.Update)]
    [ProducesResponseType<AdminProductVariantDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> UpdateVariant (
        long id,
        long variantId,
        [FromBody] UpdateProductVariantRequest request,
        CancellationToken cancellationToken)
    {
        var version = ReadIfMatch("product", id);
        var auditContext = GetAuditContext();
        var result = await catalogService.UpdateVariantAsync(
            id,
            variantId,
            version,
            request,
            auditContext,
            cancellationToken);
        SetProductETag(id, result.ProductVersion);
        return Ok(result);
    }

    [HttpPut("{id:long}/variants/{variantId:long}/status")]
    [Authorize(Policy = CatalogPermissionPolicies.Update)]
    [ProducesResponseType<AdminProductVariantDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> UpdateVariantStatus (
        long id,
        long variantId,
        [FromBody] UpdateProductVariantStatusRequest request,
        CancellationToken cancellationToken)
    {
        var version = ReadIfMatch("product", id);
        var status = ParseStatus<ProductVariantStatus>(
            request.Status,
            "INVALID_VARIANT_STATUS",
            "Active or Retired");
        var auditContext = GetAuditContext();
        var result = await catalogService.UpdateVariantStatusAsync(
            id,
            variantId,
            version,
            status,
            auditContext,
            cancellationToken);
        SetProductETag(id, result.ProductVersion);
        return Ok(result);
    }
}
