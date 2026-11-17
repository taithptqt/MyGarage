using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using MyGarage.Interfaces;

namespace MyGarage.Controllers.OData;

[Authorize(Policy = "AuditLogsODataAccess")]
[Route("odata/AuditLogs")]
[Route("api/v1/odata/AuditLogs")]
[EnableRateLimiting("odata")]
public sealed class AuditLogsODataController(IAuditLogODataQuery queries) : ODataController
{
    [HttpGet]
    [SafeEnableQuery]
    public IActionResult Get() => Ok(queries.AuditLogs(User));

    [HttpGet("{key:long}")]
    [HttpGet("({key:long})")]
    [SafeEnableQuery]
    public IActionResult Get([FromRoute] long key) =>
        Ok(SingleResult.Create(queries.AuditLogs(User).Where(x => x.Id == key)));
}
