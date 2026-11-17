using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using MyGarage.Interfaces;

namespace MyGarage.Controllers.OData;

[Authorize(Policy = "StaffOnly")]
[Route("odata/Inventory")]
[Route("api/v1/odata/Inventory")]
[EnableRateLimiting("odata")]
public sealed class InventoryODataController(IInventoryODataQuery queries) : ODataController
{
    [HttpGet]
    [SafeEnableQuery]
    public IActionResult Get() => Ok(queries.Inventory(User));

    [HttpGet("{key:long}")]
    [HttpGet("({key:long})")]
    [SafeEnableQuery]
    public IActionResult Get([FromRoute] long key) =>
        Ok(SingleResult.Create(queries.Inventory(User).Where(x => x.Id == key)));
}
