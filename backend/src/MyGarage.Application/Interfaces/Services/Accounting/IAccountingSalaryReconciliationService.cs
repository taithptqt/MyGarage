using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Accounting;

public interface IAccountingSalaryReconciliationService
{
    Task<AccountingCommandResult> GetPayrollRunCommissionReconciliationAsync(
        long id,
        long? branchId,
        CancellationToken cancellationToken = default);

    Task<AccountingCommandResult> GetStaffBaseSalaryAsync(
        long staffId,
        DateTime? asOfDate,
        long? branchId,
        CancellationToken cancellationToken = default);

    Task<AccountingCommandResult> GetStaffSalaryCoefficientAsync(
        long staffId,
        DateTime? asOfDate,
        long? branchId,
        CancellationToken cancellationToken = default);

    Task<AccountingCommandResult> RequestInitialSalaryAsync(
        long staffId,
        InitialSalaryCommand command,
        long userId,
        long? branchId,
        CancellationToken cancellationToken = default);

    Task<PayrollRunCommissionReconciliationResponse> BuildPayrollRunCommissionReconciliationAsync(
        long payrollRunId,
        long? accessBranchId,
        CancellationToken cancellationToken = default);
}

public sealed class PayrollCommissionReconciliationDetail
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

public sealed class PayrollRunCommissionReconciliationRunInfo
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

public sealed class PayrollRunCommissionReconciliationSummary
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

public sealed class PayrollRunCommissionReconciliationResponse
{
    public PayrollRunCommissionReconciliationRunInfo PayrollRun { get; init; } = new();
    public PayrollRunCommissionReconciliationSummary Summary { get; init; } = new();
    public List<PayrollCommissionReconciliationDetail> Details { get; init; } = [];
}
