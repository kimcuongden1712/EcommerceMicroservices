using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Product.API.Contracts.V1;
using Product.API.Controllers.Bases;
using Product.API.Services.V1.Interfaces;

namespace Product.API.Controllers.V1;

[AllowAnonymous]
[Route("api/v1/categories")]
[ApiExplorerSettings(GroupName = "v1")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
public sealed class PublicCategoriesController(IPublicCatalogService catalogService)
    : PublicApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CategoryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> GetCategories(
        CancellationToken cancellationToken)
    {
        var categories = await catalogService.GetCategoriesAsync(cancellationToken);
        return Ok(categories);
    }
}
