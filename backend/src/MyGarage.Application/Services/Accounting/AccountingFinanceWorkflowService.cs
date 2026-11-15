using System.Globalization;
using System.Text;
using MyGarage.Application.Common;
using MyGarage.Common;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Application.Accounting;

public sealed class AccountingFinanceWorkflowService : IAccountingFinanceWorkflowService
{
    private const string JournalStatusDraft = nameof(JournalEntryStatusCode.DRAFT);
    private const string JournalStatusPosted = nameof(JournalEntryStatusCode.POSTED);
    private const string FinanceApprovalStatusPending = nameof(FinanceApprovalRequestStatusCode.PENDING);
    private const string FinanceApprovalStatusApproved = nameof(FinanceApprovalRequestStatusCode.APPROVED);
    private const string FinanceApprovalStatusRejected = nameof(FinanceApprovalRequestStatusCode.REJECTED);
    private const string FinanceApprovalStatusCanceled = nameof(FinanceApprovalRequestStatusCode.CANCELED);
    private const string PaymentCompletedStatus = nameof(PaymentStatusCode.COMPLETED);

    private readonly IAccountingCrudService _crud;

    public AccountingFinanceWorkflowService(IAccountingCrudService crud)
    {
        _crud = crud;
    }

    public async Task<AccountingCommandResult> PostJournalAsync(long id, long userId, long? branchId)
    {
        var row = await _crud.GetByIdAsync("journal-entries", id, branchId);
        if (row is null) return new AccountingCommandResult(404);
        if (!AsString(row, "status").Equals(JournalStatusDraft, StringComparison.OrdinalIgnoreCase))
            return new AccountingCommandResult(400, new { message = "Only DRAFT journal can be posted." });

        var sums = (await _crud.QueryAsync(
            "SELECT ISNULL(SUM(debit_amount),0) AS total_debit, ISNULL(SUM(credit_amount),0) AS total_credit, COUNT(*) AS line_count FROM dbo.journal_entry_lines WHERE journal_entry_id=@id;",
            [new QueryParam("@id", id)])).FirstOrDefault();
        if ((AsLong(sums, "lineCount") ?? 0) <= 0)
            return new AccountingCommandResult(400, new { message = "Journal has no lines." });
        if (Math.Abs(AsDecimal(sums, "totalDebit") - AsDecimal(sums, "totalCredit")) > 0.005m)
            return new AccountingCommandResult(400, new { message = "Posted journal entry must be balanced." });

        await _crud.ExecuteAsync(
            "UPDATE dbo.journal_entries SET status=@status, posted_by=@u, posted_at=SYSDATETIME(), updated_at=SYSDATETIME() WHERE id=@id;",
            [
                new QueryParam("@status", JournalStatusPosted),
                new QueryParam("@u", userId),
                new QueryParam("@id", id)
            ]);
        return new AccountingCommandResult(200, await _crud.GetByIdAsync("journal-entries", id, branchId));
    }

    public async Task<AccountingCommandResult> DecideFinanceApprovalAsync(
        long id,
        string? decision,
        string? rejectReason,
        long userId,
        long? branchId)
    {
        var normalizedDecision = (decision ?? "").Trim().ToUpperInvariant();
        if (normalizedDecision is not (FinanceApprovalStatusApproved or FinanceApprovalStatusRejected or FinanceApprovalStatusCanceled))
            return new AccountingCommandResult(400, new { message = "decision must be APPROVED/REJECTED/CANCELED." });

        var row = await _crud.GetByIdAsync("finance-approval-requests", id, branchId);
        if (row is null) return new AccountingCommandResult(404);
        if (!AsString(row, "status").Equals(FinanceApprovalStatusPending, StringComparison.OrdinalIgnoreCase))
            return new AccountingCommandResult(400, new { message = "Only PENDING request can be decided." });

        await _crud.ExecuteAsync(
            "UPDATE dbo.finance_approval_requests SET status=@s, approver_id=@a, reject_reason=@r, updated_at=SYSDATETIME() WHERE id=@id;",
            [
                new QueryParam("@s", normalizedDecision),
                new QueryParam("@a", userId),
                new QueryParam("@r", (object?)rejectReason ?? DBNull.Value),
                new QueryParam("@id", id)
            ]);
        return new AccountingCommandResult(200, await _crud.GetByIdAsync("finance-approval-requests", id, branchId));
    }

    public async Task<AccountingCommandResult> VoidSupplierPaymentAsync(
        long id,
        long? approvalRequestId,
        string? reason,
        long userId,
        long? branchId)
    {
        var payment = await _crud.GetByIdAsync("supplier-payments", id, branchId);
        if (payment is null) return new AccountingCommandResult(404);

        if (!_crud.TryGetConfig("finance-approval-requests", out var approvalCfg) || !await _crud.TableExistsAsync(approvalCfg))
            return new AccountingCommandResult(400, new { message = "finance_approval_requests table is required for supplier payment void flow." });

        var approved = await _crud.ScalarAsync<int?>(
            "SELECT TOP 1 1 FROM dbo.finance_approval_requests WHERE status=@statusApproved AND request_type='SUPPLIER_PAYMENT_VOID' AND (supplier_payment_id=@pid OR id=@aid);",
            [
                new QueryParam("@statusApproved", FinanceApprovalStatusApproved),
                new QueryParam("@pid", id),
                new QueryParam("@aid", approvalRequestId.HasValue ? approvalRequestId.Value : DBNull.Value)
            ]);
        if (!approved.HasValue)
            return new AccountingCommandResult(400, new { message = "Void requires approved SUPPLIER_PAYMENT_VOID finance approval." });

        await _crud.DeleteAsync("supplier-payments", id, branchId);
        return new AccountingCommandResult(200, new { message = "Supplier payment voided.", id, approvedBy = userId, Reason = reason });
    }

    public async Task<AccountingCommandResult> GetTrialBalanceAsync(long? branchId)
    {
        if (await _crud.ViewExistsAsync("dbo.vw_gl_trial_balance"))
            return new AccountingCommandResult(200, await _crud.QueryAsync(
                """
                IF COL_LENGTH('dbo.vw_gl_trial_balance', 'branch_id') IS NOT NULL
                    SELECT * FROM dbo.vw_gl_trial_balance WHERE branch_id=@branchId ORDER BY account_code;
                ELSE
                    SELECT * FROM dbo.vw_gl_trial_balance ORDER BY account_code;
                """,
                [new QueryParam("@branchId", (object?)branchId ?? DBNull.Value)]));
        if (!_crud.TryGetConfig("journal-entry-lines", out var lineCfg) || !await _crud.TableExistsAsync(lineCfg))
            return new AccountingCommandResult(404, new { message = "journal_entry_lines table/view unavailable." });
        return new AccountingCommandResult(200, await _crud.QueryAsync(
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
                new QueryParam("@branchId", (object?)branchId ?? DBNull.Value),
                new QueryParam("@statusPosted", JournalStatusPosted)
            ]));
    }

    public async Task<AccountingCommandResult> GetSupplierPayableSummaryAsync(long? branchId)
    {
        if (await _crud.ViewExistsAsync("dbo.vw_supplier_payable_summary"))
            return new AccountingCommandResult(200, await _crud.QueryAsync(
                """
                IF COL_LENGTH('dbo.vw_supplier_payable_summary', 'branch_id') IS NOT NULL
                    SELECT * FROM dbo.vw_supplier_payable_summary WHERE branch_id=@branchId ORDER BY due_date ASC, id DESC;
                ELSE
                    SELECT * FROM dbo.vw_supplier_payable_summary ORDER BY due_date ASC, id DESC;
                """,
                [new QueryParam("@branchId", (object?)branchId ?? DBNull.Value)]));
        if (!_crud.TryGetConfig("accounts-payable", out var apCfg) || !await _crud.TableExistsAsync(apCfg))
            return new AccountingCommandResult(404, new { message = "accounts_payable table/view unavailable." });
        return new AccountingCommandResult(200, await _crud.QueryAsync(
            "SELECT * FROM dbo.accounts_payable WHERE (@branchId IS NULL OR branch_id=@branchId) ORDER BY due_date ASC, id DESC;",
            [new QueryParam("@branchId", (object?)branchId ?? DBNull.Value)]));
    }

    public async Task<AccountingCommandResult> GetPayrollRunSummaryAsync(long? branchId)
    {
        if (await _crud.ViewExistsAsync("dbo.vw_payroll_run_summary"))
            return new AccountingCommandResult(200, await _crud.QueryAsync(
                """
                IF COL_LENGTH('dbo.vw_payroll_run_summary', 'branch_id') IS NOT NULL
                    SELECT * FROM dbo.vw_payroll_run_summary WHERE branch_id=@branchId ORDER BY payroll_run_id DESC;
                ELSE
                    SELECT * FROM dbo.vw_payroll_run_summary ORDER BY payroll_run_id DESC;
                """,
                [new QueryParam("@branchId", (object?)branchId ?? DBNull.Value)]));
        if (!_crud.TryGetConfig("payroll-runs", out var runCfg) || !await _crud.TableExistsAsync(runCfg))
            return new AccountingCommandResult(404, new { message = "payroll_runs table/view unavailable." });
        return new AccountingCommandResult(200, await _crud.QueryAsync(
            "SELECT * FROM dbo.payroll_runs WHERE (@branchId IS NULL OR branch_id=@branchId) ORDER BY id DESC;",
            [new QueryParam("@branchId", (object?)branchId ?? DBNull.Value)]));
    }

    public async Task<AccountingCommandResult> GetDashboardAsync(DateTime? fromDate, DateTime? toDate, long? branchId)
    {
        var metrics = await GetDashboardCoreMetricsAsync(fromDate, toDate, branchId);
        return new AccountingCommandResult(200, new
        {
            fromDate = metrics.FromDate,
            toDate = metrics.ToDate,
            revenue = metrics.Revenue,
            netProfit = metrics.NetProfit,
            cashFlow = metrics.CashFlow,
            collected = metrics.Collected,
            refund = metrics.Refund,
            reversal = metrics.Reversal
        });
    }

    public async Task<AccountingCommandResult> GetDashboardV2Async(
        DateTime? fromDate,
        DateTime? toDate,
        int? historyLimit,
        long? branchId)
    {
        var metrics = await GetDashboardCoreMetricsAsync(fromDate, toDate, branchId);
        var generatedAt = VietnamTime.Now;
        _ = historyLimit;

        return new AccountingCommandResult(200, new
        {
            generatedAt,
            fromDate = metrics.FromDate,
            toDate = metrics.ToDate,
            summary = new
            {
                revenue = metrics.Revenue,
                collected = metrics.Collected,
                netProfit = metrics.NetProfit,
                cashFlow = metrics.CashFlow,
                refund = metrics.Refund,
                reversal = metrics.Reversal,
                totalOutstandingAr = 0m,
                totalOutstandingAp = 0m,
                overdueArCount = 0,
                overdueApAmount = 0m,
                pendingApprovalCount = 0
            },
            agingBuckets = Array.Empty<object>(),
            invoiceWatchlist = Array.Empty<object>(),
            apWatchlist = Array.Empty<object>(),
            paymentHistory = Array.Empty<object>(),
            refundTrail = Array.Empty<object>(),
            approvalTrail = Array.Empty<object>()
        });
    }

    public async Task<AccountingFileResult> ExportDashboardV2CsvAsync(
        DateTime? fromDate,
        DateTime? toDate,
        int? historyLimit,
        long? branchId)
    {
        var metrics = await GetDashboardCoreMetricsAsync(fromDate, toDate, branchId);
        var generatedAt = VietnamTime.Now;

        var csv = new StringBuilder();
        csv.AppendLine("section,key,value");
        csv.AppendLine($"meta,generatedAt,{EscapeCsv(generatedAt.ToString("yyyy-MM-dd HH:mm:ss"))}");
        csv.AppendLine($"meta,fromDate,{EscapeCsv(metrics.FromDate.ToString("yyyy-MM-dd HH:mm:ss"))}");
        csv.AppendLine($"meta,toDate,{EscapeCsv(metrics.ToDate.ToString("yyyy-MM-dd HH:mm:ss"))}");
        csv.AppendLine($"summary,revenue,{metrics.Revenue.ToString(CultureInfo.InvariantCulture)}");
        csv.AppendLine($"summary,collected,{metrics.Collected.ToString(CultureInfo.InvariantCulture)}");
        csv.AppendLine($"summary,netProfit,{metrics.NetProfit.ToString(CultureInfo.InvariantCulture)}");
        csv.AppendLine($"summary,cashFlow,{metrics.CashFlow.ToString(CultureInfo.InvariantCulture)}");
        csv.AppendLine($"summary,refund,{metrics.Refund.ToString(CultureInfo.InvariantCulture)}");
        csv.AppendLine($"summary,reversal,{metrics.Reversal.ToString(CultureInfo.InvariantCulture)}");
        csv.AppendLine("summary,totalOutstandingAr,0");
        csv.AppendLine("summary,totalOutstandingAp,0");
        csv.AppendLine("summary,overdueArCount,0");
        csv.AppendLine("summary,overdueApAmount,0");
        csv.AppendLine("summary,pendingApprovalCount,0");
        _ = historyLimit;

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        var fileName = $"dashboard-ke-toan-v2-{generatedAt:yyyyMMdd-HHmmss}.csv";
        return new AccountingFileResult(bytes, "text/csv; charset=utf-8", fileName);
    }

    private async Task<DashboardCoreMetrics> GetDashboardCoreMetricsAsync(DateTime? fromDate, DateTime? toDate, long? branchId)
    {
        var from = fromDate ?? VietnamTime.Now.Date.AddDays(-30);
        var to = toDate ?? VietnamTime.Now.Date.AddDays(1).AddTicks(-1);
        var revenue = (await _crud.QueryAsync(
            "SELECT ISNULL(SUM(TotalRevenue),0) AS revenue, ISNULL(SUM(NetProfit),0) AS net_profit, ISNULL(SUM(NetCashFlow),0) AS cash_flow FROM dbo.RevenueSnapshots WHERE SnapshotDate BETWEEN @f AND @t AND (@branchId IS NULL OR BranchId=@branchId);",
            [
                new QueryParam("@f", from.Date),
                new QueryParam("@t", to.Date),
                new QueryParam("@branchId", (object?)branchId ?? DBNull.Value)
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
                new QueryParam("@f", from),
                new QueryParam("@t", to),
                new QueryParam("@branchId", (object?)branchId ?? DBNull.Value),
                new QueryParam("@paymentCompletedStatus", PaymentCompletedStatus)
            ])).FirstOrDefault();

        return new DashboardCoreMetrics(
            from,
            to,
            AsDecimal(revenue, "revenue"),
            AsDecimal(revenue, "netProfit"),
            AsDecimal(revenue, "cashFlow"),
            AsDecimal(payments, "collected"),
            AsDecimal(payments, "refund"),
            AsDecimal(payments, "reversal"));
    }

    private static string AsString(IDictionary<string, object?>? row, string key) =>
        row != null && row.TryGetValue(key, out var v) && v != null ? Convert.ToString(v) ?? "" : "";

    private static long? AsLong(IDictionary<string, object?>? row, string key) =>
        row != null && row.TryGetValue(key, out var v) && v != null && v != DBNull.Value ? Convert.ToInt64(v) : null;

    private static decimal AsDecimal(IDictionary<string, object?>? row, string key) =>
        row != null && row.TryGetValue(key, out var v) && v != null && v != DBNull.Value ? Convert.ToDecimal(v) : 0m;

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.IndexOfAny([',', '"', '\r', '\n']) >= 0)
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private sealed record DashboardCoreMetrics(
        DateTime FromDate,
        DateTime ToDate,
        decimal Revenue,
        decimal NetProfit,
        decimal CashFlow,
        decimal Collected,
        decimal Refund,
        decimal Reversal);
}
