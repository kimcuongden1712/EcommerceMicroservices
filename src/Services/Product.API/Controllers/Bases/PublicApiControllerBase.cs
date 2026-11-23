using Infrastructure.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Product.API.Commons.Exceptions;

namespace Product.API.Controllers.Bases;

[ApiController]
public abstract class PublicApiControllerBase : ControllerBase
{
    protected ActionResult NotFoundProblem (string detail)
    {
        throw new CatalogAdminException(
            StatusCodes.Status404NotFound,
            "CATALOG_RESOURCE_NOT_FOUND",
            "Catalog resource not found",
            detail);
    }

    protected ActionResult InvalidQuery (string detail)
    {
        throw new BadRequestException(
            detail,
            "INVALID_CATALOG_QUERY",
            title: "Invalid catalog query");
    }
}
