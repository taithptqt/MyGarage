using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Catalog;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers;

[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/v1/service-boms")]
public class ServiceBomsController : ControllerBase
{
    private readonly ICatalogApplicationService _catalogService;

    public ServiceBomsController(ICatalogApplicationService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ServiceBomResponseDto>>> GetAll(
        [FromQuery] long serviceId,
        [FromQuery] long? vehicleModelId = null,
        CancellationToken ct = default)
    {
        var rows = await _catalogService.GetServiceBomsAsync(serviceId, vehicleModelId, ct);
        return Ok(rows);
    }
}
