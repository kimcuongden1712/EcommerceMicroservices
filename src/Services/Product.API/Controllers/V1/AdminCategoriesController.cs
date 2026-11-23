using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Product.API.Commons;
using Product.API.Commons.Enums;
using Product.API.Contracts.V1;
using Product.API.Controllers.Bases;
using Product.API.Services.V1.Interfaces;

namespace Product.API.Controllers.V1;

[Authorize]
[Route("api/v1/admin/categories")]
[ApiExplorerSettings(GroupName = "v1")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
public sealed class AdminCategoriesController (IAdminCatalogService catalogService)
    : AdminApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = CatalogPermissionPolicies.View)]
    [ProducesResponseType<IReadOnlyList<AdminCategoryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories (CancellationToken cancellationToken)
    {
        return Ok(await catalogService.GetCategoriesAsync(cancellationToken));
    }

    [HttpGet("{id:long}", Name = "AdminCategoryByIdV1")]
    [Authorize(Policy = CatalogPermissionPolicies.View)]
    [ProducesResponseType<AdminCategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategory (long id, CancellationToken cancellationToken)
    {
        var category = await catalogService.GetCategoryAsync(id, cancellationToken);
        SetCategoryETag(id, category.Version);
        return Ok(category);
    }

    [HttpPost]
    [Authorize(Policy = CatalogPermissionPolicies.Create)]
    [ProducesResponseType<AdminCategoryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCategory (
        [FromBody] CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var auditContext = GetAuditContext();
        var category = await catalogService.CreateCategoryAsync(request, auditContext, cancellationToken);
        var id = long.Parse(category.Id, System.Globalization.CultureInfo.InvariantCulture);
        SetCategoryETag(id, category.Version);
        return CreatedAtRoute("AdminCategoryByIdV1", new
        {
            id
        }, category);
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = CatalogPermissionPolicies.Update)]
    [ProducesResponseType<AdminCategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> UpdateCategory (
        long id,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var version = ReadIfMatch("category", id);
        var auditContext = GetAuditContext();
        var category = await catalogService.UpdateCategoryAsync(
            id,
            version,
            request,
            auditContext,
            cancellationToken);
        SetCategoryETag(id, category.Version);
        return Ok(category);
    }

    [HttpPut("{id:long}/status")]
    [Authorize(Policy = CatalogPermissionPolicies.Update)]
    [ProducesResponseType<AdminCategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> UpdateCategoryStatus (
        long id,
        [FromBody] UpdateCategoryStatusRequest request,
        CancellationToken cancellationToken)
    {
        var version = ReadIfMatch("category", id);
        var status = ParseStatus<CategoryStatus>(request.Status, "INVALID_CATEGORY_STATUS", "Active or Inactive");
        var auditContext = GetAuditContext();
        var category = await catalogService.UpdateCategoryStatusAsync(
            id,
            version,
            status,
            auditContext,
            cancellationToken);
        SetCategoryETag(id, category.Version);
        return Ok(category);
    }
}
