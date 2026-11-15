using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Catalog;

namespace MyGarage.Controllers;

[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/service-categories/admin-list")]
[Route("api/v1/service-categories/admin-list")]
public sealed class ServiceCategoryAdministrationController : ControllerBase
{
    private readonly ServiceCategoryPageQuery _query;

    public ServiceCategoryAdministrationController(ServiceCategoryPageQuery query)
    {
        _query = query;
    }

    [HttpGet]
    public async Task<IActionResult> GetPage([FromQuery] string? search, [FromQuery] int? page,
        [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return Ok(await _query.ExecuteAsync(search, page, pageSize, cancellationToken));
    }
}
