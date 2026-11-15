using System.Text.Json;

namespace MyGarage.Application.Accounting;

public sealed class AccountingCommandFacade : IAccountingCommandFacade
{
    private readonly IAccountingTableAndInvoiceService _tableAndInvoiceService;
    private readonly IAccountingFinanceWorkflowService _financeWorkflowService;
    private readonly IAccountingPayrollCommandService _payrollCommandService;

    public AccountingCommandFacade(
        IAccountingTableAndInvoiceService tableAndInvoiceService,
        IAccountingFinanceWorkflowService financeWorkflowService,
        IAccountingPayrollCommandService payrollCommandService)
    {
        _tableAndInvoiceService = tableAndInvoiceService;
        _financeWorkflowService = financeWorkflowService;
        _payrollCommandService = payrollCommandService;
    }

    public AccountingAccessResult AuthorizeTableAccess(string tableKey, AccountingActorContext actor) =>
        _tableAndInvoiceService.AuthorizeTableAccess(tableKey, actor);

    public AccountingAccessResult AuthorizeAccountingAccess(AccountingActorContext actor) =>
        _tableAndInvoiceService.AuthorizeAccountingAccess(actor);

    public AccountingAccessResult AuthorizeFinanceApprovalAccess(AccountingActorContext actor) =>
        _tableAndInvoiceService.AuthorizeFinanceApprovalAccess(actor);

    public Task<AccountingCommandResult> GetTableAsync(string tableKey, int limit, long? branchId) =>
        _tableAndInvoiceService.GetTableAsync(tableKey, limit, branchId);

    public Task<AccountingCommandResult> GetTableRowByIdAsync(string tableKey, long id, long? branchId) =>
        _tableAndInvoiceService.GetTableRowByIdAsync(tableKey, id, branchId);

    public Task<AccountingCommandResult> CreateTableRowAsync(string tableKey, JsonElement payload, long? branchId) =>
        _tableAndInvoiceService.CreateTableRowAsync(tableKey, payload, branchId);

    public Task<AccountingCommandResult> UpdateTableRowAsync(string tableKey, long id, JsonElement payload, long? branchId) =>
        _tableAndInvoiceService.UpdateTableRowAsync(tableKey, id, payload, branchId);

    public Task<AccountingCommandResult> DeleteTableRowAsync(string tableKey, long id, long? branchId) =>
        _tableAndInvoiceService.DeleteTableRowAsync(tableKey, id, branchId);

    public Task<AccountingCommandResult> CreateSupplierInvoiceAsync(JsonElement payload, long? branchId) =>
        _tableAndInvoiceService.CreateSupplierInvoiceAsync(payload, branchId);

    public Task<AccountingCommandResult> UpdateSupplierInvoiceAsync(long id, JsonElement payload, long? branchId) =>
        _tableAndInvoiceService.UpdateSupplierInvoiceAsync(id, payload, branchId);

    public Task<AccountingCommandResult> CreateSupplierInvoiceDetailAsync(JsonElement payload, long? branchId) =>
        _tableAndInvoiceService.CreateSupplierInvoiceDetailAsync(payload, branchId);

    public Task<AccountingCommandResult> UpdateSupplierInvoiceDetailAsync(long id, JsonElement payload, long? branchId) =>
        _tableAndInvoiceService.UpdateSupplierInvoiceDetailAsync(id, payload, branchId);

    public Task<AccountingCommandResult> PostJournalAsync(long id, long userId, long? branchId) =>
        _financeWorkflowService.PostJournalAsync(id, userId, branchId);

    public Task<AccountingCommandResult> DecideFinanceApprovalAsync(long id, string? decision, string? rejectReason, long userId, long? branchId) =>
        _financeWorkflowService.DecideFinanceApprovalAsync(id, decision, rejectReason, userId, branchId);

    public Task<AccountingCommandResult> VoidSupplierPaymentAsync(long id, long? approvalRequestId, string? reason, long userId, long? branchId) =>
        _financeWorkflowService.VoidSupplierPaymentAsync(id, approvalRequestId, reason, userId, branchId);

    public Task<AccountingCommandResult> ApprovePayrollAsync(long id, long userId, long? branchId, CancellationToken cancellationToken = default) =>
        _payrollCommandService.ApprovePayrollAsync(id, userId, branchId, cancellationToken);

    public Task<AccountingCommandResult> MarkPayrollPaidAsync(long id, long? branchId) =>
        _payrollCommandService.MarkPayrollPaidAsync(id, branchId);

    public Task<AccountingCommandResult> CancelPayrollRunAsync(long id, long userId, long? branchId, CancellationToken cancellationToken = default) =>
        _payrollCommandService.CancelPayrollRunAsync(id, userId, branchId, cancellationToken);

    public Task<AccountingCommandResult> ReconcilePayrollRunCommissionsAsync(long id, long userId, long? branchId, CancellationToken cancellationToken = default) =>
        _payrollCommandService.ReconcilePayrollRunCommissionsAsync(id, userId, branchId, cancellationToken);

    public Task<AccountingCommandResult> CalculatePayrollRunAsync(long id, PayrollRunCalculateRequest? request, long userId, long? branchId, CancellationToken cancellationToken = default) =>
        _payrollCommandService.CalculatePayrollRunAsync(id, request, userId, branchId, cancellationToken);

    public Task<AccountingCommandResult> CalculatePayrollForAllAsync(PayrollBatchCalculateRequest? request, long userId, long? branchId, CancellationToken cancellationToken = default) =>
        _payrollCommandService.CalculatePayrollForAllAsync(request, userId, branchId, cancellationToken);

    public Task<AccountingCommandResult> IssuePayslipAsync(IssuePayslipCommand command, long userId, long? branchId, CancellationToken cancellationToken = default) =>
        _payrollCommandService.IssuePayslipAsync(command, userId, branchId, cancellationToken);

    public Task<AccountingCommandResult> CreatePayrollPeriodAsync(CreatePayrollPeriodCommand command, long? branchId) =>
        _payrollCommandService.CreatePayrollPeriodAsync(command, branchId);

    public Task<AccountingCommandResult> GetTrialBalanceAsync(long? branchId) =>
        _financeWorkflowService.GetTrialBalanceAsync(branchId);

    public Task<AccountingCommandResult> GetSupplierPayableSummaryAsync(long? branchId) =>
        _financeWorkflowService.GetSupplierPayableSummaryAsync(branchId);

    public Task<AccountingCommandResult> GetPayrollRunSummaryAsync(long? branchId) =>
        _financeWorkflowService.GetPayrollRunSummaryAsync(branchId);

    public Task<AccountingCommandResult> GetPayrollRunCommissionReconciliationAsync(long id, long? branchId, CancellationToken cancellationToken = default) =>
        _payrollCommandService.GetPayrollRunCommissionReconciliationAsync(id, branchId, cancellationToken);

    public Task<AccountingCommandResult> GetStaffBaseSalaryAsync(long staffId, DateTime? asOfDate, long? branchId, CancellationToken cancellationToken = default) =>
        _payrollCommandService.GetStaffBaseSalaryAsync(staffId, asOfDate, branchId, cancellationToken);

    public Task<AccountingCommandResult> GetStaffSalaryCoefficientAsync(long staffId, DateTime? asOfDate, long? branchId, CancellationToken cancellationToken = default) =>
        _payrollCommandService.GetStaffSalaryCoefficientAsync(staffId, asOfDate, branchId, cancellationToken);

    public Task<AccountingCommandResult> RequestInitialSalaryAsync(long staffId, InitialSalaryCommand command, long userId, long? branchId, CancellationToken cancellationToken = default) =>
        _payrollCommandService.RequestInitialSalaryAsync(staffId, command, userId, branchId, cancellationToken);

    public Task<AccountingCommandResult> GetDashboardAsync(DateTime? fromDate, DateTime? toDate, long? branchId) =>
        _financeWorkflowService.GetDashboardAsync(fromDate, toDate, branchId);

    public Task<AccountingCommandResult> GetDashboardV2Async(DateTime? fromDate, DateTime? toDate, int? historyLimit, long? branchId) =>
        _financeWorkflowService.GetDashboardV2Async(fromDate, toDate, historyLimit, branchId);

    public Task<AccountingFileResult> ExportDashboardV2CsvAsync(DateTime? fromDate, DateTime? toDate, int? historyLimit, long? branchId) =>
        _financeWorkflowService.ExportDashboardV2CsvAsync(fromDate, toDate, historyLimit, branchId);
}
