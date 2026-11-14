using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using MyGarage.Application.Ai;

namespace MyGarage.Controllers.OData;

[Authorize(Policy = "AdminStaffOnly")]
[Route("odata/AiKnowledgeCatalog")]
[EnableRateLimiting("odata")]
public sealed class AiKnowledgeCatalogODataController(AiKnowledgeCatalogQuery query) : ODataController
{
    [HttpGet]
    [SafeEnableQuery]
    public IActionResult Get() => Ok(query.Execute());
}
