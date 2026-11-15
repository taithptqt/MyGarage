using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Accounting;

namespace MyGarage.Controllers;

public partial class AccountingController : ControllerBase
{
    [HttpGet("reports/trial-balance")]
    public async Task<IActionResult> TrialBalance()
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.GetTrialBalanceAsync(access.BranchId));
    }

    [HttpGet("reports/supplier-payable-summary")]
    public async Task<IActionResult> SupplierPayableSummary()
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.GetSupplierPayableSummaryAsync(access.BranchId));
    }

    [HttpGet("reports/payroll-run-summary")]
    public async Task<IActionResult> PayrollRunSummary()
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.GetPayrollRunSummaryAsync(access.BranchId));
    }

    [HttpGet("payroll-runs/{id:long}/commission-reconciliation")]
    public async Task<IActionResult> PayrollRunCommissionReconciliation(long id)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.GetPayrollRunCommissionReconciliationAsync(
            id,
            access.BranchId,
            HttpContext.RequestAborted));
    }

    [HttpGet("staff-salary/{staffId:long}/base")]
    public async Task<IActionResult> StaffBaseSalary(long staffId, [FromQuery] DateTime? asOfDate = null)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.GetStaffBaseSalaryAsync(
            staffId,
            asOfDate,
            access.BranchId,
            HttpContext.RequestAborted));
    }

    [HttpGet("staff-salary/{staffId:long}/coefficient")]
    public async Task<IActionResult> StaffSalaryCoefficient(long staffId, [FromQuery] DateTime? asOfDate = null)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.GetStaffSalaryCoefficientAsync(
            staffId,
            asOfDate,
            access.BranchId,
            HttpContext.RequestAborted));
    }

    [HttpPost("staff-salary/{staffId:long}/initial-requests")]
    public async Task<IActionResult> RequestInitialSalary(long staffId, [FromBody] InitialSalaryRequestDto request)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.RequestInitialSalaryAsync(
            staffId,
            new InitialSalaryCommand(request.BaseSalary, request.EffectiveFrom, request.Reason, request.Note),
            access.UserId,
            access.BranchId,
            HttpContext.RequestAborted));
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.GetDashboardAsync(fromDate, toDate, access.BranchId));
    }

    [HttpGet("dashboard-v2")]
    public async Task<IActionResult> DashboardV2(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int? historyLimit = null)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.GetDashboardV2Async(fromDate, toDate, historyLimit, access.BranchId));
    }

    [HttpGet("dashboard-v2/exports")]
    public async Task<IActionResult> ExportDashboardV2Csv(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int? historyLimit = null)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        var file = await _commandFacade.ExportDashboardV2CsvAsync(fromDate, toDate, historyLimit, access.BranchId);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
