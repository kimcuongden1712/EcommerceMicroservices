using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Product.API.Contracts.V1;
using Product.API.Controllers.Bases;
using Product.API.Services.V1.Interfaces;
using Product.API.Validation;

namespace Product.API.Controllers.V1;

[AllowAnonymous]
[Route("api/v1/products")]
[ApiExplorerSettings(GroupName = "v1")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
public sealed class PublicProductsController (IPublicCatalogService catalogService)
    : PublicApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ProductListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductListResponse>> GetProducts (
        [FromQuery] ProductListQueryRequest request,
        CancellationToken cancellationToken)
    {
        var query = request.ToProductListQuery();
        var result = await catalogService.GetProductsAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{slug}")]
    [ProducesResponseType<ProductDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailResponse>> GetProduct (
        string slug,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 160)
            return NotFoundProblem("The requested product is not publicly available.");

        var product = await catalogService.GetProductBySlugAsync(slug, cancellationToken);
        return product is null
            ? NotFoundProblem("The requested product is not publicly available.")
            : Ok(product);
    }
}
