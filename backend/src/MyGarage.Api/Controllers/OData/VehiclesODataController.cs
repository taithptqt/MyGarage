using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using MyGarage.Interfaces;

namespace MyGarage.Controllers.OData;

[Authorize]
[Route("odata/Vehicles")]
[Route("api/v1/odata/Vehicles")]
[EnableRateLimiting("odata")]
public sealed class VehiclesODataController(IVehicleODataQuery queries) : ODataController
{
    [HttpGet]
    [SafeEnableQuery]
    public IActionResult Get([FromQuery] bool includeDeleted = false)
    {
        if (!ModelState.IsValid) return BadRequest();
        if (includeDeleted && !User.IsAdminStaff()) return Forbid();
        return Ok(queries.Vehicles(User, includeDeleted));
    }

    [HttpGet("{key:long}")]
    [HttpGet("({key:long})")]
    [SafeEnableQuery]
    public IActionResult Get([FromRoute] long key, [FromQuery] bool includeDeleted = false)
    {
        if (!ModelState.IsValid) return BadRequest();
        if (includeDeleted && !User.IsAdminStaff()) return Forbid();
        return Ok(SingleResult.Create(queries.Vehicles(User, includeDeleted).Where(x => x.Id == key)));
    }
}
