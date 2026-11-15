using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public interface IAccountingDataService
{
    Task<(bool Found, string? ErrorMessage, long PayrollPeriodId)> CancelPayrollRunAsync(
        long id,
        long? accessBranchId,
        long actorUserId,
        CancellationToken ct = default);

    Task<(bool Found, string? Status, int RunNo)> GetPayrollRunStatusAsync(
        long id,
        long? accessBranchId,
        CancellationToken ct = default);

    Task<PayrollItem?> GetPayrollItemWithRunAsync(long payrollItemId, CancellationToken ct = default);
    Task<Payslip?> GetPayslipByPayrollItemIdAsync(long payrollItemId, CancellationToken ct = default);
    Task<PayrollRun?> GetPayrollRunWithPeriodAsync(long payrollRunId, CancellationToken ct = default);
    Task<List<(long StaffId, decimal CommissionAmount)>> GetPayrollItemCommissionsAsync(
        long payrollRunId,
        CancellationToken ct = default);
    Task<List<CommissionTransaction>> GetPositiveCommissionTransactionsAsync(
        IReadOnlyCollection<long> staffIds,
        CancellationToken ct = default);
    Task<Staff?> GetActiveStaffAsync(long staffId, CancellationToken ct = default);
    Task<Dictionary<long, decimal>> LoadStaffTierBaseSalariesAsync(
        IReadOnlyCollection<long> staffIds,
        CancellationToken ct = default);
    Task<decimal?> GetSalaryCoefficientAsync(int yearsOfService, CancellationToken ct = default);
}
