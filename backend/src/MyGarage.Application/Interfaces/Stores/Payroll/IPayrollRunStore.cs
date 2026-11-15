using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Payroll.DTOs;
using MyGarage.Models;

namespace MyGarage.Application.Payroll;

public interface IPayrollRunStore
{
    Task<IReadOnlyList<PayrollRun>> GetPayrollRunsPageAsync(
        long? branchId,
        string? normalizedPeriod,
        string? normalizedStatus,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<PayrollRun?> GetPayrollRunByIdAsync(long id, bool track = false, CancellationToken ct = default);

    Task<PayrollPeriod?> GetPayrollPeriodByCodeAsync(string periodCode, CancellationToken ct = default);

    Task AddPayrollPeriodAsync(PayrollPeriod period, CancellationToken ct = default);

    Task<int> GetMaxRunNoAsync(long payrollPeriodId, long branchId, CancellationToken ct = default);

    Task AddPayrollRunAsync(PayrollRun run, CancellationToken ct = default);

    Task<IReadOnlyList<PayslipDto>> GetMyPayslipsAsync(long staffId, string? normalizedPeriod, CancellationToken ct = default);

    Task<long?> GetPayrollRunBranchIdAsync(long payrollRunId, CancellationToken ct = default);

    Task<PayrollPeriod?> GetPayrollPeriodByIdAsync(long id, CancellationToken ct = default);

    Task<PayrollPeriod?> GetLatestOpenPayrollPeriodAsync(CancellationToken ct = default);

    Task<PayrollRun?> GetLatestDraftPayrollRunAsync(long payrollPeriodId, long? branchId, CancellationToken ct = default);

    Task<int> GetMaxRunNoForPeriodAsync(long payrollPeriodId, long? branchId, CancellationToken ct = default);

    Task<List<PayrollItem>> GetPayrollItemsByRunIdAsync(long payrollRunId, CancellationToken ct = default);

    void AddPayrollItem(PayrollItem item);

    Task<HashSet<long>> GetExistingPayslipItemIdsAsync(IReadOnlyCollection<long> payrollItemIds, CancellationToken ct = default);

    Task AddPayslipsAsync(IEnumerable<Payslip> payslips, CancellationToken ct = default);
}
