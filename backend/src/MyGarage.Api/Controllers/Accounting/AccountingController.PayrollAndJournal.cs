using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Accounting;

namespace MyGarage.Controllers;

public partial class AccountingController : ControllerBase
{
    [HttpPost("journal-entries/{id:long}/postings")]
    public async Task<IActionResult> PostJournal(long id, [FromBody] PostJournalRequest request)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        _ = request;
        return ToActionResult(await _commandFacade.PostJournalAsync(id, access.UserId, access.BranchId));
    }

    [HttpPost("finance-approvals/{id:long}/decisions")]
    public async Task<IActionResult> DecideFinanceApproval(long id, [FromBody] FinanceDecisionRequest request)
    {
        if (!TryGetFinanceApprovalAccess(out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.DecideFinanceApprovalAsync(
            id,
            request.Decision,
            request.RejectReason,
            access.UserId,
            access.BranchId));
    }

    [HttpPost("payroll-runs/{id:long}/approvals")]
    public async Task<IActionResult> ApprovePayroll(long id, [FromBody] PayrollApproveRequest request)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        _ = request;
        return ToActionResult(await _commandFacade.ApprovePayrollAsync(id, access.UserId, access.BranchId, HttpContext.RequestAborted));
    }

    [HttpPost("payroll-runs/{id:long}/payments")]
    public async Task<IActionResult> MarkPayrollPaid(long id, [FromBody] PayrollPaidRequest request)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        _ = request;
        return ToActionResult(await _commandFacade.MarkPayrollPaidAsync(id, access.BranchId));
    }

    [HttpPost("payroll-runs/{id:long}/cancellations")]
    public async Task<IActionResult> CancelPayrollRun(long id)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.CancelPayrollRunAsync(
            id,
            access.UserId,
            access.BranchId,
            HttpContext.RequestAborted));
    }

    [HttpPost("payroll-runs/{id:long}/commission-reconciliations")]
    public async Task<IActionResult> ReconcilePayrollRunCommissions(long id)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.ReconcilePayrollRunCommissionsAsync(
            id,
            access.UserId,
            access.BranchId,
            HttpContext.RequestAborted));
    }

    [HttpPost("payroll-runs/{id:long}/calculations")]
    public async Task<IActionResult> CalculatePayrollRun(long id, [FromBody] PayrollRunCalculateRequest? request)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.CalculatePayrollRunAsync(
            id,
            request,
            access.UserId,
            access.BranchId,
            HttpContext.RequestAborted));
    }

    [HttpPost("payroll-runs/batch-calculations")]
    public async Task<IActionResult> CalculatePayrollForAll([FromBody] PayrollBatchCalculateRequest? request)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.CalculatePayrollForAllAsync(
            request,
            access.UserId,
            access.BranchId,
            HttpContext.RequestAborted));
    }

    [HttpPost("payslips")]
    public async Task<IActionResult> IssuePayslip([FromBody] CreatePayslipRequest? request)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        request ??= new CreatePayslipRequest();
        return ToActionResult(await _commandFacade.IssuePayslipAsync(
            new IssuePayslipCommand(request.PayrollItemId, request.SlipNo, request.IssuedAt, request.IssuedBy, request.FileUrl),
            access.UserId,
            access.BranchId,
            HttpContext.RequestAborted));
    }

    [HttpPost("payroll-periods")]
    public async Task<IActionResult> CreatePayrollPeriod([FromBody] CreatePayrollPeriodRequest request)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.CreatePayrollPeriodAsync(
            new CreatePayrollPeriodCommand(request.PeriodCode, request.StartDate, request.EndDate, request.PayrollDate, request.Status),
            access.BranchId));
    }

    [HttpPost("supplier-payments/{id:long}/voids")]
    public async Task<IActionResult> VoidSupplierPayment(long id, [FromBody] SupplierPaymentVoidRequest request)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        return ToActionResult(await _commandFacade.VoidSupplierPaymentAsync(
            id,
            request.ApprovalRequestId,
            request.Reason,
            access.UserId,
            access.BranchId));
    }
}
