using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using MyGarage.Interfaces;

namespace MyGarage.Controllers.OData;

[Authorize]
[Route("odata/Appointments")]
[Route("api/v1/odata/Appointments")]
[EnableRateLimiting("odata")]
public sealed class AppointmentsODataController(IODataQueryService queries) : ODataController
{
    [HttpGet]
    [SafeEnableQuery]
    public IActionResult Get() => Ok(queries.Appointments(User));

    [HttpGet("{key:long}")]
    [HttpGet("({key:long})")]
    [SafeEnableQuery]
    public IActionResult Get([FromRoute] long key) =>
        Ok(SingleResult.Create(queries.Appointments(User).Where(x => x.Id == key)));
}
