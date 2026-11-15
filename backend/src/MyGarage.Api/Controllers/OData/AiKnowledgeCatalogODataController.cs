using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using MyGarage.Application.Ai;

namespace MyGarage.Controllers.OData;

[Authorize(Policy = "AdminStaffOnly")]
[Route("odata/AiKnowledgeCatalog")]
[Route("api/v1/odata/AiKnowledgeCatalog")]
[EnableRateLimiting("odata")]
public sealed class AiKnowledgeCatalogODataController(IAiKnowledgeCatalogQuery query) : ODataController
{
    [HttpGet]
    [SafeEnableQuery]
    public IActionResult Get() => Ok(query.Execute());

    [HttpGet("{key:long}")]
    [HttpGet("({key:long})")]
    [SafeEnableQuery]
    public IActionResult Get([FromRoute] long key) =>
        Ok(SingleResult.Create(query.Execute().Where(x => x.Id == key)));
}
