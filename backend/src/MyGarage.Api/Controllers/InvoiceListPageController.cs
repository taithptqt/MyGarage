using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Payments;

namespace MyGarage.Controllers;

[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/invoices/list-page")]
[Route("api/v1/invoices/list-page")]
public sealed class InvoiceListPageController(InvoiceListPageQuery query) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<InvoiceListPage>> Get(string? search, string? status, string? source, int? page, int? pageSize, CancellationToken cancellationToken)
        => Ok(await query.ExecuteAsync(search, status, source, page, pageSize, cancellationToken));
}
