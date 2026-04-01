using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Services;

namespace MyGarage.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AccountingController : ControllerBase
{
    private readonly AccountingCrudService _crud;
    private readonly GarageContext _db;

    public AccountingController(AccountingCrudService crud, GarageContext db)
    {
        _crud = crud;
        _db = db;
    }

    [HttpGet("table/{tableKey}")]
    public async Task<IActionResult> GetTable(string tableKey, [FromQuery] int limit = 500)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        try { return Ok(await _crud.GetAllAsync(tableKey, limit, access.BranchId)); }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("table/{tableKey}/{id:long}")]
    public async Task<IActionResult> GetById(string tableKey, long id)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
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
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
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
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
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
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        try { return await _crud.DeleteAsync(tableKey, id, access.BranchId) ? NoContent() : NotFound(); }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("journal-entries/{id:long}/post")]
    public async Task<IActionResult> PostJournal(long id, [FromBody] PostJournalRequest request)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        var row = await _crud.GetByIdAsync("journal-entries", id, access.BranchId);
        if (row is null) return NotFound();
        if (!AsString(row, "status").Equals("DRAFT", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only DRAFT journal can be posted." });

        var sums = (await _crud.QueryAsync(
            "SELECT ISNULL(SUM(debit_amount),0) AS total_debit, ISNULL(SUM(credit_amount),0) AS total_credit, COUNT(*) AS line_count FROM dbo.journal_entry_lines WHERE journal_entry_id=@id;",
            [new SqlParameter("@id", id)])).FirstOrDefault();
        if ((AsLong(sums, "lineCount") ?? 0) <= 0) return BadRequest(new { message = "Journal has no lines." });
        if (Math.Abs(AsDecimal(sums, "totalDebit") - AsDecimal(sums, "totalCredit")) > 0.005m)
            return BadRequest(new { message = "Posted journal entry must be balanced." });

        await _crud.ExecuteAsync(
            "UPDATE dbo.journal_entries SET status='POSTED', posted_by=@u, posted_at=SYSDATETIME(), updated_at=SYSDATETIME() WHERE id=@id;",
            [new SqlParameter("@u", access.UserId), new SqlParameter("@id", id)]);
        return Ok(await _crud.GetByIdAsync("journal-entries", id, access.BranchId));
    }

    [HttpPost("finance-approvals/{id:long}/decision")]
    public async Task<IActionResult> DecideFinanceApproval(long id, [FromBody] FinanceDecisionRequest request)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        var decision = (request.Decision ?? "").Trim().ToUpperInvariant();
        if (decision is not ("APPROVED" or "REJECTED" or "CANCELED"))
            return BadRequest(new { message = "decision must be APPROVED/REJECTED/CANCELED." });

        var row = await _crud.GetByIdAsync("finance-approval-requests", id, access.BranchId);
        if (row is null) return NotFound();
        if (!AsString(row, "status").Equals("PENDING", StringComparison.OrdinalIgnoreCase))
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
        if (!AsString(row, "status").Equals("DRAFT", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only DRAFT payroll run can be approved." });

        var lineCount = await _crud.ScalarAsync<int?>(
            "SELECT COUNT(*) FROM dbo.payroll_items WHERE payroll_run_id=@id;",
            [new SqlParameter("@id", id)]);
        if (!lineCount.HasValue || lineCount.Value <= 0)
            return BadRequest(new { message = "Cannot approve payroll run without payroll items." });

        await _crud.ExecuteAsync(
            "UPDATE dbo.payroll_runs SET status='APPROVED', approved_by=@u, approved_at=SYSDATETIME(), updated_at=SYSDATETIME() WHERE id=@id;",
            [new SqlParameter("@u", access.UserId), new SqlParameter("@id", id)]);
        return Ok(await _crud.GetByIdAsync("payroll-runs", id, access.BranchId));
    }

    [HttpPost("payroll-runs/{id:long}/mark-paid")]
    public async Task<IActionResult> MarkPayrollPaid(long id, [FromBody] PayrollPaidRequest request)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;
        var row = await _crud.GetByIdAsync("payroll-runs", id, access.BranchId);
        if (row is null) return NotFound();
        if (!AsString(row, "status").Equals("APPROVED", StringComparison.OrdinalIgnoreCase) || !AsLong(row, "approvedBy").HasValue)
            return BadRequest(new { message = "Run must be APPROVED and approvedBy set before PAID." });

        await _crud.ExecuteAsync(
            "UPDATE dbo.payroll_runs SET status='PAID', paid_at=SYSDATETIME(), updated_at=SYSDATETIME() WHERE id=@id;",
            [new SqlParameter("@id", id)]);
        return Ok(await _crud.GetByIdAsync("payroll-runs", id, access.BranchId));
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
            "SELECT TOP 1 1 FROM dbo.finance_approval_requests WHERE status='APPROVED' AND request_type='SUPPLIER_PAYMENT_VOID' AND (supplier_payment_id=@pid OR id=@aid);",
            [new SqlParameter("@pid", id), new SqlParameter("@aid", request.ApprovalRequestId.HasValue ? request.ApprovalRequestId.Value : DBNull.Value)]);
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
            """
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
                WHERE je.status='POSTED' AND (@branchId IS NULL OR jel.branch_id=@branchId)
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
                WHERE je.status='POSTED' AND (@branchId IS NULL OR jel.BranchId=@branchId)
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
                WHERE je.status='POSTED'
                GROUP BY coa.account_code, coa.account_name
                ORDER BY coa.account_code;
            """,
            [new SqlParameter("@branchId", (object?)access.BranchId ?? DBNull.Value)]));
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
    public async Task<IActionResult> StaffBaseSalary(long staffId, [FromQuery] DateTime? asOfDate = null)
    {
        if (!TryGetAccountingAccess(out var access, out var denied)) return denied;

        var staff = await _db.Staffs
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == staffId && s.IsDeleted != true);
        if (staff is null) return NotFound(new { message = $"Staff #{staffId} not found." });
        if (access.BranchId.HasValue && staff.BranchId != access.BranchId.Value) return NotFound();

        var asOf = DateOnly.FromDateTime((asOfDate ?? DateTime.UtcNow).Date);
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
        var asOf = DateOnly.FromDateTime((asOfDate ?? DateTime.UtcNow).Date);
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

        var effectiveFromDate = DateOnly.FromDateTime((request.EffectiveFrom ?? DateTime.UtcNow).Date);
        var existingBaseSalary = await ResolveStaffBaseSalaryAsync(staff.Id, access.BranchId, effectiveFromDate);
        if (existingBaseSalary.HasValue && existingBaseSalary.Value > 0m)
        {
            return BadRequest(new { message = "Staff already has an active base salary profile for the selected effective date." });
        }

        var pendingRequest = (await _crud.QueryAsync(
            """
            SELECT TOP 1 id
            FROM dbo.finance_approval_requests
            WHERE request_type='PAYROLL_ADJUSTMENT'
              AND JSON_VALUE(request_payload, '$.requestSubtype') = 'STAFF_INITIAL_SALARY'
              AND status='PENDING'
              AND TRY_CONVERT(bigint, JSON_VALUE(request_payload, '$.staffId')) = @staffId
            ORDER BY id DESC;
            """,
            [new SqlParameter("@staffId", staff.Id)])).FirstOrDefault();
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
            requestedAt = DateTime.UtcNow.ToString("O")
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
                'PENDING',
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
            status = "PENDING",
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
        var from = fromDate ?? DateTime.UtcNow.Date.AddDays(-30);
        var to = toDate ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);
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
                  AND UPPER(ISNULL(p.Status,'')) IN ('COMPLETED','PAID','SUCCESS')
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
                  AND UPPER(ISNULL(p.Status,'')) IN ('COMPLETED','PAID','SUCCESS')
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
                new SqlParameter("@branchId", (object?)access.BranchId ?? DBNull.Value)
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
                  AND UPPER(ISNULL(status,'ACTIVE'))='ACTIVE'
                ORDER BY effective_from DESC, id DESC;
                """,
                [
                    new SqlParameter("@staffId", staffId),
                    new SqlParameter("@branchId", (object?)branchId ?? DBNull.Value),
                    new SqlParameter("@asOf", asOf)
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
                  AND UPPER(ISNULL(status,'ACTIVE'))='ACTIVE'
                ORDER BY effective_from DESC, id DESC;
                """,
                [
                    new SqlParameter("@staffId", staffId),
                    new SqlParameter("@asOf", asOf)
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
        var roleNameUpper = (roleName ?? "").Trim().ToUpperInvariant();
        var isAccountant =
            string.Equals(claimRole, "Staff", StringComparison.OrdinalIgnoreCase) &&
            (
                string.Equals(roleCode, "ACCOUNTANT", StringComparison.OrdinalIgnoreCase) ||
                roleNameUpper.Contains("ACCOUNTANT", StringComparison.OrdinalIgnoreCase) ||
                roleNameUpper.Contains("KE TOAN", StringComparison.OrdinalIgnoreCase)
            );

        if (!isAccountant)
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

        long? branchId = null;
        var branchClaim =
            User.FindFirstValue("branch_id") ??
            User.FindFirstValue("branchId") ??
            User.FindFirstValue("BranchId");
        if (long.TryParse(branchClaim, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedBranch) && parsedBranch > 0)
        {
            branchId = parsedBranch;
        }
        if (!branchId.HasValue)
        {
            deniedResult = Forbid();
            return false;
        }

        access = new AccountingAccess(userId, branchId);
        return true;
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

    private sealed record AccountingAccess(long UserId, long? BranchId);
    public sealed class PostJournalRequest { public long PostedBy { get; set; } }
    public sealed class FinanceDecisionRequest { public string? Decision { get; set; } public long ApproverId { get; set; } public string? RejectReason { get; set; } }
    public sealed class PayrollApproveRequest { public long ApprovedBy { get; set; } }
    public sealed class PayrollPaidRequest { public long PaidBy { get; set; } }
    public sealed class SupplierPaymentVoidRequest { public long? ApprovalRequestId { get; set; } public long? ApprovedBy { get; set; } public string? Reason { get; set; } }
    public sealed class InitialSalaryRequestDto
    {
        public decimal? BaseSalary { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public string? Reason { get; set; }
        public string? Note { get; set; }
    }
}

