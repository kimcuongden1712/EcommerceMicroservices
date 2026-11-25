using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Product.API.Services.V1.Interfaces;

namespace Product.API.Controllers.V1;

[AllowAnonymous]
[ApiController]
[Route("api/v1/media")]
[ApiExplorerSettings(GroupName = "v1")]
public sealed class ProductImageDeliveryController(IProductImageDeliveryService deliveryService) : ControllerBase
{
    [HttpGet("{assetKey}")]
    [Produces("image/jpeg", "image/png", "image/webp")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetImage(string assetKey, CancellationToken cancellationToken)
    {
        var image = await deliveryService.GetPublicImageMetadataAsync(assetKey, cancellationToken);
        if (image is null)
            return NotFound();

        var stream = await deliveryService.OpenPublicImageStreamAsync(
            image.AssetKey,
            image.ContentLength,
            cancellationToken);
        Response.Headers.CacheControl = "public, max-age=0, must-revalidate";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Length"] = image.ContentLength.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return File(stream, image.ContentType, enableRangeProcessing: false);
    }
}
