using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using MyGarage.Interfaces;

namespace MyGarage.Controllers.OData;

[Authorize]
[Route("odata/Quotations")]
[Route("api/v1/odata/Quotations")]
[EnableRateLimiting("odata")]
public sealed class QuotationsODataController(IQuotationODataQuery queries) : ODataController
{
    [HttpGet]
    [SafeEnableQuery]
    public IActionResult Get() => Ok(queries.Quotations(User));

    [HttpGet("{key:long}")]
    [HttpGet("({key:long})")]
    [SafeEnableQuery]
    public IActionResult Get([FromRoute] long key) =>
        Ok(SingleResult.Create(queries.Quotations(User).Where(x => x.Id == key)));
}
