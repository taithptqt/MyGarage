using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Accounting;

public interface IAccountingPayrollCommandService
{
    Task<AccountingCommandResult> ApprovePayrollAsync(long id, long userId, long? branchId, CancellationToken cancellationToken = default);
    Task<AccountingCommandResult> MarkPayrollPaidAsync(long id, long? branchId);
    Task<AccountingCommandResult> CancelPayrollRunAsync(long id, long userId, long? branchId, CancellationToken cancellationToken = default);
    Task<AccountingCommandResult> ReconcilePayrollRunCommissionsAsync(long id, long userId, long? branchId, CancellationToken cancellationToken = default);
    Task<AccountingCommandResult> CalculatePayrollRunAsync(long id, PayrollRunCalculateRequest? request, long userId, long? branchId, CancellationToken cancellationToken = default);
    Task<AccountingCommandResult> CalculatePayrollForAllAsync(PayrollBatchCalculateRequest? request, long userId, long? branchId, CancellationToken cancellationToken = default);
    Task<AccountingCommandResult> IssuePayslipAsync(IssuePayslipCommand command, long userId, long? branchId, CancellationToken cancellationToken = default);
    Task<AccountingCommandResult> CreatePayrollPeriodAsync(CreatePayrollPeriodCommand command, long? branchId);

    Task<AccountingCommandResult> GetPayrollRunCommissionReconciliationAsync(long id, long? branchId, CancellationToken cancellationToken = default);
    Task<AccountingCommandResult> GetStaffBaseSalaryAsync(long staffId, DateTime? asOfDate, long? branchId, CancellationToken cancellationToken = default);
    Task<AccountingCommandResult> GetStaffSalaryCoefficientAsync(long staffId, DateTime? asOfDate, long? branchId, CancellationToken cancellationToken = default);
    Task<AccountingCommandResult> RequestInitialSalaryAsync(long staffId, InitialSalaryCommand command, long userId, long? branchId, CancellationToken cancellationToken = default);
}
