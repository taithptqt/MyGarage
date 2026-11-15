using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Invoices;

namespace MyGarage.Controllers;

// Mirrors the existing staff-only invoice read scope pending a separate branch-policy decision.
[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/invoices")]
[Route("api/v1/invoices")]
public sealed class InvoiceViewDetailController(InvoiceViewDetailQuery query) : ControllerBase
{
    [HttpGet("{id:long}/view-detail")]
    public async Task<IActionResult> Get(long id, [FromQuery] int? detailsPage, [FromQuery] int? paymentsPage,
        [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        var result = await query.ExecuteAsync(id, detailsPage, paymentsPage, pageSize, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }
}
