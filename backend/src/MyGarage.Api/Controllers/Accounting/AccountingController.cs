using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Accounting;

namespace MyGarage.Controllers;

[Route("api/v1/accounting")]
[ApiController]
[Authorize]
public partial class AccountingController : ControllerBase
{
    private readonly IAccountingCommandFacade _commandFacade;

    public AccountingController(IAccountingCommandFacade commandFacade)
    {
        _commandFacade = commandFacade;
    }

    [HttpGet("table/{tableKey}")]
    public async Task<IActionResult> GetTable(string tableKey, [FromQuery] int limit = 500)
    {
        if (!TryGetTableAccess(tableKey, out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.GetTableAsync(tableKey, limit, access.BranchId));
    }

    [HttpGet("table/{tableKey}/{id:long}")]
    public async Task<IActionResult> GetById(string tableKey, long id)
    {
        if (!TryGetTableAccess(tableKey, out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.GetTableRowByIdAsync(tableKey, id, access.BranchId));
    }

    [HttpPost("table/{tableKey}")]
    public async Task<IActionResult> Create(string tableKey, [FromBody] JsonElement payload)
    {
        if (!TryGetTableAccess(tableKey, out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.CreateTableRowAsync(tableKey, payload, access.BranchId));
    }

    [HttpPut("table/{tableKey}/{id:long}")]
    public async Task<IActionResult> Update(string tableKey, long id, [FromBody] JsonElement payload)
    {
        if (!TryGetTableAccess(tableKey, out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.UpdateTableRowAsync(tableKey, id, payload, access.BranchId));
    }

    [HttpDelete("table/{tableKey}/{id:long}")]
    public async Task<IActionResult> Delete(string tableKey, long id)
    {
        if (!TryGetTableAccess(tableKey, out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.DeleteTableRowAsync(tableKey, id, access.BranchId));
    }

    [HttpPost("supplier-invoices")]
    public async Task<IActionResult> CreateSupplierInvoice([FromBody] JsonElement payload)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.CreateSupplierInvoiceAsync(payload, access.BranchId));
    }

    [HttpPut("supplier-invoices/{id:long}")]
    public async Task<IActionResult> UpdateSupplierInvoice(long id, [FromBody] JsonElement payload)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.UpdateSupplierInvoiceAsync(id, payload, access.BranchId));
    }

    [HttpPost("supplier-invoice-details")]
    public async Task<IActionResult> CreateSupplierInvoiceDetail([FromBody] JsonElement payload)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.CreateSupplierInvoiceDetailAsync(payload, access.BranchId));
    }

    [HttpPut("supplier-invoice-details/{id:long}")]
    public async Task<IActionResult> UpdateSupplierInvoiceDetail(long id, [FromBody] JsonElement payload)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.UpdateSupplierInvoiceDetailAsync(id, payload, access.BranchId));
    }
}
