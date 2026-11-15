using System;
using System.Threading.Tasks;

namespace MyGarage.Application.Accounting;

public interface IAccountingFinanceWorkflowService
{
    Task<AccountingCommandResult> PostJournalAsync(long id, long userId, long? branchId);
    Task<AccountingCommandResult> DecideFinanceApprovalAsync(long id, string? decision, string? rejectReason, long userId, long? branchId);
    Task<AccountingCommandResult> VoidSupplierPaymentAsync(long id, long? approvalRequestId, string? reason, long userId, long? branchId);

    Task<AccountingCommandResult> GetTrialBalanceAsync(long? branchId);
    Task<AccountingCommandResult> GetSupplierPayableSummaryAsync(long? branchId);
    Task<AccountingCommandResult> GetPayrollRunSummaryAsync(long? branchId);

    Task<AccountingCommandResult> GetDashboardAsync(DateTime? fromDate, DateTime? toDate, long? branchId);
    Task<AccountingCommandResult> GetDashboardV2Async(DateTime? fromDate, DateTime? toDate, int? historyLimit, long? branchId);
    Task<AccountingFileResult> ExportDashboardV2CsvAsync(DateTime? fromDate, DateTime? toDate, int? historyLimit, long? branchId);
}
