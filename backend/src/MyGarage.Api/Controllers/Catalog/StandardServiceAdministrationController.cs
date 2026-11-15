using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Catalog;

namespace MyGarage.Controllers;

[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/v1/standard-services/admin-list")]
public sealed class StandardServiceAdministrationController(StandardServicePageQuery query) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPage([FromQuery] string? search, [FromQuery] int? page,
        [FromQuery] int? pageSize, CancellationToken cancellationToken, [FromQuery] bool nameCodeOnly = false)
        => Ok(await query.ExecuteAsync(search, page, pageSize, nameCodeOnly, cancellationToken));
}
