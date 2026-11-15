using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Invoices;

namespace MyGarage.Controllers;

[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/v1/invoices")]
public sealed class InvoiceViewDetailController(InvoiceViewDetailQuery query) : ControllerBase
{
    [HttpGet("{id:long}/view-detail")]
    public async Task<IActionResult> Get(long id, [FromQuery] int? detailsPage, [FromQuery] int? paymentsPage,
        [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return denied!;
        var result = await query.ExecuteAsync(id, detailsPage, paymentsPage, pageSize, cancellationToken, branchId);
        return result == null ? NotFound() : Ok(result);
    }
}
