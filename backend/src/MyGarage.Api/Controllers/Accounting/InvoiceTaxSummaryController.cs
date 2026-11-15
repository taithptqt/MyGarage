using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Payments;

namespace MyGarage.Controllers;

[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/v1/invoices/tax-summary")]
public sealed class InvoiceTaxSummaryController(InvoiceTaxSummaryQuery query) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<InvoiceTaxSummary>> Get(CancellationToken cancellationToken)
    {
        if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
        return Ok(await query.ExecuteAsync(cancellationToken, branchId));
    }
}
