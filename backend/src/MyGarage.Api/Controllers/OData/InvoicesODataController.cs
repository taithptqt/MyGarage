using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using MyGarage.Application.Payments;

namespace MyGarage.Controllers.OData;

[Authorize]
[Route("odata/Invoices")]
[Route("api/v1/odata/Invoices")]
[EnableRateLimiting("odata")]
public sealed class InvoicesODataController(IInvoiceQuery query) : ODataController
{
    [HttpGet]
    [SafeEnableQuery]
    public IActionResult Get() => Ok(query.Execute(ResolveScope()));

    [HttpGet("{key:long}")]
    [HttpGet("({key:long})")]
    [SafeEnableQuery]
    public IActionResult Get([FromRoute] long key) =>
        Ok(SingleResult.Create(query.Execute(ResolveScope()).Where(x => x.Id == key)));

    private InvoiceScope ResolveScope() => User.IsStaffUser()
        ? (User.IsAdminStaff() ? new InvoiceScope(InvoiceScopeKind.All, null) : new InvoiceScope(InvoiceScopeKind.Branch, User.GetCurrentBranchId()))
        : string.Equals(User.GetCurrentRole(), "Customer", StringComparison.OrdinalIgnoreCase)
            ? new InvoiceScope(InvoiceScopeKind.Customer, User.GetCurrentUserId())
            : new InvoiceScope(InvoiceScopeKind.None, null);
}
