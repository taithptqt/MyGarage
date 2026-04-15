using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Services;

namespace MyGarage.Controllers;

[Route("api/accounting")]
[ApiController]
[Authorize]
public class AccountingController : ControllerBase
{
    private static readonly HashSet<string> RestrictedGenericWriteTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "supplier-invoices",
        "supplier-invoice-details",
        "supplier-payments",
        "supplier-accounting-profiles",
        "accounting-periods",
        "chart-of-accounts",
        "journal-entries",
        "journal-entry-lines",
        "finance-approval-requests",
        "approval-policies",
        "bank-accounts",
        "bank-transactions",
        "bank-reconciliations",
        "staff-contracts",
        "staff-salary-profiles",
        "payroll-periods",
        "payroll-runs",
        "payroll-items",
        "payslips",
        "salary-advances",
        "employee-loans",
        "attendance-adjustments",
        "overtime-entries",
        "pit-profiles",
        "employee-dependents",
        "tax-filings",
        "legal-documents",
        "document-attachments"
    };

    private const string JournalStatusDraft = nameof(JournalEntryStatusCode.DRAFT);
    private const string JournalStatusPosted = nameof(JournalEntryStatusCode.POSTED);
    private const string FinanceApprovalStatusPending = nameof(FinanceApprovalRequestStatusCode.PENDING);
    private const string FinanceApprovalStatusApproved = nameof(FinanceApprovalRequestStatusCode.APPROVED);
    private const string FinanceApprovalStatusRejected = nameof(FinanceApprovalRequestStatusCode.REJECTED);
    private const string FinanceApprovalStatusCanceled = nameof(FinanceApprovalRequestStatusCode.CANCELED);
    private const string PayrollRunStatusDraft = nameof(PayrollRunStatusCode.DRAFT);
    private const string PayrollRunStatusCalculated = nameof(PayrollRunStatusCode.CALCULATED);
    private const string PayrollRunStatusApproved = nameof(PayrollRunStatusCode.APPROVED);
    private const string PayrollRunStatusPaid = nameof(PayrollRunStatusCode.PAID);
    private const string PayrollRunStatusLocked = nameof(PayrollRunStatusCode.LOCKED);
    private const string PayrollItemStatusApproved = nameof(PayrollItemStatusCode.APPROVED);
    private const string PayrollItemStatusPaid = nameof(PayrollItemStatusCode.PAID);
    private const string PayrollPeriodStatusOpen = nameof(PayrollPeriodStatusCode.OPEN);
    private const string PayrollPeriodStatusApproved = nameof(PayrollPeriodStatusCode.APPROVED);
    private const string PayrollPeriodStatusPaid = nameof(PayrollPeriodStatusCode.PAID);
    private const string PayrollPeriodStatusLocked = nameof(PayrollPeriodStatusCode.LOCKED);
    private const string CommissionStatusPending = nameof(CommissionTransactionStatusCode.PENDING);
    private const string CommissionStatusApproved = nameof(CommissionTransactionStatusCode.APPROVED);
    private const string CommissionStatusProcessed = nameof(CommissionTransactionStatusCode.PROCESSED);
    private const string CommissionStatusPaid = nameof(CommissionTransactionStatusCode.PAID);
    private const string CommissionStatusCanceled = nameof(CommissionTransactionStatusCode.CANCELED);
    private const string StaffStatusActive = nameof(StaffStatusCode.ACTIVE);
    private const string StaffStatusOnLeave = nameof(StaffStatusCode.ON_LEAVE);
    private const string StaffSalaryProfileStatusActive = nameof(StaffSalaryProfileStatusCode.ACTIVE);
    private const string StaffContractStatusActive = nameof(StaffContractStatusCode.ACTIVE);
    private const string EmployeeLoanStatusActive = nameof(EmployeeLoanStatusCode.ACTIVE);
    private const string SalaryAdvanceStatusApproved = nameof(SalaryAdvanceStatusCode.APPROVED);
    private const string SalaryAdvanceStatusDeducting = nameof(SalaryAdvanceStatusCode.DEDUCTING);
    private const string AdjustmentApprovedStatus = nameof(AttendanceAdjustmentStatusCode.APPROVED);
    private const string AdjustmentPaidStatus = nameof(OvertimeEntryStatusCode.PAID);
    private const string PaymentCompletedStatus = nameof(PaymentStatusCode.COMPLETED);

    private readonly IAccountingCrudService _crud;
    private readonly IGarageContext _db;
    private readonly IPayrollBatchCalculationService _payrollBatchCalculationService;

    public AccountingController(
        IAccountingCrudService crud,
        IGarageContext db,
        IPayrollBatchCalculationService payrollBatchCalculationService)
    {
        _crud = crud;
        _db = db;
        _payrollBatchCalculationService = payrollBatchCalculationService;
    }

    [HttpGet("table/{tableKey}")]
    public async Task<IActionResult> GetTable(string tableKey, [FromQuery] int limit = 500)
    {
        if (!TryGetTableAccess(tableKey, out var access, out var denied)) return denied;
        try { return Ok(await _crud.GetAllAsync(tableKey, limit, access.BranchId)); }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("table/{tableKey}/{id:long}")]
    public async Task<IActionResult> GetById(string tableKey, long id)
    {
        if (!TryGetTableAccess(tableKey, out var access, out var denied)) return denied;
        try
        {
            var row = await _crud.GetByIdAsync(tableKey, id, access.BranchId);
            return row is null ? NotFound() : Ok(row);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("table/{tableKey}")]
    public async Task<IActionResult> Create(string tableKey, [FromBody] JsonElement payload)
    {
        if (!TryGetTableAccess(tableKey, out var access, out var denied)) return denied;
        if (IsRestrictedGenericWriteTable(tableKey))
        {
            return StatusCode(405, new { message = $"Generic write is disabled for accounting-critical table '{tableKey}'. Use a dedicated workflow endpoint." });
        }
        try
        {
            if (tableKey.Equals("supplier-invoices", StringComparison.OrdinalIgnoreCase))
            {
                var error = await ValidateSupplierInvoice3WayMatchingAsync(payload, access.BranchId);
                if (!string.IsNullOrWhiteSpace(error)) return BadRequest(new { message = error });
            }

            if (tableKey.Equals("supplier-payments", StringComparison.OrdinalIgnoreCase))
            {
                var error = await ValidateSupplierPaymentAsync(payload, access.BranchId);
                if (!string.IsNullOrWhiteSpace(error)) return BadRequest(new { message = error });
            }
            var created = await _crud.InsertAsync(tableKey, payload, access.BranchId);
            return created is null ? BadRequest(new { message = "Failed to create row." }) : Ok(created);
        }
        catch (SqlException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("table/{tableKey}/{id:long}")]
    public async Task<IActionResult> Update(string tableKey, long id, [FromBody] JsonElement payload)
    {
        if (!TryGetTableAccess(tableKey, out var access, out var denied)) return denied;
        if (IsRestrictedGenericWriteTable(tableKey))
        {
            return StatusCode(405, new { message = $"Generic write is disabled for accounting-critical table '{tableKey}'. Use a dedicated workflow endpoint." });
        }
        try
        {
            if (tableKey.Equals("supplier-invoices", StringComparison.OrdinalIgnoreCase))
            {
                var error = await ValidateSupplierInvoice3WayMatchingAsync(payload, access.BranchId, id);
                if (!string.IsNullOrWhiteSpace(error)) return BadRequest(new { message = error });
            }

            if (tableKey.Equals("supplier-payments", StringComparison.OrdinalIgnoreCase))
            {
                var error = await ValidateSupplierPaymentAsync(payload, access.BranchId, id);
                if (!string.IsNullOrWhiteSpace(error)) return BadRequest(new { message = error });
            }
            var updated = await _crud.UpdateAsync(tableKey, id, payload, access.BranchId);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (SqlException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("table/{tableKey}/{id:long}")]
    public async Task<IActionResult> Delete(string tableKey, long id)
    {
        if (!TryGetTableAccess(tableKey, out var access, out var denied)) return denied;
        if (IsRestrictedGenericWriteTable(tableKey))
        {
            return StatusCode(405, new { message = $"Generic write is disabled for accounting-critical table '{tableKey}'. Use a dedicated workflow endpoint." });
        }
        try { return await _crud.DeleteAsync(tableKey, id, access.BranchId) ? NoContent() : NotFound(); }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("supplier-invoices")]
    public async Task<IActionResult> CreateSupplierInvoice([FromBody] JsonElement payload)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;

        var error = await ValidateSupplierInvoice3WayMatchingAsync(payload, access.BranchId);
        if (!string.IsNullOrWhiteSpace(error))
        {
            return BadRequest(new { message = error });
        }

        var created = await _crud.InsertAsync("supplier-invoices", payload, access.BranchId);
        return created is null
            ? BadRequest(new { message = "Failed to create supplier invoice." })
            : Ok(created);
    }

    [HttpPut("supplier-invoices/{id:long}")]
    public async Task<IActionResult> UpdateSupplierInvoice(long id, [FromBody] JsonElement payload)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;

        var error = await ValidateSupplierInvoice3WayMatchingAsync(payload, access.BranchId, id);
        if (!string.IsNullOrWhiteSpace(error))
        {
            return BadRequest(new { message = error });
        }

        var updated = await _crud.UpdateAsync("supplier-invoices", id, payload, access.BranchId);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("supplier-invoice-details")]
    public async Task<IActionResult> CreateSupplierInvoiceDetail([FromBody] JsonElement payload)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;

        var error = await ValidateSupplierInvoiceDetail3WayMatchingAsync(payload, access.BranchId);
        if (!string.IsNullOrWhiteSpace(error))
        {
            return BadRequest(new { message = error });
        }

        var created = await _crud.InsertAsync("supplier-invoice-details", payload, access.BranchId);
        return created is null
            ? BadRequest(new { message = "Failed to create supplier invoice detail." })
            : Ok(created);
    }

    [HttpPut("supplier-invoice-details/{id:long}")]
    public async Task<IActionResult> UpdateSupplierInvoiceDetail(long id, [FromBody] JsonElement payload)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;

        var error = await ValidateSupplierInvoiceDetail3WayMatchingAsync(payload, access.BranchId, id);
        if (!string.IsNullOrWhiteSpace(error))
        {
            return BadRequest(new { message = error });
        }

        var updated = await _crud.UpdateAsync("supplier-invoice-details", id, payload, access.BranchId);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("journal-entries/{id:long}/post")]
    public async Task<IActionResult> PostJournal(long id, [FromBody] PostJournalRequest request)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        var row = await _crud.GetByIdAsync("journal-entries", id, access.BranchId);
        if (row is null) return NotFound();
        if (!AsString(row, "status").Equals(JournalStatusDraft, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only DRAFT journal can be posted." });

        var sums = (await _crud.QueryAsync(
            "SELECT ISNULL(SUM(debit_amount),0) AS total_debit, ISNULL(SUM(credit_amount),0) AS total_credit, COUNT(*) AS line_count FROM dbo.journal_entry_lines WHERE journal_entry_id=@id;",
            [new SqlParameter("@id", id)])).FirstOrDefault();
        if ((AsLong(sums, "lineCount") ?? 0) <= 0) return BadRequest(new { message = "Journal has no lines." });
        if (Math.Abs(AsDecimal(sums, "totalDebit") - AsDecimal(sums, "totalCredit")) > 0.005m)
            return BadRequest(new { message = "Posted journal entry must be balanced." });

        await _crud.ExecuteAsync(
            "UPDATE dbo.journal_entries SET status=@status, posted_by=@u, posted_at=SYSDATETIME(), updated_at=SYSDATETIME() WHERE id=@id;",
            [
                new SqlParameter("@status", JournalStatusPosted),
                new SqlParameter("@u", access.UserId),
                new SqlParameter("@id", id)
            ]);
        return Ok(await _crud.GetByIdAsync("journal-entries", id, access.BranchId));
    }

    [HttpPost("finance-approvals/{id:long}/decision")]
    public async Task<IActionResult> DecideFinanceApproval(long id, [FromBody] FinanceDecisionRequest request)
    {
        if (!TryGetFinanceApprovalAccess(out var access, out var denied)) return denied;
        var decision = (request.Decision ?? "").Trim().ToUpperInvariant();
        if (decision is not (FinanceApprovalStatusApproved or FinanceApprovalStatusRejected or FinanceApprovalStatusCanceled))
            return BadRequest(new { message = "decision must be APPROVED/REJECTED/CANCELED." });

        var row = await _crud.GetByIdAsync("finance-approval-requests", id, access.BranchId);
        if (row is null) return NotFound();
        if (!AsString(row, "status").Equals(FinanceApprovalStatusPending, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only PENDING request can be decided." });

        await _crud.ExecuteAsync(
            "UPDATE dbo.finance_approval_requests SET status=@s, approver_id=@a, reject_reason=@r, updated_at=SYSDATETIME() WHERE id=@id;",
            [
                new SqlParameter("@s", decision),
                new SqlParameter("@a", access.UserId),
                new SqlParameter("@r", (object?)request.RejectReason ?? DBNull.Value),
                new SqlParameter("@id", id)
            ]);
        return Ok(await _crud.GetByIdAsync("finance-approval-requests", id, access.BranchId));
    }

    [HttpPost("payroll-runs/{id:long}/approve")]
    public async Task<IActionResult> ApprovePayroll(long id, [FromBody] PayrollApproveRequest request)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        var row = await _crud.GetByIdAsync("payroll-runs", id, access.BranchId);
        if (row is null) return NotFound();
        var payrollPeriodId = AsLong(row, "payrollPeriodId");
        var runStatus = AsString(row, "status").Trim().ToUpperInvariant();
        if (runStatus is not PayrollRunStatusDraft and not PayrollRunStatusCalculated)
            return BadRequest(new { message = "Only DRAFT/CALCULATED payroll run can be approved." });

        var lineCount = await _crud.ScalarAsync<int?>(
            "SELECT COUNT(*) FROM dbo.payroll_items WHERE payroll_run_id=@id;",
            [new SqlParameter("@id", id)]);
        if (!lineCount.HasValue || lineCount.Value <= 0)
            return BadRequest(new { message = "Cannot approve payroll run without payroll items." });

        var reconciliation = await BuildPayrollRunCommissionReconciliationAsync(id, access.BranchId, HttpContext.RequestAborted);
        if (!reconciliation.Summary.IsFullyMatched)
        {
            return BadRequest(new
            {
                message = "Cannot approve payroll run when commission reconciliation is not matched.",
                commissionGap = reconciliation.Summary.TotalGap
            });
        }

        await _crud.ExecuteAsync(
            "UPDATE dbo.payroll_runs SET status=@status, approved_by=@u, approved_at=SYSDATETIME(), updated_at=SYSDATETIME() WHERE id=@id;",
            [
                new SqlParameter("@status", PayrollRunStatusApproved),
                new SqlParameter("@u", access.UserId),
                new SqlParameter("@id", id)
            ]);
        await _crud.ExecuteAsync(
            $"""
            UPDATE dbo.payroll_items
            SET status=@itemStatusApproved
            WHERE payroll_run_id=@id
              AND UPPER(ISNULL(status, @runStatusDraft)) IN (@runStatusDraft, @runStatusCalculated);
            """,
            [
                new SqlParameter("@id", id),
                new SqlParameter("@itemStatusApproved", PayrollItemStatusApproved),
                new SqlParameter("@runStatusDraft", PayrollRunStatusDraft),
                new SqlParameter("@runStatusCalculated", PayrollRunStatusCalculated)
            ]);
        if (payrollPeriodId.HasValue && payrollPeriodId.Value > 0)
            await SyncPayrollPeriodStatusAsync(payrollPeriodId.Value);
        return Ok(await _crud.GetByIdAsync("payroll-runs", id, access.BranchId));
    }

    [HttpPost("payroll-runs/{id:long}/mark-paid")]
    public async Task<IActionResult> MarkPayrollPaid(long id, [FromBody] PayrollPaidRequest request)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        var row = await _crud.GetByIdAsync("payroll-runs", id, access.BranchId);
        if (row is null) return NotFound();
        var payrollPeriodId = AsLong(row, "payrollPeriodId");
        if (!AsString(row, "status").Equals(PayrollRunStatusApproved, StringComparison.OrdinalIgnoreCase) || !AsLong(row, "approvedBy").HasValue)
            return BadRequest(new { message = "Run must be APPROVED and approvedBy set before PAID." });

        await _crud.ExecuteAsync(
            "UPDATE dbo.payroll_runs SET status=@status, paid_at=SYSDATETIME(), updated_at=SYSDATETIME() WHERE id=@id;",
            [
                new SqlParameter("@id", id),
                new SqlParameter("@status", PayrollRunStatusPaid)
            ]);
        await _crud.ExecuteAsync(
            $"""
            UPDATE dbo.payroll_items
            SET status=@itemStatusPaid
            WHERE payroll_run_id=@id;
            """,
            [
                new SqlParameter("@id", id),
                new SqlParameter("@itemStatusPaid", PayrollItemStatusPaid)
            ]);
        if (payrollPeriodId.HasValue && payrollPeriodId.Value > 0)
            await SyncPayrollPeriodStatusAsync(payrollPeriodId.Value);
        return Ok(await _crud.GetByIdAsync("payroll-runs", id, access.BranchId));
    }

    [HttpPost("payroll-runs/{id:long}/reconcile-commissions")]
    public async Task<IActionResult> ReconcilePayrollRunCommissions(long id)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;

        var run = await _db.PayrollRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, HttpContext.RequestAborted);
        if (run is null) return NotFound(new { message = $"Payroll run #{id} not found." });
        if (access.BranchId.HasValue && run.BranchId.HasValue && run.BranchId.Value != access.BranchId.Value)
            return NotFound(new { message = $"Payroll run #{id} not found." });

        var runStatus = (run.Status ?? string.Empty).Trim().ToUpperInvariant();
        if (runStatus is not PayrollRunStatusDraft and not PayrollRunStatusCalculated)
        {
            return BadRequest(new
            {
                message = "Only DRAFT/CALCULATED payroll run can be reconciled.",
                currentStatus = run.Status
            });
        }

        var before = await BuildPayrollRunCommissionReconciliationAsync(id, access.BranchId, HttpContext.RequestAborted);

        PayrollBatchCalculationResult calculateResult;
        try
        {
            calculateResult = await _payrollBatchCalculationService.CalculatePayrollForAllAsync(
                new PayrollBatchCalculationCommand
                {
                    UserId = access.UserId,
                    BranchId = access.BranchId,
                    PayrollRunId = id,
                    OverwriteExisting = true,
                    IncludeInactiveStaff = false,
                    IssuePayslips = false
                },
                HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var after = await BuildPayrollRunCommissionReconciliationAsync(id, access.BranchId, HttpContext.RequestAborted);

        return Ok(new
        {
            message = after.Summary.IsFullyMatched
                ? "Commission reconciliation completed and matched."
                : "Commission reconciliation completed but still has gap.",
            payrollRunId = id,
            runNo = run.RunNo,
            beforeSummary = before.Summary,
            afterSummary = after.Summary,
            matched = after.Summary.IsFullyMatched,
            calculateResult = new
            {
                calculateResult.ProcessedStaffCount,
                calculateResult.CalculatedStaffCount,
                calculateResult.Inserted,
                calculateResult.Updated,
                calculateResult.SkippedNoBaseSalary,
                calculateResult.SkippedExisting,
                calculateResult.CommissionTotal,
                calculateResult.ProcessedCommissionTransactions
            },
            reconciliation = after
        });
    }

    [HttpPost("payroll-runs/{id:long}/calculate")]
    public async Task<IActionResult> CalculatePayrollRun(long id, [FromBody] PayrollRunCalculateRequest? request)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        request ??= new PayrollRunCalculateRequest();

        try
        {
            var result = await _payrollBatchCalculationService.CalculatePayrollForAllAsync(
                new PayrollBatchCalculationCommand
                {
                    UserId = access.UserId,
                    BranchId = access.BranchId,
                    PayrollRunId = id,
                    OverwriteExisting = request.OverwriteExisting,
                    IncludeInactiveStaff = request.IncludeInactiveStaff,
                    IssuePayslips = request.IssuePayslips
                },
                HttpContext.RequestAborted);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("payroll-runs/calculate-all")]
    public async Task<IActionResult> CalculatePayrollForAll([FromBody] PayrollBatchCalculateRequest? request)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        request ??= new PayrollBatchCalculateRequest();
        try
        {
            var result = await _payrollBatchCalculationService.CalculatePayrollForAllAsync(
                new PayrollBatchCalculationCommand
                {
                    UserId = access.UserId,
                    BranchId = access.BranchId,
                    PayrollPeriodId = request.PayrollPeriodId,
                    PayrollRunId = request.PayrollRunId,
                    OverwriteExisting = request.OverwriteExisting,
                    IncludeInactiveStaff = request.IncludeInactiveStaff,
                    IssuePayslips = request.IssuePayslips
                },
                HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("payslips")]
    public async Task<IActionResult> IssuePayslip([FromBody] CreatePayslipRequest? request)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        request ??= new CreatePayslipRequest();

        if (request.PayrollItemId <= 0)
            return BadRequest(new { message = "payrollItemId must be a positive number." });

        var payrollItem = await _db.PayrollItems
            .AsNoTracking()
            .Include(x => x.PayrollRun)
            .FirstOrDefaultAsync(x => x.Id == request.PayrollItemId);
        if (payrollItem is null)
            return NotFound(new { message = $"Payroll item #{request.PayrollItemId} not found." });

        if (access.BranchId.HasValue && payrollItem.PayrollRun.BranchId != access.BranchId.Value)
            return NotFound(new { message = $"Payroll item #{request.PayrollItemId} not found." });

        var runStatus = (payrollItem.PayrollRun.Status ?? string.Empty).Trim().ToUpperInvariant();
        if (runStatus is not PayrollRunStatusApproved and not PayrollRunStatusPaid)
            return BadRequest(new { message = "Payslip can only be issued when payroll run is APPROVED or PAID." });

        var existingPayslip = await _db.Payslips
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PayrollItemId == request.PayrollItemId);
        if (existingPayslip is not null)
        {
            var existingRow = await _crud.GetByIdAsync("payslips", existingPayslip.Id, access.BranchId);
            return existingRow is null ? Ok(existingPayslip) : Ok(existingRow);
        }

        var issuedAt = DateTime.UtcNow;
        var issuedAtText = (request.IssuedAt ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(issuedAtText))
        {
            if (!DateTime.TryParse(issuedAtText, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out issuedAt))
                return BadRequest(new { message = "issuedAt is invalid." });
        }

        var slipNo = (request.SlipNo ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(slipNo))
            slipNo = $"PS-{payrollItem.PayrollRun.RunNo}-{request.PayrollItemId}-{issuedAt:yyMMddHHmm}";
        if (slipNo.Length > 40)
            return BadRequest(new { message = "slipNo must be <= 40 characters." });

        var fileUrl = (request.FileUrl ?? string.Empty).Trim();

        var payload = JsonSerializer.SerializeToElement(new
        {
            payrollItemId = request.PayrollItemId,
            slipNo,
            issuedAt,
            issuedBy = access.UserId,
            fileUrl
        });

        try
        {
            var created = await _crud.InsertAsync("payslips", payload, access.BranchId);
            return created is null
                ? BadRequest(new { message = "Failed to issue payslip." })
                : Ok(created);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            var duplicated = await _db.Payslips
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.PayrollItemId == request.PayrollItemId);
            if (duplicated is not null)
            {
                var duplicatedRow = await _crud.GetByIdAsync("payslips", duplicated.Id, access.BranchId);
                return duplicatedRow is null ? Ok(duplicated) : Ok(duplicatedRow);
            }

            return Conflict(new { message = "Payslip already exists for this payroll item." });
        }
        catch (SqlException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("payroll-periods")]
    public async Task<IActionResult> CreatePayrollPeriod([FromBody] CreatePayrollPeriodRequest request)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;

        var periodCode = (request.PeriodCode ?? "").Trim();
        if (string.IsNullOrWhiteSpace(periodCode))
            return BadRequest(new { message = "periodCode is required." });
        if (periodCode.Length > 20)
            return BadRequest(new { message = "periodCode must be <= 20 characters." });

        if (!DateOnly.TryParse(request.StartDate, out var startDate)
            || !DateOnly.TryParse(request.EndDate, out var endDate)
            || !DateOnly.TryParse(request.PayrollDate, out var payrollDate))
            return BadRequest(new { message = "startDate/endDate/payrollDate must be valid dates (yyyy-MM-dd)." });

        if (startDate > endDate)
            return BadRequest(new { message = "startDate must be <= endDate." });
        if (payrollDate < endDate)
            return BadRequest(new { message = "payrollDate must be >= endDate." });

        try
        {
            var duplicated = await _crud.ScalarAsync<int?>(
                "SELECT COUNT(*) FROM dbo.payroll_periods WHERE UPPER(period_code)=UPPER(@periodCode);",
                [new SqlParameter("@periodCode", periodCode)]);
            if (duplicated.GetValueOrDefault() > 0)
                return Conflict(new { message = $"Payroll period code '{periodCode}' already exists." });

            var payload = JsonSerializer.SerializeToElement(new
            {
                periodCode,
                startDate = startDate.ToDateTime(TimeOnly.MinValue),
                endDate = endDate.ToDateTime(TimeOnly.MinValue),
                payrollDate = payrollDate.ToDateTime(TimeOnly.MinValue),
                status = PayrollPeriodStatusOpen
            });

            var created = await _crud.InsertAsync("payroll-periods", payload, access.BranchId);
            return created is null
                ? BadRequest(new { message = "Failed to create payroll period." })
                : Ok(created);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return Conflict(new { message = $"Payroll period code '{periodCode}' already exists." });
        }
        catch (SqlException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("supplier-payments/{id:long}/void")]
    public async Task<IActionResult> VoidSupplierPayment(long id, [FromBody] SupplierPaymentVoidRequest request)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        var payment = await _crud.GetByIdAsync("supplier-payments", id, access.BranchId);
        if (payment is null) return NotFound();

        if (!_crud.TryGetConfig("finance-approval-requests", out var approvalCfg) || !await _crud.TableExistsAsync(approvalCfg))
            return BadRequest(new { message = "finance_approval_requests table is required for supplier payment void flow." });

        var approved = await _crud.ScalarAsync<int?>(
            "SELECT TOP 1 1 FROM dbo.finance_approval_requests WHERE status=@statusApproved AND request_type='SUPPLIER_PAYMENT_VOID' AND (supplier_payment_id=@pid OR id=@aid);",
            [
                new SqlParameter("@statusApproved", FinanceApprovalStatusApproved),
                new SqlParameter("@pid", id),
                new SqlParameter("@aid", request.ApprovalRequestId.HasValue ? request.ApprovalRequestId.Value : DBNull.Value)
            ]);
        if (!approved.HasValue) return BadRequest(new { message = "Void requires approved SUPPLIER_PAYMENT_VOID finance approval." });

        await _crud.DeleteAsync("supplier-payments", id, access.BranchId);
        return Ok(new { message = "Supplier payment voided.", id, approvedBy = access.UserId, request.Reason });
    }

    [HttpGet("reports/trial-balance")]
    public async Task<IActionResult> TrialBalance()
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        if (await _crud.ViewExistsAsync("dbo.vw_gl_trial_balance"))
            return Ok(await _crud.QueryAsync(
                """
                IF COL_LENGTH('dbo.vw_gl_trial_balance', 'branch_id') IS NOT NULL
                    SELECT * FROM dbo.vw_gl_trial_balance WHERE branch_id=@branchId ORDER BY account_code;
                ELSE
                    SELECT * FROM dbo.vw_gl_trial_balance ORDER BY account_code;
                """,
                [new SqlParameter("@branchId", (object?)access.BranchId ?? DBNull.Value)]));
        if (!_crud.TryGetConfig("journal-entry-lines", out var lineCfg) || !await _crud.TableExistsAsync(lineCfg))
            return NotFound(new { message = "journal_entry_lines table/view unavailable." });
        return Ok(await _crud.QueryAsync(
            $"""
            IF COL_LENGTH('dbo.journal_entry_lines', 'branch_id') IS NOT NULL
                SELECT
                    coa.account_code,
                    coa.account_name,
                    SUM(jel.debit_amount) AS total_debit,
                    SUM(jel.credit_amount) AS total_credit,
                    SUM(jel.debit_amount - jel.credit_amount) AS balance
                FROM dbo.journal_entry_lines jel
                INNER JOIN dbo.chart_of_accounts coa ON coa.id = jel.account_id
                INNER JOIN dbo.journal_entries je ON je.id = jel.journal_entry_id
                WHERE je.status=@statusPosted AND (@branchId IS NULL OR jel.branch_id=@branchId)
                GROUP BY coa.account_code, coa.account_name
                ORDER BY coa.account_code;
            ELSE IF COL_LENGTH('dbo.journal_entry_lines', 'BranchId') IS NOT NULL
                SELECT
                    coa.account_code,
                    coa.account_name,
                    SUM(jel.debit_amount) AS total_debit,
                    SUM(jel.credit_amount) AS total_credit,
                    SUM(jel.debit_amount - jel.credit_amount) AS balance
                FROM dbo.journal_entry_lines jel
                INNER JOIN dbo.chart_of_accounts coa ON coa.id = jel.account_id
                INNER JOIN dbo.journal_entries je ON je.id = jel.journal_entry_id
                WHERE je.status=@statusPosted AND (@branchId IS NULL OR jel.BranchId=@branchId)
                GROUP BY coa.account_code, coa.account_name
                ORDER BY coa.account_code;
            ELSE
                SELECT
                    coa.account_code,
                    coa.account_name,
                    SUM(jel.debit_amount) AS total_debit,
                    SUM(jel.credit_amount) AS total_credit,
                    SUM(jel.debit_amount - jel.credit_amount) AS balance
                FROM dbo.journal_entry_lines jel
                INNER JOIN dbo.chart_of_accounts coa ON coa.id = jel.account_id
                INNER JOIN dbo.journal_entries je ON je.id = jel.journal_entry_id
                WHERE je.status=@statusPosted
                GROUP BY coa.account_code, coa.account_name
                ORDER BY coa.account_code;
            """,
            [
                new SqlParameter("@branchId", (object?)access.BranchId ?? DBNull.Value),
                new SqlParameter("@statusPosted", JournalStatusPosted)
            ]));
    }

    [HttpGet("reports/supplier-payable-summary")]
    public async Task<IActionResult> SupplierPayableSummary()
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        if (await _crud.ViewExistsAsync("dbo.vw_supplier_payable_summary"))
            return Ok(await _crud.QueryAsync(
                """
                IF COL_LENGTH('dbo.vw_supplier_payable_summary', 'branch_id') IS NOT NULL
                    SELECT * FROM dbo.vw_supplier_payable_summary WHERE branch_id=@branchId ORDER BY due_date ASC, id DESC;
                ELSE
                    SELECT * FROM dbo.vw_supplier_payable_summary ORDER BY due_date ASC, id DESC;
                """,
                [new SqlParameter("@branchId", (object?)access.BranchId ?? DBNull.Value)]));
        if (!_crud.TryGetConfig("accounts-payable", out var apCfg) || !await _crud.TableExistsAsync(apCfg))
            return NotFound(new { message = "accounts_payable table/view unavailable." });
        return Ok(await _crud.QueryAsync(
            "SELECT * FROM dbo.accounts_payable WHERE (@branchId IS NULL OR branch_id=@branchId) ORDER BY due_date ASC, id DESC;",
            [new SqlParameter("@branchId", (object?)access.BranchId ?? DBNull.Value)]));
    }

    [HttpGet("reports/payroll-run-summary")]
    public async Task<IActionResult> PayrollRunSummary()
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        if (await _crud.ViewExistsAsync("dbo.vw_payroll_run_summary"))
            return Ok(await _crud.QueryAsync(
                """
                IF COL_LENGTH('dbo.vw_payroll_run_summary', 'branch_id') IS NOT NULL
                    SELECT * FROM dbo.vw_payroll_run_summary WHERE branch_id=@branchId ORDER BY payroll_run_id DESC;
                ELSE
                    SELECT * FROM dbo.vw_payroll_run_summary ORDER BY payroll_run_id DESC;
                """,
                [new SqlParameter("@branchId", (object?)access.BranchId ?? DBNull.Value)]));
        if (!_crud.TryGetConfig("payroll-runs", out var runCfg) || !await _crud.TableExistsAsync(runCfg))
            return NotFound(new { message = "payroll_runs table/view unavailable." });
        return Ok(await _crud.QueryAsync(
            "SELECT * FROM dbo.payroll_runs WHERE (@branchId IS NULL OR branch_id=@branchId) ORDER BY id DESC;",
            [new SqlParameter("@branchId", (object?)access.BranchId ?? DBNull.Value)]));
    }

    [HttpGet("payroll-runs/{id:long}/commission-reconciliation")]
    public async Task<IActionResult> PayrollRunCommissionReconciliation(long id)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        try
        {
            var reconciliation = await BuildPayrollRunCommissionReconciliationAsync(id, access.BranchId, HttpContext.RequestAborted);
            return Ok(reconciliation);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    private async Task<PayrollRunCommissionReconciliationResponse> BuildPayrollRunCommissionReconciliationAsync(
        long payrollRunId,
        long? accessBranchId,
        CancellationToken cancellationToken)
    {
        var run = await _db.PayrollRuns
            .AsNoTracking()
            .Include(x => x.PayrollPeriod)
            .FirstOrDefaultAsync(x => x.Id == payrollRunId, cancellationToken);
        if (run is null) throw new KeyNotFoundException($"Payroll run #{payrollRunId} not found.");
        if (accessBranchId.HasValue && run.BranchId.HasValue && run.BranchId.Value != accessBranchId.Value)
            throw new KeyNotFoundException($"Payroll run #{payrollRunId} not found.");

        var periodStart = run.PayrollPeriod.StartDate.ToDateTime(TimeOnly.MinValue);
        var periodEnd = run.PayrollPeriod.EndDate;
        var periodEndExclusive = periodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var payrollItems = await _db.PayrollItems
            .AsNoTracking()
            .Where(x => x.PayrollRunId == payrollRunId)
            .Select(x => new
            {
                x.StaffId,
                x.CommissionAmount
            })
            .ToListAsync(cancellationToken);
        var staffIds = payrollItems.Select(x => x.StaffId).Distinct().ToHashSet();

        var transactions = await _db.CommissionTransactions
            .AsNoTracking()
            .Where(x => x.CommissionAmount > 0m)
            .Where(x => !staffIds.Any() || staffIds.Contains(x.StaffId))
            .ToListAsync(cancellationToken);

        var details = new List<PayrollCommissionReconciliationDetail>();
        foreach (var byStaff in payrollItems.GroupBy(x => x.StaffId))
        {
            var staffId = byStaff.Key;
            var payrollCommissionAmount = Math.Round(byStaff.Sum(x => Math.Max(0m, x.CommissionAmount)), 0, MidpointRounding.AwayFromZero);

            var staffTransactions = transactions
                .Where(x => x.StaffId == staffId)
                .Where(x =>
                {
                    var effectiveAt = x.ApprovedAt ?? x.CreatedAt ?? x.UpdatedAt;
                    return effectiveAt.HasValue && effectiveAt.Value >= periodStart && effectiveAt.Value < periodEndExclusive;
                })
                .ToList();

            decimal processedAmount = 0m;
            decimal pendingAmount = 0m;
            decimal paidAmount = 0m;
            decimal canceledAmount = 0m;
            var processedCount = 0;
            var pendingCount = 0;
            var paidCount = 0;
            var canceledCount = 0;

            foreach (var tx in staffTransactions)
            {
                var amount = Math.Max(0m, tx.CommissionAmount);
                var status = (tx.Status ?? string.Empty).Trim().ToUpperInvariant();
                switch (status)
                {
                    case CommissionStatusProcessed:
                        processedAmount += amount;
                        processedCount++;
                        break;
                    case CommissionStatusPending:
                    case CommissionStatusApproved:
                        pendingAmount += amount;
                        pendingCount++;
                        break;
                    case CommissionStatusPaid:
                        paidAmount += amount;
                        paidCount++;
                        break;
                    case CommissionStatusCanceled:
                        canceledAmount += amount;
                        canceledCount++;
                        break;
                }
            }

            var gapAmount = Math.Round(payrollCommissionAmount - Math.Round(processedAmount, 0, MidpointRounding.AwayFromZero), 0, MidpointRounding.AwayFromZero);
            details.Add(new PayrollCommissionReconciliationDetail
            {
                StaffId = staffId,
                PayrollCommissionAmount = payrollCommissionAmount,
                ProcessedAmount = Math.Round(processedAmount, 0, MidpointRounding.AwayFromZero),
                PendingAmount = Math.Round(pendingAmount, 0, MidpointRounding.AwayFromZero),
                PaidAmount = Math.Round(paidAmount, 0, MidpointRounding.AwayFromZero),
                CanceledAmount = Math.Round(canceledAmount, 0, MidpointRounding.AwayFromZero),
                ProcessedCount = processedCount,
                PendingCount = pendingCount,
                PaidCount = paidCount,
                CanceledCount = canceledCount,
                GapAmount = gapAmount,
                IsMatched = gapAmount == 0m
            });
        }

        var totalPayrollCommission = details.Sum(x => x.PayrollCommissionAmount);
        var totalProcessed = details.Sum(x => x.ProcessedAmount);
        var totalPending = details.Sum(x => x.PendingAmount);
        var totalPaid = details.Sum(x => x.PaidAmount);
        var totalCanceled = details.Sum(x => x.CanceledAmount);
        var totalGap = details.Sum(x => x.GapAmount);

        return new PayrollRunCommissionReconciliationResponse
        {
            PayrollRun = new PayrollRunCommissionReconciliationRunInfo
            {
                Id = run.Id,
                RunNo = run.RunNo,
                Status = run.Status,
                BranchId = run.BranchId,
                PayrollPeriodId = run.PayrollPeriodId,
                PeriodCode = run.PayrollPeriod.PeriodCode,
                PeriodStartDate = run.PayrollPeriod.StartDate.ToString("yyyy-MM-dd"),
                PeriodEndDate = run.PayrollPeriod.EndDate.ToString("yyyy-MM-dd")
            },
            Summary = new PayrollRunCommissionReconciliationSummary
            {
                StaffCount = details.Count,
                TotalPayrollCommission = totalPayrollCommission,
                TotalProcessed = totalProcessed,
                TotalPending = totalPending,
                TotalPaid = totalPaid,
                TotalCanceled = totalCanceled,
                TotalGap = totalGap,
                IsFullyMatched = totalGap == 0m
            },
            Details = details
        };
    }

    [HttpGet("staff-salary/{staffId:long}/base")]
    public async Task<IActionResult> StaffBaseSalary(long staffId, [FromQuery] DateTime? asOfDate = null)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;

        var staff = await _db.Staffs
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == staffId && s.IsDeleted != true);
        if (staff is null) return NotFound(new { message = $"Staff #{staffId} not found." });
        if (access.BranchId.HasValue && staff.BranchId != access.BranchId.Value) return NotFound();

        var asOf = DateOnly.FromDateTime((asOfDate ?? VietnamTime.Now).Date);
        var baseSalary = await ResolveStaffBaseSalaryAsync(staff.Id, access.BranchId, asOf);

        return Ok(new
        {
            staffId = staff.Id,
            staffName = staff.FullName,
            branchId = staff.BranchId,
            asOfDate = asOf.ToString("yyyy-MM-dd"),
            baseSalary
        });
    }

    [HttpGet("staff-salary/{staffId:long}/coefficient")]
    public async Task<IActionResult> StaffSalaryCoefficient(long staffId, [FromQuery] DateTime? asOfDate = null)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;

        var staff = await _db.Staffs
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == staffId && s.IsDeleted != true);
        if (staff is null) return NotFound(new { message = $"Staff #{staffId} not found." });
        if (access.BranchId.HasValue && staff.BranchId != access.BranchId.Value) return NotFound();
        var hireDate = await ResolveStaffHireDateAsync(staff.Id, staff.HireDate);
        var asOf = DateOnly.FromDateTime((asOfDate ?? VietnamTime.Now).Date);
        decimal coefficient = 1.0000m;
        int yearsOfService = 0;

        if (hireDate.HasValue && asOf >= hireDate.Value)
        {
            yearsOfService = CalculateCompletedYears(hireDate.Value, asOf);
            coefficient = await ResolveStaffSalaryCoefficientAsync(staff.Id, hireDate.Value, asOf);
        }

        var baseSalary = await ResolveStaffBaseSalaryAsync(staff.Id, access.BranchId, asOf);
        var hasBaseSalary = baseSalary.HasValue && baseSalary.Value > 0m;
        decimal calculatedSalary = hasBaseSalary
            ? Math.Round(baseSalary!.Value * coefficient, 0, MidpointRounding.AwayFromZero)
            : 0m;

        return Ok(new
        {
            staffId = staff.Id,
            staffName = staff.FullName,
            branchId = staff.BranchId,
            hireDate = hireDate?.ToString("yyyy-MM-dd") ?? "",
            asOfDate = asOf.ToString("yyyy-MM-dd"),
            yearsOfService,
            baseSalary = hasBaseSalary ? baseSalary!.Value : 0m,
            hasBaseSalary,
            coefficient,
            calculatedSalary,
            policyReference = "Nearest active threshold in SalaryCoefficients where YearsOfService <= completed years; default 1.0 when no threshold is configured."
        });
    }

    [HttpPost("staff-salary/{staffId:long}/request-initial")]
    public async Task<IActionResult> RequestInitialSalary(long staffId, [FromBody] InitialSalaryRequestDto request)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;

        var requestedBaseSalary = request.BaseSalary.GetValueOrDefault();
        if (requestedBaseSalary <= 0m)
        {
            return BadRequest(new { message = "baseSalary must be greater than 0." });
        }

        if (!_crud.TryGetConfig("finance-approval-requests", out var approvalCfg) || !await _crud.TableExistsAsync(approvalCfg))
        {
            return BadRequest(new { message = "finance_approval_requests table is required for salary approval flow." });
        }

        var staff = await _db.Staffs
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == staffId && s.IsDeleted != true);
        if (staff is null) return NotFound(new { message = $"Staff #{staffId} not found." });
        if (access.BranchId.HasValue && staff.BranchId != access.BranchId.Value) return NotFound();

        var effectiveFromDate = DateOnly.FromDateTime((request.EffectiveFrom ?? VietnamTime.Now).Date);
        var existingBaseSalary = await ResolveStaffBaseSalaryAsync(staff.Id, access.BranchId, effectiveFromDate);
        if (existingBaseSalary.HasValue && existingBaseSalary.Value > 0m)
        {
            return BadRequest(new { message = "Staff already has an active base salary profile for the selected effective date." });
        }

        var pendingRequest = (await _crud.QueryAsync(
            $"""
            SELECT TOP 1 id
            FROM dbo.finance_approval_requests
            WHERE request_type='PAYROLL_ADJUSTMENT'
              AND JSON_VALUE(request_payload, '$.requestSubtype') = 'STAFF_INITIAL_SALARY'
              AND status=@statusPending
              AND TRY_CONVERT(bigint, JSON_VALUE(request_payload, '$.staffId')) = @staffId
            ORDER BY id DESC;
            """,
            [
                new SqlParameter("@staffId", staff.Id),
                new SqlParameter("@statusPending", FinanceApprovalStatusPending)
            ])).FirstOrDefault();
        if (pendingRequest is not null)
        {
            return Conflict(new { message = "A pending initial salary request for this staff already exists." });
        }

        var payloadObject = new
        {
            requestType = "PAYROLL_ADJUSTMENT",
            requestSubtype = "STAFF_INITIAL_SALARY",
            staffId = staff.Id,
            staffName = staff.FullName,
            branchId = staff.BranchId,
            baseSalary = requestedBaseSalary,
            effectiveFrom = effectiveFromDate.ToString("yyyy-MM-dd"),
            note = request.Note?.Trim() ?? "",
            requestedBy = access.UserId,
            requestedAt = VietnamTime.Now.ToString("O")
        };
        var payloadJson = JsonSerializer.Serialize(payloadObject);
        var reason = string.IsNullOrWhiteSpace(request.Reason)
            ? $"Yêu cầu tạo lương khởi điểm cho nhân sự {staff.FullName} (#{staff.Id})."
            : request.Reason.Trim();

        var createdId = await _crud.ScalarAsync<long?>(
            """
            DECLARE @inserted TABLE (id bigint);
            INSERT INTO dbo.finance_approval_requests
            (
                request_type,
                requested_by,
                approver_id,
                status,
                risk_level,
                amount,
                reason,
                reject_reason,
                request_payload,
                created_at,
                updated_at
            )
            OUTPUT INSERTED.id INTO @inserted(id)
            VALUES
            (
                'PAYROLL_ADJUSTMENT',
                @requestedBy,
                NULL,
                @statusPending,
                'MEDIUM',
                @amount,
                @reason,
                NULL,
                @payload,
                SYSDATETIME(),
                SYSDATETIME()
            );
            SELECT TOP 1 id FROM @inserted;
            """,
            [
                new SqlParameter("@requestedBy", access.UserId),
                new SqlParameter("@statusPending", FinanceApprovalStatusPending),
                new SqlParameter("@amount", requestedBaseSalary),
                new SqlParameter("@reason", reason),
                new SqlParameter("@payload", payloadJson)
            ]);

        if (!createdId.HasValue)
        {
            return BadRequest(new { message = "Failed to create initial salary approval request." });
        }

        return Ok(new
        {
            requestId = createdId.Value,
            status = FinanceApprovalStatusPending,
            staffId = staff.Id,
            staffName = staff.FullName,
            baseSalary = requestedBaseSalary,
            effectiveFrom = effectiveFromDate.ToString("yyyy-MM-dd"),
            message = "Initial salary request has been sent to admin for approval."
        });
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        var from = fromDate ?? VietnamTime.Now.Date.AddDays(-30);
        var to = toDate ?? VietnamTime.Now.Date.AddDays(1).AddTicks(-1);
        var revenue = (await _crud.QueryAsync(
            "SELECT ISNULL(SUM(TotalRevenue),0) AS revenue, ISNULL(SUM(NetProfit),0) AS net_profit, ISNULL(SUM(NetCashFlow),0) AS cash_flow FROM dbo.RevenueSnapshots WHERE SnapshotDate BETWEEN @f AND @t AND (@branchId IS NULL OR BranchId=@branchId);",
            [
                new SqlParameter("@f", from.Date),
                new SqlParameter("@t", to.Date),
                new SqlParameter("@branchId", (object?)access.BranchId ?? DBNull.Value)
            ])).FirstOrDefault();
        var payments = (await _crud.QueryAsync(
            """
            IF COL_LENGTH('dbo.Payments', 'Direction') IS NOT NULL
            BEGIN
                SELECT
                    ISNULL(SUM(CASE WHEN UPPER(ISNULL(p.Direction,''))='INFLOW' THEN p.Amount ELSE 0 END),0) AS collected,
                    ISNULL(SUM(CASE WHEN UPPER(ISNULL(p.Type,''))='REFUND' THEN ABS(p.Amount) ELSE 0 END),0) AS refund,
                    ISNULL(SUM(CASE WHEN UPPER(ISNULL(p.Type,''))='REVERSAL' THEN ABS(p.Amount) ELSE 0 END),0) AS reversal
                FROM dbo.Payments p
                LEFT JOIN dbo.Invoices i ON i.Id = p.InvoiceId
                LEFT JOIN dbo.ServiceOrders so ON so.Id = COALESCE(p.ServiceOrderId, i.ServiceOrderId)
                LEFT JOIN dbo.RetailOrders ro ON ro.Id = COALESCE(p.RetailOrderId, i.RetailOrderId)
                WHERE p.CreatedAt BETWEEN @f AND @t
                  AND UPPER(ISNULL(p.Status,'')) = @paymentCompletedStatus
                  AND (
                        @branchId IS NULL
                        OR so.ExecuteBranchId = @branchId
                        OR so.OriginBranchId = @branchId
                        OR ro.BranchId = @branchId
                      );
            END
            ELSE
            BEGIN
                SELECT
                    ISNULL(SUM(CASE WHEN UPPER(ISNULL(p.Type,'')) NOT IN ('REFUND','REVERSAL') THEN p.Amount ELSE 0 END),0) AS collected,
                    ISNULL(SUM(CASE WHEN UPPER(ISNULL(p.Type,''))='REFUND' THEN ABS(p.Amount) ELSE 0 END),0) AS refund,
                    ISNULL(SUM(CASE WHEN UPPER(ISNULL(p.Type,''))='REVERSAL' THEN ABS(p.Amount) ELSE 0 END),0) AS reversal
                FROM dbo.Payments p
                LEFT JOIN dbo.Invoices i ON i.Id = p.InvoiceId
                LEFT JOIN dbo.ServiceOrders so ON so.Id = COALESCE(p.ServiceOrderId, i.ServiceOrderId)
                LEFT JOIN dbo.RetailOrders ro ON ro.Id = COALESCE(p.RetailOrderId, i.RetailOrderId)
                WHERE p.CreatedAt BETWEEN @f AND @t
                  AND UPPER(ISNULL(p.Status,'')) = @paymentCompletedStatus
                  AND (
                        @branchId IS NULL
                        OR so.ExecuteBranchId = @branchId
                        OR so.OriginBranchId = @branchId
                        OR ro.BranchId = @branchId
                      );
            END
            """,
            [
                new SqlParameter("@f", from),
                new SqlParameter("@t", to),
                new SqlParameter("@branchId", (object?)access.BranchId ?? DBNull.Value),
                new SqlParameter("@paymentCompletedStatus", PaymentCompletedStatus)
            ])).FirstOrDefault();
        return Ok(new
        {
            fromDate = from,
            toDate = to,
            revenue = AsDecimal(revenue, "revenue"),
            netProfit = AsDecimal(revenue, "netProfit"),
            cashFlow = AsDecimal(revenue, "cashFlow"),
            collected = AsDecimal(payments, "collected"),
            refund = AsDecimal(payments, "refund"),
            reversal = AsDecimal(payments, "reversal")
        });
    }

    private async Task SyncPayrollPeriodStatusAsync(long payrollPeriodId)
    {
        if (payrollPeriodId <= 0) return;

        var periodRow = await _crud.GetByIdAsync("payroll-periods", payrollPeriodId, null);
        if (periodRow is null) return;

        var currentStatus = AsString(periodRow, "status");
        if (currentStatus.Equals(PayrollPeriodStatusLocked, StringComparison.OrdinalIgnoreCase))
            return;

        var summary = (await _crud.QueryAsync(
            """
            SELECT
                COUNT(*) AS totalCount,
                SUM(CASE WHEN UPPER(ISNULL(status,'')) IN (@runStatusDraft,@runStatusCalculated) THEN 1 ELSE 0 END) AS draftCount,
                SUM(CASE WHEN UPPER(ISNULL(status,''))=@runStatusApproved THEN 1 ELSE 0 END) AS approvedCount,
                SUM(CASE WHEN UPPER(ISNULL(status,'')) IN (@runStatusPaid,@runStatusLocked) THEN 1 ELSE 0 END) AS paidCount
            FROM dbo.payroll_runs
            WHERE payroll_period_id=@periodId;
            """,
            [
                new SqlParameter("@periodId", payrollPeriodId),
                new SqlParameter("@runStatusDraft", PayrollRunStatusDraft),
                new SqlParameter("@runStatusCalculated", PayrollRunStatusCalculated),
                new SqlParameter("@runStatusApproved", PayrollRunStatusApproved),
                new SqlParameter("@runStatusPaid", PayrollRunStatusPaid),
                new SqlParameter("@runStatusLocked", PayrollRunStatusLocked)
            ])).FirstOrDefault();

        var totalCount = Math.Max(0, AsInt(summary, "totalCount"));
        var draftCount = Math.Max(0, AsInt(summary, "draftCount"));
        var approvedCount = Math.Max(0, AsInt(summary, "approvedCount"));
        var paidCount = Math.Max(0, AsInt(summary, "paidCount"));

        var nextStatus = PayrollPeriodStatusOpen;
        if (totalCount > 0)
        {
            if (draftCount > 0)
            {
                nextStatus = PayrollPeriodStatusOpen;
            }
            else if (paidCount >= totalCount)
            {
                nextStatus = PayrollPeriodStatusPaid;
            }
            else if (approvedCount > 0 || paidCount > 0)
            {
                nextStatus = PayrollPeriodStatusApproved;
            }
        }

        if (currentStatus.Equals(nextStatus, StringComparison.OrdinalIgnoreCase))
            return;

        await _crud.ExecuteAsync(
            "UPDATE dbo.payroll_periods SET status=@status WHERE id=@id;",
            [
                new SqlParameter("@status", nextStatus),
                new SqlParameter("@id", payrollPeriodId)
            ]);
    }

    private static readonly IReadOnlyDictionary<string, decimal> VietnamRegionalMinimumWageByZone =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["I"] = 4_960_000m,
            ["II"] = 4_410_000m,
            ["III"] = 3_860_000m,
            ["IV"] = 3_450_000m
        };

    private static readonly VietnamPayrollPolicy VietnamPayrollPolicyPre2026 = new(
        EmployeeBhxhRate: 0.08m,
        EmployeeBhytRate: 0.015m,
        EmployeeBhtnRate: 0.01m,
        EmployerBhxhRate: 0.175m,
        EmployerBhytRate: 0.03m,
        EmployerBhtnRate: 0.01m,
        EmployerOaidRate: 0.005m,
        PersonalDeduction: 11_000_000m,
        DependentDeduction: 4_400_000m,
        SocialInsuranceBaseReference: 2_340_000m,
        RegionalMinimumWageByZone: VietnamRegionalMinimumWageByZone);

    private static readonly VietnamPayrollPolicy VietnamPayrollPolicy2026 = VietnamPayrollPolicyPre2026 with
    {
        PersonalDeduction = 15_500_000m,
        DependentDeduction = 6_200_000m
    };

    private static readonly IReadOnlyList<PitBracket> VietnamPitBrackets =
    [
        new PitBracket(5_000_000m, 0.05m),
        new PitBracket(10_000_000m, 0.10m),
        new PitBracket(18_000_000m, 0.15m),
        new PitBracket(32_000_000m, 0.20m),
        new PitBracket(52_000_000m, 0.25m),
        new PitBracket(80_000_000m, 0.30m),
        new PitBracket(decimal.MaxValue, 0.35m)
    ];

    private async Task<PayrollRunResolution> ResolveOrCreateDraftPayrollRunAsync(long payrollPeriodId, long? branchId, long calculatedByUserId)
    {
        var existing = (await _crud.QueryAsync(
            """
            SELECT TOP 1 id, run_no
            FROM dbo.payroll_runs
            WHERE payroll_period_id=@payrollPeriodId
              AND (@branchId IS NULL OR branch_id=@branchId)
              AND UPPER(ISNULL(status,''))=@payrollRunStatusDraft
            ORDER BY run_no DESC, id DESC;
            """,
            [
                new SqlParameter("@payrollPeriodId", payrollPeriodId),
                new SqlParameter("@branchId", (object?)branchId ?? DBNull.Value),
                new SqlParameter("@payrollRunStatusDraft", PayrollRunStatusDraft)
            ])).FirstOrDefault();

        var existingRunId = AsLong(existing, "id");
        if (existingRunId.HasValue && existingRunId.Value > 0)
        {
            var existingRunNo = AsLong(existing, "runNo");
            return new PayrollRunResolution(existingRunId.Value, existingRunNo.GetValueOrDefault(1), false);
        }

        var nextRunNo = await _crud.ScalarAsync<long?>(
            """
            SELECT ISNULL(MAX(run_no), 0) + 1
            FROM dbo.payroll_runs
            WHERE payroll_period_id=@payrollPeriodId
              AND (@branchId IS NULL OR branch_id=@branchId);
            """,
            [
                new SqlParameter("@payrollPeriodId", payrollPeriodId),
                new SqlParameter("@branchId", (object?)branchId ?? DBNull.Value)
            ]);
        var runNo = Math.Max(1, nextRunNo.GetValueOrDefault(1));

        var runId = await _crud.ScalarAsync<long?>(
            """
            DECLARE @inserted TABLE(id bigint);
            INSERT INTO dbo.payroll_runs
            (
                payroll_period_id,
                branch_id,
                run_no,
                status,
                calculated_by,
                calculated_at
            )
            OUTPUT INSERTED.id INTO @inserted(id)
            VALUES
            (
                @payrollPeriodId,
                @branchId,
                @runNo,
                @payrollRunStatusDraft,
                @calculatedBy,
                SYSDATETIME()
            );
            SELECT TOP 1 id FROM @inserted;
            """,
            [
                new SqlParameter("@payrollPeriodId", payrollPeriodId),
                new SqlParameter("@branchId", (object?)branchId ?? DBNull.Value),
                new SqlParameter("@runNo", runNo),
                new SqlParameter("@payrollRunStatusDraft", PayrollRunStatusDraft),
                new SqlParameter("@calculatedBy", calculatedByUserId)
            ]);

        if (!runId.HasValue || runId.Value <= 0)
        {
            throw new InvalidOperationException("Failed to resolve payroll run for batch calculation.");
        }

        return new PayrollRunResolution(runId.Value, runNo, true);
    }

    private async Task<Dictionary<long, decimal>> LoadProfileBaseSalaryMapAsync(IReadOnlyCollection<long> staffIds, long? branchId, DateOnly asOfDate)
    {
        var result = new Dictionary<long, decimal>();
        if (staffIds.Count == 0) return result;
        if (!_crud.TryGetConfig("staff-salary-profiles", out var salaryCfg) || !await _crud.TableExistsAsync(salaryCfg))
            return result;

        var idList = BuildLongListSqlLiteral(staffIds);
        if (idList == "0") return result;

        var asOf = asOfDate.ToDateTime(TimeOnly.MinValue);
        var rows = await _crud.QueryAsync(
            $"""
            WITH ranked AS
            (
                SELECT
                    staff_id,
                    base_salary,
                    ROW_NUMBER() OVER (PARTITION BY staff_id ORDER BY effective_from DESC, id DESC) AS rn
                FROM dbo.staff_salary_profiles
                WHERE staff_id IN ({idList})
                  AND (@branchId IS NULL OR branch_id=@branchId OR branch_id IS NULL)
                  AND effective_from <= @asOf
                  AND (effective_to IS NULL OR effective_to >= @asOf)
                  AND UPPER(ISNULL(status, @staffSalaryProfileStatusActive))=@staffSalaryProfileStatusActive
            )
            SELECT staff_id, base_salary
            FROM ranked
            WHERE rn=1;
            """,
            [
                new SqlParameter("@branchId", (object?)branchId ?? DBNull.Value),
                new SqlParameter("@asOf", asOf),
                new SqlParameter("@staffSalaryProfileStatusActive", StaffSalaryProfileStatusActive)
            ]);

        foreach (var row in rows)
        {
            var staffId = AsLong(row, "staffId");
            if (!staffId.HasValue || staffId.Value <= 0) continue;
            var baseSalary = AsDecimal(row, "baseSalary");
            if (baseSalary > 0m)
            {
                result[staffId.Value] = baseSalary;
            }
        }

        return result;
    }

    private async Task<Dictionary<long, ContractCompensationSnapshot>> LoadContractCompensationMapAsync(IReadOnlyCollection<long> staffIds, DateOnly asOfDate)
    {
        var result = new Dictionary<long, ContractCompensationSnapshot>();
        if (staffIds.Count == 0) return result;
        if (!_crud.TryGetConfig("staff-contracts", out var contractCfg) || !await _crud.TableExistsAsync(contractCfg))
            return result;

        var idList = BuildLongListSqlLiteral(staffIds);
        if (idList == "0") return result;

        var asOf = asOfDate.ToDateTime(TimeOnly.MinValue);
        var rows = await _crud.QueryAsync(
            $"""
            WITH ranked AS
            (
                SELECT
                    staff_id,
                    base_salary,
                    allowance_amount,
                    ROW_NUMBER() OVER (PARTITION BY staff_id ORDER BY effective_from DESC, id DESC) AS rn
                FROM dbo.staff_contracts
                WHERE staff_id IN ({idList})
                  AND effective_from <= @asOf
                  AND (effective_to IS NULL OR effective_to >= @asOf)
                  AND UPPER(ISNULL(status, @staffContractStatusActive))=@staffContractStatusActive
            )
            SELECT staff_id, base_salary, allowance_amount
            FROM ranked
            WHERE rn=1;
            """,
            [
                new SqlParameter("@asOf", asOf),
                new SqlParameter("@staffContractStatusActive", StaffContractStatusActive)
            ]);

        foreach (var row in rows)
        {
            var staffId = AsLong(row, "staffId");
            if (!staffId.HasValue || staffId.Value <= 0) continue;
            var baseSalary = Math.Max(0m, AsDecimal(row, "baseSalary"));
            var allowanceAmount = Math.Max(0m, AsDecimal(row, "allowanceAmount"));
            result[staffId.Value] = new ContractCompensationSnapshot(baseSalary, allowanceAmount);
        }

        return result;
    }

    private async Task<Dictionary<long, decimal>> LoadStaffTierBaseSalaryMapAsync(IReadOnlyCollection<long> staffIds)
    {
        var result = new Dictionary<long, decimal>();
        if (staffIds.Count == 0) return result;

        var staffIdSet = staffIds
            .Where(id => id > 0)
            .ToHashSet();
        if (staffIdSet.Count == 0) return result;

        var rows = await (
            from staff in _db.Staffs.AsNoTracking()
            join tier in _db.StaffTiers.AsNoTracking() on staff.StaffTierId equals tier.Id
            where staffIdSet.Contains(staff.Id)
                  && staff.IsDeleted != true
                  && tier.IsDeleted != true
                  && tier.BaseSalary.HasValue
                  && tier.BaseSalary.Value > 0m
            select new
            {
                staff.Id,
                BaseSalary = tier.BaseSalary!.Value
            }).ToListAsync();

        foreach (var row in rows)
        {
            result[row.Id] = row.BaseSalary;
        }

        return result;
    }

    private async Task<Dictionary<long, decimal>> LoadOvertimeAmountMapAsync(IReadOnlyCollection<long> staffIds, DateOnly periodStartDate, DateOnly periodEndDate)
    {
        var result = new Dictionary<long, decimal>();
        if (staffIds.Count == 0) return result;
        if (!_crud.TryGetConfig("overtime-entries", out var overtimeCfg) || !await _crud.TableExistsAsync(overtimeCfg))
            return result;

        var idList = BuildLongListSqlLiteral(staffIds);
        if (idList == "0") return result;

        var fromDate = periodStartDate.ToDateTime(TimeOnly.MinValue);
        var toDate = periodEndDate.ToDateTime(TimeOnly.MaxValue);
        var rows = await _crud.QueryAsync(
            $"""
            SELECT
                staff_id,
                SUM(ISNULL(amount, 0)) AS overtime_amount
            FROM dbo.overtime_entries
            WHERE staff_id IN ({idList})
              AND work_date >= @fromDate
              AND work_date <= @toDate
              AND (
                    status IS NULL
                    OR LTRIM(RTRIM(ISNULL(status, ''))) = ''
                    OR UPPER(ISNULL(status, '')) IN (@adjustmentApprovedStatus, @adjustmentPaidStatus, @journalPostedStatus, @paymentCompletedStatus)
                  )
            GROUP BY staff_id;
            """,
            [
                new SqlParameter("@fromDate", fromDate),
                new SqlParameter("@toDate", toDate),
                new SqlParameter("@adjustmentApprovedStatus", AdjustmentApprovedStatus),
                new SqlParameter("@adjustmentPaidStatus", AdjustmentPaidStatus),
                new SqlParameter("@journalPostedStatus", JournalStatusPosted),
                new SqlParameter("@paymentCompletedStatus", PaymentCompletedStatus)
            ]);

        foreach (var row in rows)
        {
            var staffId = AsLong(row, "staffId");
            if (!staffId.HasValue || staffId.Value <= 0) continue;
            result[staffId.Value] = Math.Max(0m, AsDecimal(row, "overtimeAmount"));
        }

        return result;
    }

    private async Task<Dictionary<long, decimal>> LoadAttendanceDeductionMapAsync(IReadOnlyCollection<long> staffIds, DateOnly periodStartDate, DateOnly periodEndDate)
    {
        var result = new Dictionary<long, decimal>();
        if (staffIds.Count == 0) return result;
        if (!_crud.TryGetConfig("attendance-adjustments", out var attendanceCfg) || !await _crud.TableExistsAsync(attendanceCfg))
            return result;

        var idList = BuildLongListSqlLiteral(staffIds);
        if (idList == "0") return result;

        var fromDate = periodStartDate.ToDateTime(TimeOnly.MinValue);
        var toDate = periodEndDate.ToDateTime(TimeOnly.MaxValue);
        var rows = await _crud.QueryAsync(
            $"""
            SELECT
                staff_id,
                SUM
                (
                    CASE
                        WHEN amount_impact IS NULL THEN 0
                        WHEN amount_impact < 0 THEN ABS(amount_impact)
                        WHEN UPPER(ISNULL(adjustment_type, '')) IN ('DEDUCTION', 'ABSENT', 'LATE', 'UNPAID_LEAVE', 'PENALTY') THEN amount_impact
                        ELSE 0
                    END
                ) AS attendance_deduction
            FROM dbo.attendance_adjustments
            WHERE staff_id IN ({idList})
              AND work_date >= @fromDate
              AND work_date <= @toDate
              AND (
                    status IS NULL
                    OR LTRIM(RTRIM(ISNULL(status, ''))) = ''
                    OR UPPER(ISNULL(status, '')) IN (@adjustmentApprovedStatus, @adjustmentPaidStatus, @journalPostedStatus, @paymentCompletedStatus)
                  )
            GROUP BY staff_id;
            """,
            [
                new SqlParameter("@fromDate", fromDate),
                new SqlParameter("@toDate", toDate),
                new SqlParameter("@adjustmentApprovedStatus", AdjustmentApprovedStatus),
                new SqlParameter("@adjustmentPaidStatus", AdjustmentPaidStatus),
                new SqlParameter("@journalPostedStatus", JournalStatusPosted),
                new SqlParameter("@paymentCompletedStatus", PaymentCompletedStatus)
            ]);

        foreach (var row in rows)
        {
            var staffId = AsLong(row, "staffId");
            if (!staffId.HasValue || staffId.Value <= 0) continue;
            result[staffId.Value] = Math.Max(0m, AsDecimal(row, "attendanceDeduction"));
        }

        return result;
    }

    private async Task<Dictionary<long, decimal>> LoadSalaryAdvanceDeductionMapAsync(IReadOnlyCollection<long> staffIds, long payrollPeriodId)
    {
        var result = new Dictionary<long, decimal>();
        if (staffIds.Count == 0) return result;
        if (!_crud.TryGetConfig("salary-advances", out var advanceCfg) || !await _crud.TableExistsAsync(advanceCfg))
            return result;

        var idList = BuildLongListSqlLiteral(staffIds);
        if (idList == "0") return result;

        var rows = await _crud.QueryAsync(
            $"""
            SELECT
                staff_id,
                SUM(ISNULL(amount, 0)) AS advance_deduction
            FROM dbo.salary_advances
            WHERE staff_id IN ({idList})
              AND deduct_from_payroll_period_id=@payrollPeriodId
              AND (
                    status IS NULL
                    OR LTRIM(RTRIM(ISNULL(status, ''))) = ''
                    OR UPPER(ISNULL(status, '')) IN (@salaryAdvanceStatusApproved, @salaryAdvanceStatusDeducting)
                  )
            GROUP BY staff_id;
            """,
            [
                new SqlParameter("@payrollPeriodId", payrollPeriodId),
                new SqlParameter("@salaryAdvanceStatusApproved", SalaryAdvanceStatusApproved),
                new SqlParameter("@salaryAdvanceStatusDeducting", SalaryAdvanceStatusDeducting)
            ]);

        foreach (var row in rows)
        {
            var staffId = AsLong(row, "staffId");
            if (!staffId.HasValue || staffId.Value <= 0) continue;
            result[staffId.Value] = Math.Max(0m, AsDecimal(row, "advanceDeduction"));
        }

        return result;
    }

    private async Task<Dictionary<long, decimal>> LoadLoanDeductionMapAsync(IReadOnlyCollection<long> staffIds, long payrollPeriodId)
    {
        var result = new Dictionary<long, decimal>();
        if (staffIds.Count == 0) return result;
        if (!_crud.TryGetConfig("employee-loans", out var loanCfg) || !await _crud.TableExistsAsync(loanCfg))
            return result;

        var idList = BuildLongListSqlLiteral(staffIds);
        if (idList == "0") return result;

        var rows = await _crud.QueryAsync(
            $"""
            SELECT
                staff_id,
                SUM
                (
                    CASE
                        WHEN ISNULL(monthly_deduction, 0) > 0
                            THEN CASE
                                     WHEN ISNULL(outstanding_amount, 0) > 0 AND monthly_deduction > outstanding_amount THEN outstanding_amount
                                     ELSE monthly_deduction
                                 END
                        ELSE ISNULL(outstanding_amount, 0)
                    END
                ) AS loan_deduction
            FROM dbo.employee_loans
            WHERE staff_id IN ({idList})
              AND (start_payroll_period_id IS NULL OR start_payroll_period_id <= @payrollPeriodId)
              AND ISNULL(outstanding_amount, 0) > 0
              AND UPPER(ISNULL(status, @employeeLoanStatusActive)) = @employeeLoanStatusActive
            GROUP BY staff_id;
            """,
            [
                new SqlParameter("@payrollPeriodId", payrollPeriodId),
                new SqlParameter("@employeeLoanStatusActive", EmployeeLoanStatusActive)
            ]);

        foreach (var row in rows)
        {
            var staffId = AsLong(row, "staffId");
            if (!staffId.HasValue || staffId.Value <= 0) continue;
            result[staffId.Value] = Math.Max(0m, AsDecimal(row, "loanDeduction"));
        }

        return result;
    }

    private async Task<Dictionary<long, int>> LoadDependentCountMapAsync(IReadOnlyCollection<long> staffIds, DateOnly asOfDate)
    {
        var result = new Dictionary<long, int>();
        if (staffIds.Count == 0) return result;
        var idList = BuildLongListSqlLiteral(staffIds);
        if (idList == "0") return result;

        var asOf = asOfDate.ToDateTime(TimeOnly.MinValue);

        if (_crud.TryGetConfig("pit-profiles", out var pitCfg) && await _crud.TableExistsAsync(pitCfg))
        {
            var pitRows = await _crud.QueryAsync(
                $"""
                WITH ranked AS
                (
                    SELECT
                        staff_id,
                        dependent_count,
                        ROW_NUMBER() OVER (PARTITION BY staff_id ORDER BY effective_from DESC, id DESC) AS rn
                    FROM dbo.pit_profiles
                    WHERE staff_id IN ({idList})
                      AND (effective_from IS NULL OR effective_from <= @asOf)
                      AND (effective_to IS NULL OR effective_to >= @asOf)
                )
                SELECT staff_id, dependent_count
                FROM ranked
                WHERE rn=1;
                """,
                [new SqlParameter("@asOf", asOf)]);

            foreach (var row in pitRows)
            {
                var staffId = AsLong(row, "staffId");
                if (!staffId.HasValue || staffId.Value <= 0) continue;
                result[staffId.Value] = Math.Max(0, AsInt(row, "dependentCount"));
            }
        }

        if (_crud.TryGetConfig("employee-dependents", out var dependentCfg) && await _crud.TableExistsAsync(dependentCfg))
        {
            var dependentRows = await _crud.QueryAsync(
                $"""
                SELECT
                    staff_id,
                    COUNT(*) AS dependent_count
                FROM dbo.employee_dependents
                WHERE staff_id IN ({idList})
                  AND (effective_from IS NULL OR effective_from <= @asOf)
                  AND (effective_to IS NULL OR effective_to >= @asOf)
                GROUP BY staff_id;
                """,
                [new SqlParameter("@asOf", asOf)]);

            foreach (var row in dependentRows)
            {
                var staffId = AsLong(row, "staffId");
                if (!staffId.HasValue || staffId.Value <= 0) continue;
                if (result.ContainsKey(staffId.Value)) continue;
                result[staffId.Value] = Math.Max(0, AsInt(row, "dependentCount"));
            }
        }

        return result;
    }

    private static string BuildLongListSqlLiteral(IEnumerable<long> values)
    {
        var normalized = values
            .Where(value => value > 0)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        return normalized.Length == 0 ? "0" : string.Join(", ", normalized);
    }

    private static bool IsPayrollActiveStaff(StaffPayrollSnapshot staff, DateOnly asOfDate)
    {
        if (staff.TerminationDate.HasValue && staff.TerminationDate.Value < asOfDate)
        {
            return false;
        }

        var normalizedStatus = (staff.Status ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalizedStatus))
        {
            return true;
        }

        if (normalizedStatus is StaffStatusActive or StaffStatusOnLeave)
        {
            return true;
        }

        return false;
    }

    private static decimal ResolveSalaryCoefficientForYears(int yearsOfService, IReadOnlyList<SalaryCoefficientThreshold> thresholds)
    {
        var coefficient = 1.0000m;
        foreach (var threshold in thresholds)
        {
            if (threshold.YearsOfService <= yearsOfService && threshold.CoefficientValue > 0m)
            {
                coefficient = threshold.CoefficientValue;
            }
        }

        return coefficient > 0m ? coefficient : 1.0000m;
    }

    private static VietnamPayrollPolicy ResolveVietnamPayrollPolicy(DateOnly asOfDate)
    {
        return asOfDate >= new DateOnly(2026, 1, 1) ? VietnamPayrollPolicy2026 : VietnamPayrollPolicyPre2026;
    }

    private static string NormalizeInsuranceZone(string? insuranceZone)
    {
        var text = (insuranceZone ?? "").Trim().ToUpperInvariant();
        return text is "II" or "III" or "IV" ? text : "I";
    }

    private static decimal CalculatePitByProgressiveBracket(decimal taxableIncome)
    {
        var remaining = Math.Max(0m, taxableIncome);
        if (remaining <= 0m) return 0m;

        decimal lowerBound = 0m;
        decimal totalTax = 0m;
        foreach (var bracket in VietnamPitBrackets)
        {
            if (remaining <= 0m) break;
            var range = bracket.UpperLimit == decimal.MaxValue ? remaining : bracket.UpperLimit - lowerBound;
            var taxableInRange = Math.Min(remaining, range);
            if (taxableInRange > 0m)
            {
                totalTax += taxableInRange * bracket.Rate;
                remaining -= taxableInRange;
            }
            lowerBound = bracket.UpperLimit;
        }

        return RoundVnd(totalTax);
    }

    private static VietnamStatutoryAmounts CalculateVietnamStatutoryAmounts(
        decimal baseSalaryForInsurance,
        decimal grossIncome,
        int dependentCount,
        VietnamPayrollPolicy policy,
        string insuranceZone)
    {
        var zone = NormalizeInsuranceZone(insuranceZone);
        var normalizedDependents = Math.Max(0, dependentCount);
        var socialInsuranceCap = 20m * policy.SocialInsuranceBaseReference;
        var regionalMinimumWage = policy.RegionalMinimumWageByZone.TryGetValue(zone, out var wage) && wage > 0m
            ? wage
            : policy.RegionalMinimumWageByZone["I"];
        var unemploymentCap = 20m * regionalMinimumWage;
        var baseSalary = Math.Max(0m, baseSalaryForInsurance);
        var socialInsurableBase = Math.Min(baseSalary, socialInsuranceCap);
        var unemploymentInsurableBase = Math.Min(baseSalary, unemploymentCap);

        var employeeBhxh = RoundVnd(socialInsurableBase * policy.EmployeeBhxhRate);
        var employeeBhyt = RoundVnd(socialInsurableBase * policy.EmployeeBhytRate);
        var employeeBhtn = RoundVnd(unemploymentInsurableBase * policy.EmployeeBhtnRate);
        var employeeInsuranceTotal = RoundVnd(employeeBhxh + employeeBhyt + employeeBhtn);

        var employerBhxh = RoundVnd(socialInsurableBase * policy.EmployerBhxhRate);
        var employerBhyt = RoundVnd(socialInsurableBase * policy.EmployerBhytRate);
        var employerBhtn = RoundVnd(unemploymentInsurableBase * policy.EmployerBhtnRate);
        var employerOaid = RoundVnd(socialInsurableBase * policy.EmployerOaidRate);
        var employerInsuranceTotal = RoundVnd(employerBhxh + employerBhyt + employerBhtn + employerOaid);

        var taxableIncome = Math.Max(
            0m,
            Math.Max(0m, grossIncome) -
            employeeInsuranceTotal -
            policy.PersonalDeduction -
            normalizedDependents * policy.DependentDeduction);
        var pitAmount = CalculatePitByProgressiveBracket(taxableIncome);

        return new VietnamStatutoryAmounts(employeeInsuranceTotal, employerInsuranceTotal, pitAmount);
    }

    private static decimal RoundVnd(decimal value)
    {
        return Math.Max(0m, Math.Round(value, 0, MidpointRounding.AwayFromZero));
    }

    private async Task<string?> ValidateSupplierInvoice3WayMatchingAsync(JsonElement payload, long? branchId, long? existingId = null)
    {
        long? supplierId = TryGetLong(payload, "supplierId", "supplier_id");
        long? purchaseOrderId = TryGetLong(payload, "purchaseOrderId", "purchase_order_id");
        decimal? totalAmount = TryGetDecimal(payload, "totalAmount", "total_amount");

        if (existingId.HasValue)
        {
            var existing = await _crud.GetByIdAsync("supplier-invoices", existingId.Value, branchId);
            supplierId ??= AsLong(existing, "supplierId");
            purchaseOrderId ??= AsLong(existing, "purchaseOrderId");
            totalAmount ??= AsDecimal(existing, "totalAmount");
        }

        if (!supplierId.HasValue || supplierId.Value <= 0)
        {
            return "supplierId is required.";
        }

        if (!purchaseOrderId.HasValue || purchaseOrderId.Value <= 0)
        {
            return "purchaseOrderId is required for 3-way matching.";
        }

        if (!totalAmount.HasValue || totalAmount.Value <= 0m)
        {
            return "totalAmount must be > 0 for 3-way matching.";
        }

        var purchaseOrderRow = (await _crud.QueryAsync(
            """
            SELECT TOP 1
                po.id,
                po.supplier_id AS supplierId,
                po.branch_id AS branchId
            FROM dbo.purchase_orders po
            WHERE po.id = @purchaseOrderId
              AND (@branchId IS NULL OR po.branch_id = @branchId);
            """,
            [
                new SqlParameter("@purchaseOrderId", purchaseOrderId.Value),
                new SqlParameter("@branchId", (object?)branchId ?? DBNull.Value)
            ])).FirstOrDefault();

        if (purchaseOrderRow is null)
        {
            return $"purchase_order #{purchaseOrderId.Value} not found in current branch scope.";
        }

        var poSupplierId = AsLong(purchaseOrderRow, "supplierId");
        if (!poSupplierId.HasValue || poSupplierId.Value != supplierId.Value)
        {
            return "supplierId does not match the supplier on purchase order.";
        }

        var totalsRow = (await _crud.QueryAsync(
            """
            ;WITH po_total AS (
                SELECT
                    CAST(SUM(
                        COALESCE(
                            poi.total_amount,
                            poi.TotalAmount,
                            (COALESCE(poi.ordered_quantity, poi.OrderedQuantity, poi.quantity, poi.Quantity, 0) *
                             COALESCE(poi.unit_cost, poi.UnitCost, poi.unit_price, poi.UnitPrice, 0))
                        )
                    ) AS DECIMAL(18,2)) AS po_total
                FROM dbo.purchase_order_items poi
                WHERE poi.purchase_order_id = @purchaseOrderId
            ),
            grn_total AS (
                SELECT
                    CAST(SUM(
                        COALESCE(
                            grni.line_total,
                            grni.LineTotal,
                            (COALESCE(grni.received_quantity, grni.ReceivedQuantity, grni.quantity, grni.Quantity, 0) *
                             COALESCE(grni.unit_cost, grni.UnitCost, 0))
                        )
                    ) AS DECIMAL(18,2)) AS grn_total
                FROM dbo.goods_receipt_note_items grni
                INNER JOIN dbo.goods_receipt_notes grn ON grn.id = grni.grn_id
                WHERE grn.purchase_order_id = @purchaseOrderId
            )
            SELECT
                COALESCE((SELECT po_total FROM po_total), 0) AS poTotal,
                COALESCE((SELECT grn_total FROM grn_total), 0) AS grnTotal;
            """,
            [new SqlParameter("@purchaseOrderId", purchaseOrderId.Value)])).FirstOrDefault();

        var poTotal = AsDecimal(totalsRow, "poTotal");
        var grnTotal = AsDecimal(totalsRow, "grnTotal");

        if (poTotal <= 0m)
        {
            return "purchase_order does not have valid line totals for matching.";
        }

        if (grnTotal <= 0m)
        {
            return "GRN is required before creating supplier invoice (3-way matching).";
        }

        if (totalAmount.Value > poTotal + 0.01m)
        {
            return $"Supplier invoice total ({totalAmount.Value:0.##}) exceeds PO total ({poTotal:0.##}).";
        }

        if (totalAmount.Value > grnTotal + 0.01m)
        {
            return $"Supplier invoice total ({totalAmount.Value:0.##}) exceeds received GRN total ({grnTotal:0.##}).";
        }

        // Optional deep validation when header API receives line details.
        if (payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty("details", out var detailItems) &&
            detailItems.ValueKind == JsonValueKind.Array)
        {
            foreach (var line in detailItems.EnumerateArray())
            {
                var lineError = await ValidateSupplierInvoiceDetail3WayMatchingAsync(line, branchId);
                if (!string.IsNullOrWhiteSpace(lineError))
                {
                    return lineError;
                }
            }
        }

        return null;
    }

    private async Task<string?> ValidateSupplierInvoiceDetail3WayMatchingAsync(JsonElement payload, long? branchId, long? existingId = null)
    {
        long? supplierInvoiceId = TryGetLong(payload, "supplierInvoiceId", "supplier_invoice_id");
        long? purchaseOrderItemId = TryGetLong(payload, "purchaseOrderItemId", "purchase_order_item_id");
        decimal? quantity = TryGetDecimal(payload, "quantity");
        decimal? unitCost = TryGetDecimal(payload, "unitCost", "unit_cost", "unitPrice", "unit_price");
        decimal? lineTotal = TryGetDecimal(payload, "lineTotal", "line_total", "totalAmount", "total_amount");

        if (existingId.HasValue)
        {
            var existing = await _crud.GetByIdAsync("supplier-invoice-details", existingId.Value, branchId);
            supplierInvoiceId ??= AsLong(existing, "supplierInvoiceId");
            purchaseOrderItemId ??= AsLong(existing, "purchaseOrderItemId");
            quantity ??= AsDecimal(existing, "quantity");
            unitCost ??= AsDecimal(existing, "unitCost");
            lineTotal ??= AsDecimal(existing, "lineTotal");
        }

        if (!supplierInvoiceId.HasValue || supplierInvoiceId.Value <= 0)
        {
            return "supplierInvoiceId is required.";
        }

        if (!purchaseOrderItemId.HasValue || purchaseOrderItemId.Value <= 0)
        {
            return "purchaseOrderItemId is required for 3-way matching.";
        }

        if (!quantity.HasValue || quantity.Value <= 0m)
        {
            return "quantity must be > 0.";
        }

        if (!unitCost.HasValue || unitCost.Value < 0m)
        {
            return "unitCost must be >= 0.";
        }

        var referenceRow = (await _crud.QueryAsync(
            """
            SELECT TOP 1
                sid.id AS supplierInvoiceDetailId,
                sid.supplier_invoice_id AS supplierInvoiceId,
                si.purchase_order_id AS purchaseOrderId,
                si.branch_id AS supplierInvoiceBranchId,
                poi.id AS purchaseOrderItemId,
                CAST(COALESCE(poi.ordered_quantity, poi.OrderedQuantity, poi.quantity, poi.Quantity, 0) AS DECIMAL(18,4)) AS orderedQuantity,
                CAST(COALESCE(poi.unit_cost, poi.UnitCost, poi.unit_price, poi.UnitPrice, 0) AS DECIMAL(18,4)) AS poUnitCost
            FROM dbo.supplier_invoices si
            LEFT JOIN dbo.supplier_invoice_details sid ON sid.supplier_invoice_id = si.id
            INNER JOIN dbo.purchase_order_items poi ON poi.id = @purchaseOrderItemId
            WHERE si.id = @supplierInvoiceId
              AND (@branchId IS NULL OR si.branch_id = @branchId);
            """,
            [
                new SqlParameter("@supplierInvoiceId", supplierInvoiceId.Value),
                new SqlParameter("@purchaseOrderItemId", purchaseOrderItemId.Value),
                new SqlParameter("@branchId", (object?)branchId ?? DBNull.Value)
            ])).FirstOrDefault();

        if (referenceRow is null)
        {
            return "Supplier invoice or purchase order item not found in current branch scope.";
        }

        var purchaseOrderId = AsLong(referenceRow, "purchaseOrderId");
        var orderedQuantity = AsDecimal(referenceRow, "orderedQuantity");
        var poUnitCost = AsDecimal(referenceRow, "poUnitCost");

        if (!purchaseOrderId.HasValue || purchaseOrderId.Value <= 0)
        {
            return "Supplier invoice is missing purchaseOrderId reference.";
        }

        var receivedQuantity = await _crud.ScalarAsync<decimal?>(
            """
            SELECT CAST(COALESCE(SUM(COALESCE(grni.received_quantity, grni.ReceivedQuantity, grni.quantity, grni.Quantity, 0)), 0) AS DECIMAL(18,4))
            FROM dbo.goods_receipt_note_items grni
            INNER JOIN dbo.goods_receipt_notes grn ON grn.id = grni.grn_id
            WHERE grn.purchase_order_id = @purchaseOrderId
              AND grni.purchase_order_item_id = @purchaseOrderItemId;
            """,
            [
                new SqlParameter("@purchaseOrderId", purchaseOrderId.Value),
                new SqlParameter("@purchaseOrderItemId", purchaseOrderItemId.Value)
            ]) ?? 0m;

        var alreadyInvoicedQty = await _crud.ScalarAsync<decimal?>(
            """
            SELECT CAST(COALESCE(SUM(COALESCE(sid.quantity, sid.Quantity, 0)), 0) AS DECIMAL(18,4))
            FROM dbo.supplier_invoice_details sid
            WHERE sid.supplier_invoice_id = @supplierInvoiceId
              AND sid.purchase_order_item_id = @purchaseOrderItemId
              AND (@existingId IS NULL OR sid.id <> @existingId);
            """,
            [
                new SqlParameter("@supplierInvoiceId", supplierInvoiceId.Value),
                new SqlParameter("@purchaseOrderItemId", purchaseOrderItemId.Value),
                new SqlParameter("@existingId", (object?)existingId ?? DBNull.Value)
            ]) ?? 0m;

        var effectiveQtyAfterSave = alreadyInvoicedQty + quantity.Value;
        if (orderedQuantity > 0m && effectiveQtyAfterSave > orderedQuantity + 0.0001m)
        {
            return $"Invoiced quantity ({effectiveQtyAfterSave:0.####}) exceeds PO ordered quantity ({orderedQuantity:0.####}) for purchaseOrderItemId={purchaseOrderItemId.Value}.";
        }

        if (receivedQuantity <= 0m)
        {
            return $"GRN quantity is missing for purchaseOrderItemId={purchaseOrderItemId.Value}.";
        }

        if (effectiveQtyAfterSave > receivedQuantity + 0.0001m)
        {
            return $"Invoiced quantity ({effectiveQtyAfterSave:0.####}) exceeds received GRN quantity ({receivedQuantity:0.####}) for purchaseOrderItemId={purchaseOrderItemId.Value}.";
        }

        if (poUnitCost > 0m && unitCost.Value > poUnitCost + 0.01m)
        {
            return $"unitCost ({unitCost.Value:0.####}) exceeds PO unit cost ({poUnitCost:0.####}) for purchaseOrderItemId={purchaseOrderItemId.Value}.";
        }

        var calculatedLineTotal = Math.Round(quantity.Value * unitCost.Value, 2, MidpointRounding.AwayFromZero);
        if (lineTotal.HasValue && Math.Abs(lineTotal.Value - calculatedLineTotal) > 0.01m)
        {
            return $"lineTotal mismatch for purchaseOrderItemId={purchaseOrderItemId.Value}. Expected {calculatedLineTotal:0.##}.";
        }

        return null;
    }

    private async Task<string?> ValidateSupplierPaymentAsync(JsonElement payload, long? branchId, long? existingId = null)
    {
        long? apId = TryGetLong(payload, "accountsPayableId", "accounts_payable_id");
        decimal? paidAmount = TryGetDecimal(payload, "paidAmount", "paid_amount");
        if (existingId.HasValue)
        {
            var existing = await _crud.GetByIdAsync("supplier-payments", existingId.Value, branchId);
            apId ??= AsLong(existing, "accountsPayableId");
            paidAmount ??= AsDecimal(existing, "paidAmount");
        }
        if (!apId.HasValue || apId.Value <= 0) return "accountsPayableId is required.";
        if (!paidAmount.HasValue || paidAmount.Value <= 0) return "paidAmount must be > 0.";

        var ap = (await _crud.QueryAsync(
            "SELECT TOP 1 outstanding_amount FROM dbo.accounts_payable WHERE id=@id AND (@branchId IS NULL OR branch_id=@branchId);",
            [new SqlParameter("@id", apId.Value), new SqlParameter("@branchId", (object?)branchId ?? DBNull.Value)])).FirstOrDefault();
        if (ap is null) return $"accounts_payable #{apId.Value} not found.";
        if (paidAmount.Value > AsDecimal(ap, "outstandingAmount") + 0.005m) return "paidAmount cannot exceed outstandingAmount.";
        return null;
    }

    private async Task<decimal?> ResolveStaffBaseSalaryAsync(long staffId, long? branchId, DateOnly asOfDate)
    {
        var asOf = asOfDate.ToDateTime(TimeOnly.MinValue);

        if (_crud.TryGetConfig("staff-salary-profiles", out var salaryCfg) && await _crud.TableExistsAsync(salaryCfg))
        {
            var salaryRow = (await _crud.QueryAsync(
                """
                SELECT TOP 1 base_salary
                FROM dbo.staff_salary_profiles
                WHERE staff_id=@staffId
                  AND (@branchId IS NULL OR branch_id=@branchId OR branch_id IS NULL)
                  AND effective_from <= @asOf
                  AND (effective_to IS NULL OR effective_to >= @asOf)
                  AND UPPER(ISNULL(status,@staffSalaryProfileStatusActive))=@staffSalaryProfileStatusActive
                ORDER BY effective_from DESC, id DESC;
                """,
                [
                    new SqlParameter("@staffId", staffId),
                    new SqlParameter("@branchId", (object?)branchId ?? DBNull.Value),
                    new SqlParameter("@asOf", asOf),
                    new SqlParameter("@staffSalaryProfileStatusActive", StaffSalaryProfileStatusActive)
                ])).FirstOrDefault();
            var baseSalary = AsDecimal(salaryRow, "baseSalary");
            if (baseSalary > 0m) return baseSalary;
        }

        if (_crud.TryGetConfig("staff-contracts", out var contractCfg) && await _crud.TableExistsAsync(contractCfg))
        {
            var contractRow = (await _crud.QueryAsync(
                """
                SELECT TOP 1 base_salary
                FROM dbo.staff_contracts
                WHERE staff_id=@staffId
                  AND effective_from <= @asOf
                  AND (effective_to IS NULL OR effective_to >= @asOf)
                  AND UPPER(ISNULL(status,@staffContractStatusActive))=@staffContractStatusActive
                ORDER BY effective_from DESC, id DESC;
                """,
                [
                    new SqlParameter("@staffId", staffId),
                    new SqlParameter("@asOf", asOf),
                    new SqlParameter("@staffContractStatusActive", StaffContractStatusActive)
                ])).FirstOrDefault();
            var baseSalary = AsDecimal(contractRow, "baseSalary");
            if (baseSalary > 0m) return baseSalary;
        }

        return null;
    }

    private async Task<DateOnly?> ResolveStaffHireDateAsync(long staffId, DateOnly? currentHireDate)
    {
        if (currentHireDate.HasValue) return currentHireDate.Value;

        var staffRow = (await _crud.QueryAsync(
            """
            IF OBJECT_ID('dbo.Staffs', 'U') IS NULL
            BEGIN
                SELECT CAST(NULL AS date) AS hire_date;
            END
            ELSE IF COL_LENGTH('dbo.Staffs', 'HireDate') IS NOT NULL
            BEGIN
                SELECT TOP 1 CAST(HireDate AS date) AS hire_date
                FROM dbo.Staffs
                WHERE Id=@staffId;
            END
            ELSE IF COL_LENGTH('dbo.Staffs', 'hire_date') IS NOT NULL
            BEGIN
                SELECT TOP 1 CAST(hire_date AS date) AS hire_date
                FROM dbo.Staffs
                WHERE Id=@staffId;
            END
            ELSE IF COL_LENGTH('dbo.Staffs', 'CreatedAt') IS NOT NULL
            BEGIN
                SELECT TOP 1 CAST(CreatedAt AS date) AS hire_date
                FROM dbo.Staffs
                WHERE Id=@staffId;
            END
            ELSE IF COL_LENGTH('dbo.Staffs', 'created_at') IS NOT NULL
            BEGIN
                SELECT TOP 1 CAST(created_at AS date) AS hire_date
                FROM dbo.Staffs
                WHERE Id=@staffId;
            END
            ELSE
            BEGIN
                SELECT CAST(NULL AS date) AS hire_date;
            END
            """,
            [new SqlParameter("@staffId", staffId)])).FirstOrDefault();
        var hireDate = AsDateOnly(staffRow, "hireDate");
        if (hireDate.HasValue) return hireDate;

        if (_crud.TryGetConfig("staff-contracts", out var contractCfg) && await _crud.TableExistsAsync(contractCfg))
        {
            var contractRow = (await _crud.QueryAsync(
                """
                SELECT TOP 1 effective_from AS hire_date
                FROM dbo.staff_contracts
                WHERE staff_id=@staffId
                  AND effective_from IS NOT NULL
                ORDER BY effective_from ASC, id ASC;
                """,
                [new SqlParameter("@staffId", staffId)])).FirstOrDefault();
            hireDate = AsDateOnly(contractRow, "hireDate");
            if (hireDate.HasValue) return hireDate;
        }

        if (_crud.TryGetConfig("staff-salary-profiles", out var salaryCfg) && await _crud.TableExistsAsync(salaryCfg))
        {
            var salaryRow = (await _crud.QueryAsync(
                """
                SELECT TOP 1 effective_from AS hire_date
                FROM dbo.staff_salary_profiles
                WHERE staff_id=@staffId
                  AND effective_from IS NOT NULL
                ORDER BY effective_from ASC, id ASC;
                """,
                [new SqlParameter("@staffId", staffId)])).FirstOrDefault();
            hireDate = AsDateOnly(salaryRow, "hireDate");
            if (hireDate.HasValue) return hireDate;
        }

        return null;
    }

    private async Task<decimal> ResolveStaffSalaryCoefficientAsync(long staffId, DateOnly hireDate, DateOnly asOfDate)
    {
        _ = staffId;
        var yearsOfService = CalculateCompletedYears(hireDate, asOfDate);

        var configuredCoefficient = await _db.SalaryCoefficients
            .AsNoTracking()
            .Where(item =>
                item.IsDeleted != true &&
                item.IsActive != false &&
                item.YearsOfService.HasValue &&
                item.YearsOfService.Value <= yearsOfService)
            .OrderByDescending(item => item.YearsOfService)
            .Select(item => (decimal?)item.CoefficientValue)
            .FirstOrDefaultAsync();

        if (configuredCoefficient.HasValue && configuredCoefficient.Value > 0m)
        {
            return configuredCoefficient.Value;
        }

        return 1.0000m;
    }

    private static int CalculateCompletedYears(DateOnly fromDate, DateOnly toDate)
    {
        if (toDate < fromDate) return 0;
        var years = toDate.Year - fromDate.Year;
        if (toDate < fromDate.AddYears(years)) years--;
        return Math.Max(0, years);
    }

    private bool TryGetTableAccess(string tableKey, out AccountingAccess access, out IActionResult deniedResult)
    {
        if (IsFinanceApprovalTableKey(tableKey))
        {
            return TryGetFinanceApprovalAccess(out access, out deniedResult);
        }

        return TryGetAccountingAccess(out access, out deniedResult);
    }

    private static bool IsFinanceApprovalTableKey(string tableKey)
    {
        return tableKey.Equals("finance-approval-requests", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRestrictedGenericWriteTable(string tableKey)
    {
        return RestrictedGenericWriteTables.Contains(tableKey);
    }

    private bool TryGetFinanceApprovalAccess(out AccountingAccess access, out IActionResult deniedResult)
    {
        access = default!;
        deniedResult = null!;
        if (!(User.Identity?.IsAuthenticated ?? false))
        {
            deniedResult = Unauthorized(new { message = "Unauthorized." });
            return false;
        }

        var claimRole = this.GetCurrentRole();
        var roleCode = this.GetCurrentRoleCode();
        var roleName = this.GetCurrentRoleName();
        if (!IsAccountingStaff(claimRole, roleCode, roleName) && !IsAdminStaff(claimRole, roleCode, roleName))
        {
            deniedResult = Forbid();
            return false;
        }

        var userId = this.GetCurrentUserId();
        if (!userId.HasValue || userId.Value <= 0)
        {
            deniedResult = Unauthorized(new { message = "Invalid user identity." });
            return false;
        }

        access = new AccountingAccess(userId.Value, ResolveBranchIdFromClaims());
        return true;
    }

    private bool TryGetAccountingAccess(out AccountingAccess access, out IActionResult deniedResult)
    {
        access = default!;
        deniedResult = null!;
        if (!(User.Identity?.IsAuthenticated ?? false))
        {
            deniedResult = Unauthorized(new { message = "Unauthorized." });
            return false;
        }

        var claimRole = this.GetCurrentRole();
        var roleCode = this.GetCurrentRoleCode();
        var roleName = this.GetCurrentRoleName();
        if (!IsAccountingStaff(claimRole, roleCode, roleName))
        {
            deniedResult = Forbid();
            return false;
        }

        var userId = this.GetCurrentUserId();
        if (!userId.HasValue || userId.Value <= 0)
        {
            deniedResult = Unauthorized(new { message = "Invalid user identity." });
            return false;
        }

        var branchId = ResolveBranchIdFromClaims();
        if (!branchId.HasValue)
        {
            deniedResult = Forbid();
            return false;
        }

        access = new AccountingAccess(userId.Value, branchId);
        return true;
    }

    private static bool IsAccountingStaff(string? claimRole, string? roleCode, string? roleName)
    {
        var normalizedRoleName = NormalizeRoleNameForMatch(roleName);
        return string.Equals(claimRole, "Staff", StringComparison.OrdinalIgnoreCase) &&
               (
                   string.Equals(roleCode, "ACCOUNTANT", StringComparison.OrdinalIgnoreCase) ||
                   normalizedRoleName.Contains("ACCOUNTANT", StringComparison.Ordinal) ||
                   normalizedRoleName.Contains("KE TOAN", StringComparison.Ordinal)
               );
    }

    private static string NormalizeRoleNameForMatch(string? roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return string.Empty;
        }

        var normalized = TryFixUtf8Mojibake(roleName.Trim());
        var decomposed = normalized.Normalize(NormalizationForm.FormD);
        var buffer = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                buffer.Append(c);
            }
        }

        return buffer.ToString()
            .Replace('Đ', 'D')
            .Replace('đ', 'd')
            .ToUpperInvariant();
    }

    private static string TryFixUtf8Mojibake(string value)
    {
        if (value.IndexOfAny(['Ã', 'Â', 'Ð', 'Æ']) < 0)
        {
            return value;
        }

        var decoded = Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(value));
        return decoded.Contains('\uFFFD') ? value : decoded;
    }

    private static bool IsAdminStaff(string? claimRole, string? roleCode, string? roleName)
    {
        var roleNameUpper = (roleName ?? "").Trim().ToUpperInvariant();
        return string.Equals(claimRole, "Staff", StringComparison.OrdinalIgnoreCase) &&
               (
                   string.Equals(roleCode, "ADMIN", StringComparison.OrdinalIgnoreCase) ||
                   roleNameUpper.Contains("ADMIN", StringComparison.OrdinalIgnoreCase)
               );
    }

    private long? ResolveBranchIdFromClaims()
    {
        return this.GetCurrentBranchId();
    }

    private static long? TryGetLong(JsonElement payload, params string[] keys)
    {
        if (payload.ValueKind != JsonValueKind.Object) return null;
        foreach (var prop in payload.EnumerateObject())
            foreach (var key in keys)
                if (prop.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt64(out var n)) return n;
                    if (prop.Value.ValueKind == JsonValueKind.String && long.TryParse(prop.Value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out n)) return n;
                }
        return null;
    }

    private static decimal? TryGetDecimal(JsonElement payload, params string[] keys)
    {
        if (payload.ValueKind != JsonValueKind.Object) return null;
        foreach (var prop in payload.EnumerateObject())
            foreach (var key in keys)
                if (prop.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDecimal(out var n)) return n;
                    if (prop.Value.ValueKind == JsonValueKind.String && decimal.TryParse(prop.Value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out n)) return n;
                }
        return null;
    }

    private static string AsString(IDictionary<string, object?>? row, string key) => row != null && row.TryGetValue(key, out var v) && v != null ? Convert.ToString(v) ?? "" : "";
    private static long? AsLong(IDictionary<string, object?>? row, string key) => row != null && row.TryGetValue(key, out var v) && v != null && v != DBNull.Value ? Convert.ToInt64(v) : null;
    private static decimal AsDecimal(IDictionary<string, object?>? row, string key) => row != null && row.TryGetValue(key, out var v) && v != null && v != DBNull.Value ? Convert.ToDecimal(v) : 0m;
    private static int AsInt(IDictionary<string, object?>? row, string key) => row != null && row.TryGetValue(key, out var v) && v != null && v != DBNull.Value ? Convert.ToInt32(v) : 0;
    private static DateOnly? AsDateOnly(IDictionary<string, object?>? row, string key)
    {
        if (row == null || !row.TryGetValue(key, out var v) || v == null || v == DBNull.Value) return null;
        if (v is DateOnly dateOnly) return dateOnly;
        if (v is DateTime dateTime) return DateOnly.FromDateTime(dateTime);
        if (v is DateTimeOffset dateTimeOffset) return DateOnly.FromDateTime(dateTimeOffset.DateTime);

        var text = Convert.ToString(v, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDateOnly)) return parsedDateOnly;
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDateTime)) return DateOnly.FromDateTime(parsedDateTime);
        return null;
    }

    private sealed record PayrollRunResolution(long RunId, long RunNo, bool CreatedNew);
    private sealed record SalaryCoefficientThreshold(int YearsOfService, decimal CoefficientValue);
    private sealed record ContractCompensationSnapshot(decimal BaseSalary, decimal AllowanceAmount);
    private sealed record PitBracket(decimal UpperLimit, decimal Rate);
    private sealed record VietnamPayrollPolicy(
        decimal EmployeeBhxhRate,
        decimal EmployeeBhytRate,
        decimal EmployeeBhtnRate,
        decimal EmployerBhxhRate,
        decimal EmployerBhytRate,
        decimal EmployerBhtnRate,
        decimal EmployerOaidRate,
        decimal PersonalDeduction,
        decimal DependentDeduction,
        decimal SocialInsuranceBaseReference,
        IReadOnlyDictionary<string, decimal> RegionalMinimumWageByZone);

    private sealed record VietnamStatutoryAmounts(decimal EmployeeInsuranceTotal, decimal EmployerInsuranceTotal, decimal PitAmount);

    private sealed class StaffPayrollSnapshot
    {
        public long StaffId { get; init; }
        public long? BranchId { get; init; }
        public string StaffName { get; init; } = "";
        public DateOnly? HireDate { get; init; }
        public DateOnly? TerminationDate { get; init; }
        public string? Status { get; init; }
    }

    private sealed class PayrollCommissionReconciliationDetail
    {
        public long StaffId { get; init; }
        public decimal PayrollCommissionAmount { get; init; }
        public decimal ProcessedAmount { get; init; }
        public decimal PendingAmount { get; init; }
        public decimal PaidAmount { get; init; }
        public decimal CanceledAmount { get; init; }
        public int ProcessedCount { get; init; }
        public int PendingCount { get; init; }
        public int PaidCount { get; init; }
        public int CanceledCount { get; init; }
        public decimal GapAmount { get; init; }
        public bool IsMatched { get; init; }
    }

    private sealed class PayrollRunCommissionReconciliationRunInfo
    {
        public long Id { get; init; }
        public int RunNo { get; init; }
        public string Status { get; init; } = string.Empty;
        public long? BranchId { get; init; }
        public long PayrollPeriodId { get; init; }
        public string PeriodCode { get; init; } = string.Empty;
        public string PeriodStartDate { get; init; } = string.Empty;
        public string PeriodEndDate { get; init; } = string.Empty;
    }

    private sealed class PayrollRunCommissionReconciliationSummary
    {
        public int StaffCount { get; init; }
        public decimal TotalPayrollCommission { get; init; }
        public decimal TotalProcessed { get; init; }
        public decimal TotalPending { get; init; }
        public decimal TotalPaid { get; init; }
        public decimal TotalCanceled { get; init; }
        public decimal TotalGap { get; init; }
        public bool IsFullyMatched { get; init; }
    }

    private sealed class PayrollRunCommissionReconciliationResponse
    {
        public PayrollRunCommissionReconciliationRunInfo PayrollRun { get; init; } = new();
        public PayrollRunCommissionReconciliationSummary Summary { get; init; } = new();
        public List<PayrollCommissionReconciliationDetail> Details { get; init; } = [];
    }

    private sealed record AccountingAccess(long UserId, long? BranchId);
    public sealed class PostJournalRequest { public long PostedBy { get; set; } }
    public sealed class FinanceDecisionRequest { public string? Decision { get; set; } public long ApproverId { get; set; } public string? RejectReason { get; set; } }
    public sealed class PayrollApproveRequest { public long ApprovedBy { get; set; } }
    public sealed class PayrollPaidRequest { public long PaidBy { get; set; } }
    public sealed class SupplierPaymentVoidRequest { public long? ApprovalRequestId { get; set; } public long? ApprovedBy { get; set; } public string? Reason { get; set; } }
    public sealed class PayrollBatchCalculateRequest
    {
        public long? PayrollPeriodId { get; set; }
        public long? PayrollRunId { get; set; }
        public bool OverwriteExisting { get; set; } = true;
        public bool IncludeInactiveStaff { get; set; }
        public bool IssuePayslips { get; set; }
    }
    public sealed class PayrollRunCalculateRequest
    {
        public bool OverwriteExisting { get; set; } = true;
        public bool IncludeInactiveStaff { get; set; }
        public bool IssuePayslips { get; set; }
    }
    public sealed class CreatePayslipRequest
    {
        public long PayrollItemId { get; set; }
        public string? SlipNo { get; set; }
        public string? IssuedAt { get; set; }
        public long? IssuedBy { get; set; }
        public string? FileUrl { get; set; }
    }
    public sealed class CreatePayrollPeriodRequest
    {
        public string? PeriodCode { get; set; }
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public string? PayrollDate { get; set; }
        public string? Status { get; set; }
    }
    public sealed class InitialSalaryRequestDto
    {
        public decimal? BaseSalary { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public string? Reason { get; set; }
        public string? Note { get; set; }
    }
}


