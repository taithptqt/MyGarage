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
            return await GetBranchScopedViewAsync("dbo.vw_gl_trial_balance", "account_code", branchId);
        if (!_crud.TryGetConfig("journal-entry-lines", out var lineCfg) || !await _crud.TableExistsAsync(lineCfg))
            return new AccountingCommandResult(404, new { message = "journal_entry_lines table/view unavailable." });
        if (branchId.HasValue && !await HasBranchColumnAsync("dbo.journal_entry_lines", "branch_id") &&
            !await HasBranchColumnAsync("dbo.journal_entry_lines", "BranchId"))
            return AccountingCommandResult.Problem(503, "Không thể xác định chi nhánh của dữ liệu sổ cái.");
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
            return await GetBranchScopedViewAsync("dbo.vw_supplier_payable_summary", "due_date ASC, id DESC", branchId);
        if (!_crud.TryGetConfig("accounts-payable", out var apCfg) || !await _crud.TableExistsAsync(apCfg))
            return new AccountingCommandResult(404, new { message = "accounts_payable table/view unavailable." });
        return new AccountingCommandResult(200, await _crud.QueryAsync(
            "SELECT * FROM dbo.accounts_payable WHERE (@branchId IS NULL OR branch_id=@branchId) ORDER BY due_date ASC, id DESC;",
            [new QueryParam("@branchId", (object?)branchId ?? DBNull.Value)]));
    }

    public async Task<AccountingCommandResult> GetPayrollRunSummaryAsync(long? branchId)
    {
        if (await _crud.ViewExistsAsync("dbo.vw_payroll_run_summary"))
            return await GetBranchScopedViewAsync("dbo.vw_payroll_run_summary", "payroll_run_id DESC", branchId);
        if (!_crud.TryGetConfig("payroll-runs", out var runCfg) || !await _crud.TableExistsAsync(runCfg))
            return new AccountingCommandResult(404, new { message = "payroll_runs table/view unavailable." });
        return new AccountingCommandResult(200, await _crud.QueryAsync(
            "SELECT * FROM dbo.payroll_runs WHERE (@branchId IS NULL OR branch_id=@branchId) ORDER BY id DESC;",
            [new QueryParam("@branchId", (object?)branchId ?? DBNull.Value)]));
    }

    private async Task<bool> HasBranchColumnAsync(string objectName, string columnName) =>
        await _crud.ScalarAsync<int>(
            "SELECT CASE WHEN COL_LENGTH(@objectName, @columnName) IS NULL THEN 0 ELSE 1 END;",
            [new QueryParam("@objectName", objectName), new QueryParam("@columnName", columnName)]) == 1;

    private async Task<AccountingCommandResult> GetBranchScopedViewAsync(string viewName, string orderBy, long? branchId)
    {
        var hasBranchColumn = await HasBranchColumnAsync(viewName, "branch_id");
        if (branchId.HasValue && !hasBranchColumn)
            return AccountingCommandResult.Problem(503, "Báo cáo chưa hỗ trợ phân quyền theo chi nhánh.");

        var where = hasBranchColumn ? " WHERE (@branchId IS NULL OR branch_id=@branchId)" : "";
        return new AccountingCommandResult(200, await _crud.QueryAsync(
            $"SELECT * FROM {viewName}{where} ORDER BY {orderBy};",
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
        var payables = await GetDashboardPayablesAsync(branchId, generatedAt.Date, historyLimit);
        var receivables = await GetDashboardReceivablesAsync(branchId, historyLimit, generatedAt.Date);
        var paymentHistory = await GetDashboardPaymentHistoryAsync(metrics.FromDate, metrics.ToDate, branchId, historyLimit);
        var refundTrail = await GetDashboardPaymentHistoryAsync(metrics.FromDate, metrics.ToDate, branchId, historyLimit, refundsOnly: true);
        var approvals = await GetDashboardApprovalsAsync(metrics.FromDate, metrics.ToDate, branchId, historyLimit);

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
                totalOutstandingAr = receivables?.TotalOutstanding,
                totalOutstandingAp = payables?.TotalOutstanding,
                overdueArCount = receivables?.OverdueCount,
                overdueApAmount = payables?.OverdueAmount,
                pendingApprovalCount = approvals?.PendingCount
            },
            detailsAvailable = receivables?.AgingAvailable == true,
            payablesAvailable = payables != null,
            receivablesAvailable = receivables != null,
            paymentHistoryAvailable = true,
            refundTrailAvailable = true,
            approvalTrailAvailable = approvals != null,
            agingBuckets = receivables?.AgingBuckets ?? Array.Empty<Dictionary<string, object?>>(),
            invoiceWatchlist = receivables?.Watchlist ?? Array.Empty<Dictionary<string, object?>>(),
            apWatchlist = payables?.Watchlist ?? Array.Empty<Dictionary<string, object?>>(),
            paymentHistory = paymentHistory,
            refundTrail = refundTrail,
            approvalTrail = approvals?.Trail ?? Array.Empty<Dictionary<string, object?>>()
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
        var payables = await GetDashboardPayablesAsync(branchId, generatedAt.Date, historyLimit);
        var receivables = await GetDashboardReceivablesAsync(branchId, historyLimit, generatedAt.Date);
        var approvals = await GetDashboardApprovalsAsync(metrics.FromDate, metrics.ToDate, branchId, historyLimit);

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
        csv.AppendLine($"meta,detailsAvailable,{(receivables?.AgingAvailable == true ? "true" : "false")}");
        csv.AppendLine($"meta,payablesAvailable,{(payables != null ? "true" : "false")}");
        csv.AppendLine($"meta,receivablesAvailable,{(receivables != null ? "true" : "false")}");
        csv.AppendLine($"summary,totalOutstandingAr,{receivables?.TotalOutstanding.ToString(CultureInfo.InvariantCulture)}");
        csv.AppendLine($"summary,totalOutstandingAp,{payables?.TotalOutstanding.ToString(CultureInfo.InvariantCulture)}");
        csv.AppendLine($"summary,overdueArCount,{receivables?.OverdueCount?.ToString(CultureInfo.InvariantCulture)}");
        csv.AppendLine($"summary,overdueApAmount,{payables?.OverdueAmount.ToString(CultureInfo.InvariantCulture)}");
        csv.AppendLine($"summary,pendingApprovalCount,{approvals?.PendingCount.ToString(CultureInfo.InvariantCulture)}");
        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        var fileName = $"dashboard-ke-toan-v2-{generatedAt:yyyyMMdd-HHmmss}.csv";
        return new AccountingFileResult(bytes, "text/csv; charset=utf-8", fileName);
    }

    private async Task<DashboardPayables?> GetDashboardPayablesAsync(long? branchId, DateTime today, int? historyLimit)
    {
        if (!_crud.TryGetConfig("accounts-payable", out var config) || !await _crud.TableExistsAsync(config) ||
            !await HasBranchColumnAsync("dbo.accounts_payable", "branch_id"))
            return null;

        var parameters = new[]
        {
            new QueryParam("@branchId", (object?)branchId ?? DBNull.Value),
            new QueryParam("@today", today),
            new QueryParam("@limit", Math.Clamp(historyLimit ?? 30, 1, 300))
        };
        var summary = (await _crud.QueryAsync(
            """
            SELECT COALESCE(SUM(outstanding_amount), 0) AS total_outstanding,
                   COALESCE(SUM(CASE WHEN due_date < @today THEN outstanding_amount ELSE 0 END), 0) AS overdue_amount
            FROM dbo.accounts_payable
            WHERE (@branchId IS NULL OR branch_id = @branchId)
              AND status IN ('OPEN', 'PARTIAL', 'OVERDUE') AND outstanding_amount > 0;
            """, parameters)).FirstOrDefault();
        var watchlist = await _crud.QueryAsync(
            """
            SELECT TOP (@limit) id, supplier_invoice_no, due_date, outstanding_amount,
                   CASE WHEN due_date < @today THEN DATEDIFF(day, due_date, @today) ELSE 0 END AS days_past_due
            FROM dbo.accounts_payable
            WHERE (@branchId IS NULL OR branch_id = @branchId)
              AND status IN ('OPEN', 'PARTIAL', 'OVERDUE') AND outstanding_amount > 0
            ORDER BY due_date ASC, id DESC;
            """, parameters);
        return new DashboardPayables(AsDecimal(summary, "totalOutstanding"), AsDecimal(summary, "overdueAmount"), watchlist);
    }

    private sealed record DashboardPayables(decimal TotalOutstanding, decimal OverdueAmount, IReadOnlyList<Dictionary<string, object?>> Watchlist);

    private async Task<DashboardReceivables?> GetDashboardReceivablesAsync(long? branchId, int? historyLimit, DateTime today)
    {
        if (!await HasBranchColumnAsync("dbo.Invoices", "PaymentStatus")) return null;
        var parameters = new[] { new QueryParam("@branchId", (object?)branchId ?? DBNull.Value),
            new QueryParam("@limit", Math.Clamp(historyLimit ?? 30, 1, 300)), new QueryParam("@today", today) };
        var incomplete = (await _crud.QueryAsync(
            """
            SELECT COUNT(*) AS incomplete_count FROM dbo.Invoices i
            LEFT JOIN dbo.ServiceOrders so ON so.Id = i.ServiceOrderId
            LEFT JOIN dbo.RetailOrders ro ON ro.Id = i.RetailOrderId
            WHERE UPPER(i.PayerType) = 'CUSTOMER'
              AND (@branchId IS NULL OR COALESCE(so.ExecuteBranchId, so.OriginBranchId, ro.BranchId) = @branchId)
              AND (i.PaymentStatus IS NULL OR i.TotalAmount IS NULL OR i.PaidAmount IS NULL);
            """, parameters)).FirstOrDefault();
        if ((AsLong(incomplete, "incompleteCount") ?? 0) > 0) return null;
        const string source = """
            FROM dbo.Invoices i
            LEFT JOIN dbo.ServiceOrders so ON so.Id = i.ServiceOrderId
            LEFT JOIN dbo.RetailOrders ro ON ro.Id = i.RetailOrderId
            WHERE UPPER(i.PayerType) = 'CUSTOMER'
              AND UPPER(i.PaymentStatus) IN ('UNPAID', 'PARTIAL')
              AND i.TotalAmount > COALESCE(i.PaidAmount, 0)
              AND (@branchId IS NULL OR COALESCE(so.ExecuteBranchId, so.OriginBranchId, ro.BranchId) = @branchId)
            """;
        var totals = (await _crud.QueryAsync(
            $"SELECT COALESCE(SUM(i.TotalAmount - COALESCE(i.PaidAmount, 0)), 0) AS total_outstanding {source};", parameters)).FirstOrDefault();
        var hasDueDate = await HasBranchColumnAsync("dbo.Invoices", "DueDate");
        var dateColumns = hasDueDate
            ? ", i.DueDate AS due_date, CASE WHEN i.DueDate IS NULL THEN NULL WHEN i.DueDate < @today THEN DATEDIFF(day, i.DueDate, @today) ELSE 0 END AS days_past_due"
            : "";
        var rows = await _crud.QueryAsync(
            $"SELECT TOP (@limit) i.Id AS invoice_id, i.TotalAmount - COALESCE(i.PaidAmount, 0) AS outstanding_amount {dateColumns} {source} ORDER BY i.Id DESC;", parameters);
        if (!hasDueDate)
            return new DashboardReceivables(AsDecimal(totals, "totalOutstanding"), rows, false, null, Array.Empty<Dictionary<string, object?>>());

        var ageStats = (await _crud.QueryAsync(
            $"SELECT SUM(CASE WHEN i.DueDate IS NULL THEN 1 ELSE 0 END) AS missing_count, SUM(CASE WHEN i.DueDate < @today THEN 1 ELSE 0 END) AS overdue_count {source};", parameters)).FirstOrDefault();
        var aging = await _crud.QueryAsync(
            $"SELECT CASE WHEN i.DueDate IS NULL THEN N'Chưa có hạn' WHEN i.DueDate >= @today THEN N'Chưa đến hạn' WHEN DATEDIFF(day, i.DueDate, @today) <= 30 THEN N'1–30 ngày' WHEN DATEDIFF(day, i.DueDate, @today) <= 60 THEN N'31–60 ngày' WHEN DATEDIFF(day, i.DueDate, @today) <= 90 THEN N'61–90 ngày' ELSE N'Trên 90 ngày' END AS label, COUNT(*) AS count, SUM(i.TotalAmount - COALESCE(i.PaidAmount, 0)) AS amount {source} GROUP BY CASE WHEN i.DueDate IS NULL THEN N'Chưa có hạn' WHEN i.DueDate >= @today THEN N'Chưa đến hạn' WHEN DATEDIFF(day, i.DueDate, @today) <= 30 THEN N'1–30 ngày' WHEN DATEDIFF(day, i.DueDate, @today) <= 60 THEN N'31–60 ngày' WHEN DATEDIFF(day, i.DueDate, @today) <= 90 THEN N'61–90 ngày' ELSE N'Trên 90 ngày' END;", parameters);
        var complete = (AsLong(ageStats, "missingCount") ?? 0) == 0;
        return new DashboardReceivables(AsDecimal(totals, "totalOutstanding"), rows, complete, complete ? (int)(AsLong(ageStats, "overdueCount") ?? 0) : null, aging);
    }

    private sealed record DashboardReceivables(decimal TotalOutstanding, IReadOnlyList<Dictionary<string, object?>> Watchlist, bool AgingAvailable, int? OverdueCount, IReadOnlyList<Dictionary<string, object?>> AgingBuckets);

    private async Task<DashboardApprovals?> GetDashboardApprovalsAsync(DateTime from, DateTime to, long? branchId, int? historyLimit)
    {
        // Approval requests do not have a reliable branch for every request type.
        if (branchId.HasValue || !_crud.TryGetConfig("finance-approval-requests", out var config) || !await _crud.TableExistsAsync(config))
            return null;

        var parameters = new[] { new QueryParam("@from", from), new QueryParam("@to", to),
            new QueryParam("@limit", Math.Clamp(historyLimit ?? 30, 1, 300)), new QueryParam("@pending", FinanceApprovalStatusPending) };
        var count = await _crud.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.finance_approval_requests WHERE status=@pending;", parameters);
        var trail = await _crud.QueryAsync(
            """
            SELECT TOP (@limit) id, request_type, status, updated_at
            FROM dbo.finance_approval_requests
            WHERE created_at BETWEEN @from AND @to
            ORDER BY created_at DESC, id DESC;
            """, parameters);
        return new DashboardApprovals(count, trail);
    }

    private sealed record DashboardApprovals(int PendingCount, IReadOnlyList<Dictionary<string, object?>> Trail);

    private Task<IReadOnlyList<Dictionary<string, object?>>> GetDashboardPaymentHistoryAsync(
        DateTime from, DateTime to, long? branchId, int? historyLimit, bool refundsOnly = false) =>
        _crud.QueryAsync(
            """
            SELECT TOP (@limit) p.Id AS id, p.Type AS type, p.Status AS status, p.Amount AS amount, p.CreatedAt AS created_at
            FROM dbo.Payments p
            LEFT JOIN dbo.Invoices i ON i.Id = p.InvoiceId
            LEFT JOIN dbo.ServiceOrders so ON so.Id = COALESCE(p.ServiceOrderId, i.ServiceOrderId)
            LEFT JOIN dbo.RetailOrders ro ON ro.Id = COALESCE(p.RetailOrderId, i.RetailOrderId)
            WHERE p.CreatedAt BETWEEN @from AND @to
              AND (@refundsOnly = 0 OR UPPER(p.Type) IN ('REFUND', 'REVERSAL'))
              AND (@branchId IS NULL OR so.ExecuteBranchId = @branchId OR so.OriginBranchId = @branchId OR ro.BranchId = @branchId)
            ORDER BY p.CreatedAt DESC, p.Id DESC;
            """,
            [new QueryParam("@from", from), new QueryParam("@to", to),
             new QueryParam("@branchId", (object?)branchId ?? DBNull.Value),
             new QueryParam("@refundsOnly", refundsOnly),
             new QueryParam("@limit", Math.Clamp(historyLimit ?? 30, 1, 300))]);

    private async Task<DashboardCoreMetrics> GetDashboardCoreMetricsAsync(DateTime? fromDate, DateTime? toDate, long? branchId)
    {
        var from = fromDate ?? VietnamTime.Now.Date.AddDays(-30);
        var to = toDate.HasValue && toDate.Value.TimeOfDay != TimeSpan.Zero
            ? toDate.Value
            : (toDate?.Date ?? VietnamTime.Now.Date).AddDays(1).AddTicks(-1);
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
