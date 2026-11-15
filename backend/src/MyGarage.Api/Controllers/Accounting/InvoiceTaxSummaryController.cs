using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Payments;

namespace MyGarage.Controllers;

// Matches the existing staff-wide GET /api/invoices contract; branch scope remains a separate decision.
[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/invoices/tax-summary")]
[Route("api/v1/invoices/tax-summary")]
public sealed class InvoiceTaxSummaryController(InvoiceTaxSummaryQuery query) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<InvoiceTaxSummary>> Get(CancellationToken cancellationToken)
        => Ok(await query.ExecuteAsync(cancellationToken));
}
