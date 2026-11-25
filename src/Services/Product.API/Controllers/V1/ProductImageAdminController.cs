using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Product.API.Commons;
using Product.API.Contracts.V1;
using Product.API.Controllers.Bases;
using Product.API.Services.V1.Interfaces;

namespace Product.API.Controllers.V1;

[Authorize]
[Route("api/v1/admin/products/{id:long}")]
[ApiExplorerSettings(GroupName = "v1")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
public sealed class ProductImageAdminController(
    IProductImageUploadService uploadService,
    IProductImageGalleryService galleryService) : AdminApiControllerBase
{
    [HttpPost("image-uploads")]
    [Authorize(Policy = CatalogPermissionPolicies.Create)]
    [ProducesResponseType<ProductImageUploadResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateImageUploadSession(
        long id,
        [FromBody] CreateProductImageUploadRequest request,
        CancellationToken cancellationToken)
    {
        var response = await uploadService.CreateUploadSessionAsync(id, request, cancellationToken);
        return CreatedAtAction(
            nameof(GetImageUploadStatus),
            new { id, uploadId = response.UploadId },
            response);
    }

    [HttpGet("image-uploads/{uploadId:long}")]
    [Authorize(Policy = CatalogPermissionPolicies.View)]
    [ProducesResponseType<ProductImageUploadStatusResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetImageUploadStatus(
        long id,
        long uploadId,
        CancellationToken cancellationToken)
    {
        var response = await uploadService.GetUploadSessionAsync(id, uploadId, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPut("images")]
    [Authorize(Policy = CatalogPermissionPolicies.Update)]
    [ProducesResponseType<IReadOnlyList<AdminProductImageResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReplaceProductImages(
        long id,
        [FromBody] ReplaceProductImagesRequest request,
        CancellationToken cancellationToken)
    {
        var expectedVersion = ReadIfMatch("product", id);
        var result = await galleryService.ReplaceGalleryAsync(
            id,
            expectedVersion,
            request,
            GetAuditContext(),
            cancellationToken);
        SetProductETag(id, result.ProductVersion);
        return Ok(result.Images);
    }
}
