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
    private const string PayrollRunStatusCanceled = nameof(PayrollRunStatusCode.CANCELED);
    private const string PayrollItemStatusApproved = nameof(PayrollItemStatusCode.APPROVED);
    private const string PayrollItemStatusPaid = nameof(PayrollItemStatusCode.PAID);
    private const string PayrollPeriodStatusOpen = nameof(PayrollPeriodStatusCode.OPEN);
    private const string PayrollPeriodStatusApproved = nameof(PayrollPeriodStatusCode.APPROVED);
    private const string PayrollPeriodStatusPaid = nameof(PayrollPeriodStatusCode.PAID);
    private const string PayrollPeriodStatusLocked = nameof(PayrollPeriodStatusCode.LOCKED);
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
        if (!AsString(row, "status").Equals(PayrollRunStatusDraft, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only DRAFT payroll run can be approved." });

        var lineCount = await _crud.ScalarAsync<int?>(
            "SELECT COUNT(*) FROM dbo.payroll_items WHERE payroll_run_id=@id;",
            [new SqlParameter("@id", id)]);
        if (!lineCount.HasValue || lineCount.Value <= 0)
            return BadRequest(new { message = "Cannot approve payroll run without payroll items." });

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

    [HttpPost("payroll-runs/{id:long}/cancel")]
    public async Task<IActionResult> CancelPayrollRun(long id, [FromBody] PayrollCancelRequest request)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        var row = await _crud.GetByIdAsync("payroll-runs", id, access.BranchId);
        if (row is null) return NotFound();

        var payrollPeriodId = AsLong(row, "payrollPeriodId");
        var status = AsString(row, "status").ToUpperInvariant();
        if (status is not (PayrollRunStatusDraft or PayrollRunStatusCalculated))
            return BadRequest(new { message = "Only DRAFT/CALCULATED payroll run can be canceled." });

        await _crud.ExecuteAsync(
            "UPDATE dbo.payroll_runs SET status=@status, updated_at=SYSDATETIME() WHERE id=@id;",
            [
                new SqlParameter("@id", id),
                new SqlParameter("@status", PayrollRunStatusCanceled)
            ]);
        if (payrollPeriodId.HasValue && payrollPeriodId.Value > 0)
            await SyncPayrollPeriodStatusAsync(payrollPeriodId.Value);
        return Ok(await _crud.GetByIdAsync("payroll-runs", id, access.BranchId));
    }

    [HttpPost("payroll-runs/calculate-all")]
    public async Task<IActionResult> CalculatePayrollForAll([FromBody] PayrollBatchCalculateRequest? request)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        request ??= new PayrollBatchCalculateRequest();
        var effectiveBranchId = access.BranchId ?? request.BranchId;
        if (!effectiveBranchId.HasValue || effectiveBranchId.Value <= 0)
        {
            return BadRequest(new { message = "branchId is required for payroll batch calculation." });
        }

        if (access.BranchId.HasValue && request.BranchId.HasValue && request.BranchId.Value != access.BranchId.Value)
        {
            return Forbid();
        }
        try
        {
            var result = await _payrollBatchCalculationService.CalculatePayrollForAllAsync(
                new PayrollBatchCalculationCommand
                {
                    UserId = access.UserId,
                    BranchId = effectiveBranchId,
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
            var existingPeriod = (await _crud.QueryAsync(
                """
                SELECT TOP 1
                    id,
                    period_code,
                    start_date,
                    end_date,
                    payroll_date,
                    status
                FROM dbo.payroll_periods
                WHERE UPPER(period_code)=UPPER(@periodCode)
                ORDER BY id DESC;
                """,
                [new SqlParameter("@periodCode", periodCode)])).FirstOrDefault();

            if (existingPeriod is not null)
            {
                var existingId = AsLong(existingPeriod, "id");
                var existingStartDate = AsDateOnly(existingPeriod, "startDate");
                var existingEndDate = AsDateOnly(existingPeriod, "endDate");
                var existingPayrollDate = AsDateOnly(existingPeriod, "payrollDate");
                var dateMismatch =
                    (existingStartDate.HasValue && existingStartDate.Value != startDate) ||
                    (existingEndDate.HasValue && existingEndDate.Value != endDate) ||
                    (existingPayrollDate.HasValue && existingPayrollDate.Value != payrollDate);

                if (dateMismatch)
                {
                    return Conflict(new
                    {
                        message = $"Payroll period code '{periodCode}' already exists with a different date range."
                    });
                }

                if (existingId.HasValue)
                {
                    var reused = await _crud.GetByIdAsync("payroll-periods", existingId.Value, access.BranchId);
                    return reused is null ? Ok(existingPeriod) : Ok(reused);
                }
            }

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
            var existingPeriod = (await _crud.QueryAsync(
                """
                SELECT TOP 1 id, period_code, start_date, end_date, payroll_date, status
                FROM dbo.payroll_periods
                WHERE UPPER(period_code)=UPPER(@periodCode)
                ORDER BY id DESC;
                """,
                [new SqlParameter("@periodCode", periodCode)])).FirstOrDefault();
            if (existingPeriod is not null)
            {
                var existingId = AsLong(existingPeriod, "id");
                if (existingId.HasValue)
                {
                    var reused = await _crud.GetByIdAsync("payroll-periods", existingId.Value, access.BranchId);
                    return reused is null ? Ok(existingPeriod) : Ok(reused);
                }
            }

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

    [HttpGet("staff-salary/{staffId:long}/base")]
    public async Task<IActionResult> StaffBaseSalary(
        long staffId,
        [FromQuery] DateTime? asOfDate = null,
        [FromQuery] bool includeTierFallback = true)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;

        var staff = await _db.Staffs
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == staffId && s.IsDeleted != true);
        if (staff is null) return NotFound(new { message = $"Staff #{staffId} not found." });
        if (access.BranchId.HasValue && staff.BranchId != access.BranchId.Value) return NotFound();

        var asOf = DateOnly.FromDateTime((asOfDate ?? VietnamTime.Now).Date);
        var baseSalary = await ResolveStaffBaseSalaryAsync(staff.Id, access.BranchId, asOf, includeTierFallback);
        var hasBaseSalary = baseSalary.HasValue && baseSalary.Value > 0m;

        return Ok(new
        {
            staffId = staff.Id,
            staffName = staff.FullName,
            branchId = staff.BranchId,
            asOfDate = asOf.ToString("yyyy-MM-dd"),
            includeTierFallback,
            hasBaseSalary,
            baseSalary = hasBaseSalary ? baseSalary!.Value : 0m
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
            ? $"Y�u c?u t?o luong kh?i di?m cho nh�n s? {staff.FullName} (#{staff.Id})."
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
            message = "Da gui duyet thanh cong. Yeu cau da chuyen den Admin de phe duyet."
        });
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        var from = fromDate ?? VietnamTime.Now.Date.AddDays(-30);
        var to = toDate ?? VietnamTime.Now.Date.AddDays(1).AddTicks(-1);
        var payload = await BuildAccountingDashboardV2PayloadAsync(from, to, access, historyLimit: 50);
        var summary = payload.TryGetValue("summary", out var summaryRaw) && summaryRaw is IDictionary<string, object?> summaryDict
            ? summaryDict
            : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        return Ok(new
        {
            fromDate = from,
            toDate = to,
            revenue = AsDecimal(summary, "revenue"),
            netProfit = AsDecimal(summary, "netProfit"),
            cashFlow = AsDecimal(summary, "cashFlow"),
            collected = AsDecimal(summary, "collected"),
            refund = AsDecimal(summary, "refund"),
            reversal = AsDecimal(summary, "reversal"),
            source = "accounting-dashboard-v2",
            lockPeriodBadge = payload.GetValueOrDefault("lockPeriodBadge"),
            agingBuckets = payload.GetValueOrDefault("agingBuckets")
        });
    }

    [HttpGet("dashboard-v2")]
    public async Task<IActionResult> DashboardV2([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null, [FromQuery] int historyLimit = 200)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        var from = fromDate ?? VietnamTime.Now.Date.AddDays(-30);
        var to = toDate ?? VietnamTime.Now.Date.AddDays(1).AddTicks(-1);
        var payload = await BuildAccountingDashboardV2PayloadAsync(from, to, access, historyLimit);
        return Ok(payload);
    }

    [HttpGet("dashboard-v2/export")]
    public async Task<IActionResult> ExportDashboardV2Csv([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null, [FromQuery] int historyLimit = 200)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        var from = fromDate ?? VietnamTime.Now.Date.AddDays(-30);
        var to = toDate ?? VietnamTime.Now.Date.AddDays(1).AddTicks(-1);
        var payload = await BuildAccountingDashboardV2PayloadAsync(from, to, access, historyLimit);

        var csv = BuildDashboardV2Csv(payload);
        var fileName = $"accounting-dashboard-v2-{VietnamTime.Now:yyyyMMdd-HHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileName);
    }

    private async Task<Dictionary<string, object?>> BuildAccountingDashboardV2PayloadAsync(DateTime from, DateTime to, AccountingAccess access, int historyLimit)
    {
        var safeLimit = Math.Clamp(historyLimit, 20, 1000);
        var now = VietnamTime.Now;
        var today = now.Date;

        var revenue = (await _crud.QueryAsync(
            """
            SELECT
                ISNULL(SUM(TotalRevenue),0) AS revenue,
                ISNULL(SUM(NetProfit),0) AS net_profit,
                ISNULL(SUM(NetCashFlow),0) AS cash_flow
            FROM dbo.RevenueSnapshots
            WHERE SnapshotDate BETWEEN @f AND @t
              AND (@branchId IS NULL OR BranchId=@branchId);
            """,
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

        var arRows = await _crud.QueryAsync(
            """
            SELECT
                i.Id,
                CAST(CONCAT('INV-', i.Id) AS nvarchar(64)) AS InvoiceCode,
                i.IssuedAt AS InvoiceDate,
                CAST(NULL AS datetime2) AS DueDate,
                i.PaymentStatus,
                i.TotalAmount,
                i.PaidAmount,
                ISNULL(i.TotalAmount,0) - ISNULL(i.PaidAmount,0) AS OutstandingAmount,
                i.CompanyName,
                i.ReceiveEmail,
                i.ServiceOrderId,
                i.RetailOrderId,
                i.CreatedAt,
                i.UpdatedAt
            FROM dbo.Invoices i
            LEFT JOIN dbo.ServiceOrders so ON so.Id = i.ServiceOrderId
            LEFT JOIN dbo.RetailOrders ro ON ro.Id = i.RetailOrderId
            WHERE (
                    @branchId IS NULL
                    OR so.ExecuteBranchId = @branchId
                    OR so.OriginBranchId = @branchId
                    OR ro.BranchId = @branchId
                  )
            ORDER BY i.Id DESC;
            """,
            [new SqlParameter("@branchId", (object?)access.BranchId ?? DBNull.Value)]);

        var apRows = new List<Dictionary<string, object?>>();
        if (_crud.TryGetConfig("accounts-payable", out var apCfg) && await _crud.TableExistsAsync(apCfg))
        {
            apRows = (await _crud.QueryAsync(
                """
                SELECT
                    id,
                    supplier_id,
                    branch_id,
                    supplier_invoice_no,
                    due_date,
                    original_amount,
                    paid_amount,
                    outstanding_amount,
                    status,
                    note,
                    created_at,
                    updated_at
                FROM dbo.accounts_payable
                WHERE (@branchId IS NULL OR branch_id=@branchId OR branch_id IS NULL)
                ORDER BY id DESC;
                """,
                [new SqlParameter("@branchId", (object?)access.BranchId ?? DBNull.Value)])).ToList();
        }

        var paymentHistory = (await _crud.QueryAsync(
            """
            SELECT TOP (@limit)
                p.Id,
                p.InvoiceId,
                p.ServiceOrderId,
                p.RetailOrderId,
                p.MethodId AS PaymentMethodId,
                p.Amount,
                p.Type,
                p.Direction,
                p.Status,
                p.TransactionCode,
                CAST(NULL AS nvarchar(100)) AS ReferenceCode,
                p.CreatedAt,
                p.UpdatedAt,
                CAST(CONCAT('INV-', i.Id) AS nvarchar(64)) AS InvoiceCode,
                CAST(NULL AS datetime2) AS DueDate
            FROM dbo.Payments p
            LEFT JOIN dbo.Invoices i ON i.Id = p.InvoiceId
            LEFT JOIN dbo.ServiceOrders so ON so.Id = COALESCE(p.ServiceOrderId, i.ServiceOrderId)
            LEFT JOIN dbo.RetailOrders ro ON ro.Id = COALESCE(p.RetailOrderId, i.RetailOrderId)
            WHERE (
                    @branchId IS NULL
                    OR so.ExecuteBranchId = @branchId
                    OR so.OriginBranchId = @branchId
                    OR ro.BranchId = @branchId
                  )
            ORDER BY p.CreatedAt DESC, p.Id DESC;
            """,
            [
                new SqlParameter("@limit", safeLimit),
                new SqlParameter("@branchId", (object?)access.BranchId ?? DBNull.Value)
            ])).ToList();

        var refundTrail = paymentHistory
            .Where(x =>
            {
                var type = AsString(x, "type").Trim().ToUpperInvariant();
                return type is "REFUND" or "REVERSAL";
            })
            .Take(50)
            .ToList();

        var approvalTrail = new List<Dictionary<string, object?>>();
        if (_crud.TryGetConfig("finance-approval-requests", out var approvalCfg) && await _crud.TableExistsAsync(approvalCfg))
        {
            approvalTrail = (await _crud.QueryAsync(
                """
                SELECT TOP (@limit)
                    id,
                    request_type,
                    status,
                    risk_level,
                    amount,
                    reason,
                    reject_reason,
                    requested_by,
                    approver_id,
                    request_payload,
                    created_at,
                    updated_at
                FROM dbo.finance_approval_requests
                WHERE (
                        @branchId IS NULL
                        OR TRY_CONVERT(bigint, JSON_VALUE(request_payload, '$.branchId')) = @branchId
                        OR JSON_VALUE(request_payload, '$.branchId') IS NULL
                      )
                ORDER BY updated_at DESC, id DESC;
                """,
                [
                    new SqlParameter("@limit", safeLimit),
                    new SqlParameter("@branchId", (object?)access.BranchId ?? DBNull.Value)
                ])).ToList();
        }

        var lockPeriodBadge = await BuildLockPeriodBadgeAsync(access.BranchId);
        var arOpenRows = BuildReceivableOpenRows(arRows, today);
        var apOpenRows = BuildPayableOpenRows(apRows, today);

        var agingBuckets = BuildAgingBuckets(arOpenRows);
        var invoiceWatchlist = arOpenRows
            .OrderByDescending(x => x.IsOverdue)
            .ThenByDescending(x => x.DaysPastDue)
            .ThenByDescending(x => x.OutstandingAmount)
            .Take(10)
            .Select(x => new Dictionary<string, object?>
            {
                ["invoiceId"] = x.InvoiceId,
                ["invoiceCode"] = x.InvoiceCode,
                ["dueDate"] = x.DueDate,
                ["daysPastDue"] = x.DaysPastDue,
                ["outstandingAmount"] = x.OutstandingAmount,
                ["isOverdue"] = x.IsOverdue,
                ["companyName"] = x.CompanyName
            })
            .ToList();

        var apWatchlist = apOpenRows
            .OrderByDescending(x => x.IsOverdue)
            .ThenByDescending(x => x.DaysPastDue)
            .ThenByDescending(x => x.OutstandingAmount)
            .Take(10)
            .Select(x => new Dictionary<string, object?>
            {
                ["id"] = x.Id,
                ["dueDate"] = x.DueDate,
                ["daysPastDue"] = x.DaysPastDue,
                ["outstandingAmount"] = x.OutstandingAmount,
                ["isOverdue"] = x.IsOverdue,
                ["supplierInvoiceNo"] = x.SupplierInvoiceNo
            })
            .ToList();

        var pendingApprovalCount = approvalTrail.Count(x => AsString(x, "status").Trim().ToUpperInvariant() == FinanceApprovalStatusPending);
        var summary = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["revenue"] = AsDecimal(revenue, "revenue"),
            ["netProfit"] = AsDecimal(revenue, "netProfit"),
            ["cashFlow"] = AsDecimal(revenue, "cashFlow"),
            ["collected"] = AsDecimal(payments, "collected"),
            ["refund"] = AsDecimal(payments, "refund"),
            ["reversal"] = AsDecimal(payments, "reversal"),
            ["totalOutstandingAr"] = arOpenRows.Sum(x => x.OutstandingAmount),
            ["totalOutstandingAp"] = apOpenRows.Sum(x => x.OutstandingAmount),
            ["overdueArCount"] = arOpenRows.Count(x => x.IsOverdue),
            ["overdueApAmount"] = apOpenRows.Where(x => x.IsOverdue).Sum(x => x.OutstandingAmount),
            ["pendingApprovalCount"] = pendingApprovalCount
        };

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["source"] = "accounting-dashboard-v2",
            ["generatedAt"] = now,
            ["fromDate"] = from,
            ["toDate"] = to,
            ["branchId"] = access.BranchId,
            ["summary"] = summary,
            ["agingBuckets"] = agingBuckets,
            ["invoiceWatchlist"] = invoiceWatchlist,
            ["apWatchlist"] = apWatchlist,
            ["paymentHistory"] = paymentHistory,
            ["refundTrail"] = refundTrail,
            ["approvalTrail"] = approvalTrail,
            ["lockPeriodBadge"] = lockPeriodBadge
        };
    }

    private async Task<Dictionary<string, object?>> BuildLockPeriodBadgeAsync(long? branchId)
    {
        Dictionary<string, object?> accountingPeriod = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, object?> payrollPeriod = new(StringComparer.OrdinalIgnoreCase);

        if (_crud.TryGetConfig("accounting-periods", out var accountingPeriodCfg) && await _crud.TableExistsAsync(accountingPeriodCfg))
        {
            accountingPeriod = (await _crud.QueryAsync(
                """
                SELECT TOP 1
                    id, period_code, start_date, end_date, status, closed_at, note
                FROM dbo.accounting_periods
                WHERE (@branchId IS NULL OR branch_id=@branchId OR branch_id IS NULL)
                ORDER BY end_date DESC, id DESC;
                """,
                [new SqlParameter("@branchId", (object?)branchId ?? DBNull.Value)])).FirstOrDefault()
                ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        if (_crud.TryGetConfig("payroll-periods", out var payrollPeriodCfg) && await _crud.TableExistsAsync(payrollPeriodCfg))
        {
            payrollPeriod = (await _crud.QueryAsync(
                """
                SELECT TOP 1
                    id, period_code, start_date, end_date, status, payroll_date
                FROM dbo.payroll_periods
                ORDER BY end_date DESC, id DESC;
                """)).FirstOrDefault()
                ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        var accountingStatus = AsString(accountingPeriod, "status").Trim().ToUpperInvariant();
        var payrollStatus = AsString(payrollPeriod, "status").Trim().ToUpperInvariant();
        var isLocked = accountingStatus.Contains("LOCK", StringComparison.OrdinalIgnoreCase)
            || accountingStatus.Contains("CLOSE", StringComparison.OrdinalIgnoreCase)
            || payrollStatus.Contains("LOCK", StringComparison.OrdinalIgnoreCase);

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["isLocked"] = isLocked,
            ["accountingPeriod"] = accountingPeriod,
            ["payrollPeriod"] = payrollPeriod,
            ["badgeText"] = isLocked ? "LOCK PERIOD" : "OPEN PERIOD"
        };
    }

    private static bool IsInvoiceClosedStatus(string status)
    {
        var normalized = (status ?? "").Trim().ToUpperInvariant();
        return normalized is "PAID" or "CANCELLED" or "CANCELED" or "VOID" or "WRITTEN_OFF" or "CLOSED";
    }

    private static List<ReceivableOpenRow> BuildReceivableOpenRows(IEnumerable<Dictionary<string, object?>> rows, DateTime today)
    {
        var result = new List<ReceivableOpenRow>();
        foreach (var row in rows)
        {
            var status = (row.TryGetValue("paymentStatus", out var statusRaw) ? Convert.ToString(statusRaw) : null) ?? "";
            var outstanding = row.TryGetValue("outstandingAmount", out var outstandingRaw) && outstandingRaw is not null && outstandingRaw != DBNull.Value
                ? Math.Max(0m, Convert.ToDecimal(outstandingRaw))
                : 0m;
            if (outstanding <= 0m || IsInvoiceClosedStatus(status)) continue;

            DateTime? dueDate = null;
            if (row.TryGetValue("dueDate", out var dueDateRaw) && dueDateRaw is not null && dueDateRaw != DBNull.Value)
            {
                if (dueDateRaw is DateTime dt) dueDate = dt.Date;
                else if (DateTime.TryParse(Convert.ToString(dueDateRaw), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) dueDate = parsed.Date;
            }

            var daysPastDue = dueDate.HasValue ? (today.Date - dueDate.Value.Date).Days : 0;
            var isOverdue = dueDate.HasValue && daysPastDue > 0;

            result.Add(new ReceivableOpenRow(
                InvoiceId: row.TryGetValue("id", out var idRaw) && idRaw is not null && idRaw != DBNull.Value ? Convert.ToInt64(idRaw) : 0L,
                InvoiceCode: (row.TryGetValue("invoiceCode", out var invoiceCodeRaw) ? Convert.ToString(invoiceCodeRaw) : null) ?? "",
                DueDate: dueDate,
                OutstandingAmount: outstanding,
                DaysPastDue: daysPastDue,
                IsOverdue: isOverdue,
                CompanyName: (row.TryGetValue("companyName", out var companyNameRaw) ? Convert.ToString(companyNameRaw) : null) ?? ""
            ));
        }

        return result;
    }

    private static List<PayableOpenRow> BuildPayableOpenRows(IEnumerable<Dictionary<string, object?>> rows, DateTime today)
    {
        var result = new List<PayableOpenRow>();
        foreach (var row in rows)
        {
            var outstanding = row.TryGetValue("outstandingAmount", out var outstandingRaw) && outstandingRaw is not null && outstandingRaw != DBNull.Value
                ? Math.Max(0m, Convert.ToDecimal(outstandingRaw))
                : 0m;
            if (outstanding <= 0m) continue;

            DateTime? dueDate = null;
            if (row.TryGetValue("dueDate", out var dueDateRaw) && dueDateRaw is not null && dueDateRaw != DBNull.Value)
            {
                if (dueDateRaw is DateTime dt) dueDate = dt.Date;
                else if (DateTime.TryParse(Convert.ToString(dueDateRaw), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) dueDate = parsed.Date;
            }

            var daysPastDue = dueDate.HasValue ? (today.Date - dueDate.Value.Date).Days : 0;
            var isOverdue = dueDate.HasValue && daysPastDue > 0;

            result.Add(new PayableOpenRow(
                Id: row.TryGetValue("id", out var idRaw) && idRaw is not null && idRaw != DBNull.Value ? Convert.ToInt64(idRaw) : 0L,
                SupplierInvoiceNo: (row.TryGetValue("supplierInvoiceNo", out var noRaw) ? Convert.ToString(noRaw) : null) ?? "",
                DueDate: dueDate,
                OutstandingAmount: outstanding,
                DaysPastDue: daysPastDue,
                IsOverdue: isOverdue
            ));
        }

        return result;
    }

    private static List<Dictionary<string, object?>> BuildAgingBuckets(IEnumerable<ReceivableOpenRow> rows)
    {
        var buckets = new[]
        {
            new { Key = "CURRENT", Label = "Current / Not due", Min = int.MinValue, Max = 0 },
            new { Key = "D1_30", Label = "1-30 days", Min = 1, Max = 30 },
            new { Key = "D31_60", Label = "31-60 days", Min = 31, Max = 60 },
            new { Key = "D61_90", Label = "61-90 days", Min = 61, Max = 90 },
            new { Key = "D90_PLUS", Label = "90+ days", Min = 91, Max = int.MaxValue }
        };

        var results = new List<Dictionary<string, object?>>();
        foreach (var bucket in buckets)
        {
            var selected = rows.Where(x => x.DaysPastDue >= bucket.Min && x.DaysPastDue <= bucket.Max).ToList();
            results.Add(new Dictionary<string, object?>
            {
                ["bucket"] = bucket.Key,
                ["label"] = bucket.Label,
                ["count"] = selected.Count,
                ["amount"] = selected.Sum(x => x.OutstandingAmount)
            });
        }

        return results;
    }

    private static string BuildDashboardV2Csv(Dictionary<string, object?> payload)
    {
        var sb = new StringBuilder();
        sb.AppendLine("section,key,value");

        if (payload.TryGetValue("summary", out var summaryRaw) && summaryRaw is IDictionary<string, object?> summary)
        {
            foreach (var kv in summary)
            {
                sb.AppendLine($"summary,{CsvEscape(kv.Key)},{CsvEscape(kv.Value)}");
            }
        }

        if (payload.TryGetValue("agingBuckets", out var agingRaw) && agingRaw is IEnumerable<Dictionary<string, object?>> aging)
        {
            foreach (var row in aging)
            {
                sb.AppendLine($"aging,{CsvEscape(AsString(row, "bucket"))},{CsvEscape(row.GetValueOrDefault("amount"))}");
            }
        }

        if (payload.TryGetValue("lockPeriodBadge", out var lockRaw) && lockRaw is IDictionary<string, object?> lockBadge)
        {
            lockBadge.TryGetValue("badgeText", out var badgeText);
            lockBadge.TryGetValue("isLocked", out var isLocked);
            sb.AppendLine($"lockPeriod,badgeText,{CsvEscape(badgeText)}");
            sb.AppendLine($"lockPeriod,isLocked,{CsvEscape(isLocked)}");
        }

        if (payload.TryGetValue("paymentHistory", out var paymentRaw) && paymentRaw is IEnumerable<Dictionary<string, object?>> payments)
        {
            foreach (var row in payments.Take(50))
            {
                var key = $"payment#{AsString(row, "id")}";
                var value = $"{AsString(row, "type")} | {AsString(row, "status")} | {AsDecimal(row, "amount")}";
                sb.AppendLine($"paymentHistory,{CsvEscape(key)},{CsvEscape(value)}");
            }
        }

        if (payload.TryGetValue("refundTrail", out var refundRaw) && refundRaw is IEnumerable<Dictionary<string, object?>> refunds)
        {
            foreach (var row in refunds.Take(50))
            {
                var key = $"refund#{AsString(row, "id")}";
                var value = $"{AsString(row, "type")} | {AsString(row, "status")} | {AsDecimal(row, "amount")}";
                sb.AppendLine($"refundTrail,{CsvEscape(key)},{CsvEscape(value)}");
            }
        }

        if (payload.TryGetValue("approvalTrail", out var approvalRaw) && approvalRaw is IEnumerable<Dictionary<string, object?>> approvals)
        {
            foreach (var row in approvals.Take(50))
            {
                var key = $"approval#{AsString(row, "id")}";
                var value = $"{AsString(row, "requestType")} | {AsString(row, "status")} | {AsDecimal(row, "amount")}";
                sb.AppendLine($"approvalTrail,{CsvEscape(key)},{CsvEscape(value)}");
            }
        }

        return sb.ToString();
    }

    private static string CsvEscape(object? value)
    {
        var raw = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return $"\"{raw.Replace("\"", "\"\"")}\"";
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

    private async Task<decimal?> ResolveStaffBaseSalaryAsync(
        long staffId,
        long? branchId,
        DateOnly asOfDate,
        bool includeTierFallback = true)
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

        if (includeTierFallback)
        {
            // Keep this fallback consistent with PayrollBatchCalculationService.
            var tierBaseSalary = await (
                from staff in _db.Staffs.AsNoTracking()
                join tier in _db.StaffTiers.AsNoTracking() on staff.StaffTierId equals tier.Id
                where staff.Id == staffId
                      && staff.IsDeleted != true
                      && tier.IsDeleted != true
                      && tier.BaseSalary.HasValue
                      && tier.BaseSalary.Value > 0m
                select (decimal?)tier.BaseSalary!.Value
            ).FirstOrDefaultAsync();
            if (tierBaseSalary.HasValue && tierBaseSalary.Value > 0m) return tierBaseSalary.Value;
        }

        return null;
    }

    private async Task<DateOnly?> ResolveStaffHireDateAsync(long staffId, DateOnly? currentHireDate)
    {
        if (currentHireDate.HasValue) return currentHireDate.Value;

        var staffsTableExists = await _crud.ScalarAsync<int?>("SELECT OBJECT_ID('dbo.Staffs', 'U');");
        if (staffsTableExists.HasValue)
        {
            var columnRow = (await _crud.QueryAsync(
                """
                SELECT TOP 1 c.name AS column_name
                FROM sys.columns c
                WHERE c.object_id = OBJECT_ID('dbo.Staffs')
                  AND c.name IN ('HireDate', 'hire_date', 'CreatedAt', 'created_at')
                ORDER BY CASE c.name
                    WHEN 'HireDate' THEN 1
                    WHEN 'hire_date' THEN 2
                    WHEN 'CreatedAt' THEN 3
                    WHEN 'created_at' THEN 4
                    ELSE 99
                END;
                """)).FirstOrDefault();

            var columnName = AsString(columnRow, "columnName");
            if (!string.IsNullOrWhiteSpace(columnName))
            {
                var staffSql = $"""
                SELECT TOP 1 CAST([{columnName}] AS date) AS hire_date
                FROM dbo.Staffs
                WHERE Id=@staffId;
                """;
                var staffRow = (await _crud.QueryAsync(staffSql, [new SqlParameter("@staffId", staffId)])).FirstOrDefault();
                var directHireDate = AsDateOnly(staffRow, "hireDate");
                if (directHireDate.HasValue) return directHireDate.Value;
            }
        }

        var hireDate = (DateOnly?)null;

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
        try
        {
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
        }
        catch (Exception)
        {
            // Never break payroll UI because of coefficient source issues.
            return 1.0000m;
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

        var claimRole = User.FindFirstValue(ClaimTypes.Role);
        var roleCode = User.FindFirstValue("role_code");
        var roleName = User.FindFirstValue("role_name");
        var isAccountingStaff = IsAccountingStaff(claimRole, roleCode, roleName);
        var isAdminStaff = IsAdminStaff(claimRole, roleCode, roleName);
        if (!isAccountingStaff && !isAdminStaff)
        {
            deniedResult = Forbid();
            return false;
        }

        var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId) || userId <= 0)
        {
            deniedResult = Unauthorized(new { message = "Invalid user identity." });
            return false;
        }

        var branchId = ResolveBranchIdFromClaims();
        // ACCOUNTANT must always be scoped to exactly one branch.
        if (isAccountingStaff && !branchId.HasValue)
        {
            deniedResult = Forbid();
            return false;
        }

        access = new AccountingAccess(userId, branchId);
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

        var claimRole = User.FindFirstValue(ClaimTypes.Role);
        var roleCode = User.FindFirstValue("role_code");
        var roleName = User.FindFirstValue("role_name");
        if (!IsAccountingStaff(claimRole, roleCode, roleName))
        {
            deniedResult = Forbid();
            return false;
        }

        var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId) || userId <= 0)
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

        access = new AccountingAccess(userId, branchId);
        return true;
    }

    private static bool IsAccountingStaff(string? claimRole, string? roleCode, string? roleName)
    {
        var roleNameUpper = (roleName ?? "").Trim().ToUpperInvariant();
        return string.Equals(claimRole, "Staff", StringComparison.OrdinalIgnoreCase) &&
               (
                   string.Equals(roleCode, "ACCOUNTANT", StringComparison.OrdinalIgnoreCase) ||
                   roleNameUpper.Contains("ACCOUNTANT", StringComparison.OrdinalIgnoreCase) ||
                   roleNameUpper.Contains("KE TOAN", StringComparison.OrdinalIgnoreCase) ||
                   roleNameUpper.Contains("K? TO�N", StringComparison.OrdinalIgnoreCase)
               );
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
        var branchClaim =
            User.FindFirstValue("branch_id") ??
            User.FindFirstValue("branchId") ??
            User.FindFirstValue("BranchId");
        if (long.TryParse(branchClaim, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedBranch) && parsedBranch > 0)
        {
            return parsedBranch;
        }

        return null;
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
    private sealed record ReceivableOpenRow(long InvoiceId, string InvoiceCode, DateTime? DueDate, decimal OutstandingAmount, int DaysPastDue, bool IsOverdue, string CompanyName);
    private sealed record PayableOpenRow(long Id, string SupplierInvoiceNo, DateTime? DueDate, decimal OutstandingAmount, int DaysPastDue, bool IsOverdue);
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

    private sealed record AccountingAccess(long UserId, long? BranchId);
    public sealed class PostJournalRequest { public long PostedBy { get; set; } }
    public sealed class FinanceDecisionRequest { public string? Decision { get; set; } public long ApproverId { get; set; } public string? RejectReason { get; set; } }
    public sealed class PayrollApproveRequest { public long ApprovedBy { get; set; } }
    public sealed class PayrollPaidRequest { public long PaidBy { get; set; } }
    public sealed class PayrollCancelRequest { public long? CanceledBy { get; set; } public string? Reason { get; set; } }
    public sealed class SupplierPaymentVoidRequest { public long? ApprovalRequestId { get; set; } public long? ApprovedBy { get; set; } public string? Reason { get; set; } }
    public sealed class PayrollBatchCalculateRequest
    {
        public long? BranchId { get; set; }
        public long? PayrollPeriodId { get; set; }
        public long? PayrollRunId { get; set; }
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


