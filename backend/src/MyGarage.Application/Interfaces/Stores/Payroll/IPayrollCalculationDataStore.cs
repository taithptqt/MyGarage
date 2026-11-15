using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Payroll.DTOs;
using MyGarage.Models;

namespace MyGarage.Application.Payroll;

public interface IPayrollCalculationDataStore
{
    Task<IReadOnlyList<Staff>> GetActiveBranchStaffWithTierAsync(long branchId, CancellationToken ct = default);

    Task<IReadOnlyList<StaffContract>> GetActiveContractsForStaffAsync(
        IReadOnlyCollection<long> staffIds,
        CancellationToken ct = default);

    Task<HashSet<long>> GetValidBranchStaffIdsAsync(
        long branchId,
        IReadOnlyCollection<long> requestedStaffIds,
        CancellationToken ct = default);

    Task<IReadOnlyList<StaffPayrollSnapshot>> GetPayrollStaffSnapshotsAsync(long? branchId, CancellationToken ct = default);

    Task<IReadOnlyList<SalaryCoefficientThreshold>> GetActiveSalaryCoefficientThresholdsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<StaffSalaryProfile>> GetSalaryProfilesAsOfAsync(IReadOnlyCollection<long> staffIds, long? branchId, DateOnly asOfDate, CancellationToken ct = default);

    Task<IReadOnlyList<StaffContract>> GetContractsAsOfAsync(IReadOnlyCollection<long> staffIds, DateOnly asOfDate, CancellationToken ct = default);

    Task<Dictionary<long, decimal>> GetStaffTierBaseSalariesAsync(IReadOnlyCollection<long> staffIds, CancellationToken ct = default);

    Task<IReadOnlyList<OvertimeEntry>> GetOvertimeEntriesInRangeAsync(IReadOnlyCollection<long> staffIds, DateOnly periodStartDate, DateOnly periodEndDate, CancellationToken ct = default);

    Task<IReadOnlyList<AttendanceAdjustment>> GetAttendanceAdjustmentsInRangeAsync(IReadOnlyCollection<long> staffIds, DateOnly periodStartDate, DateOnly periodEndDate, CancellationToken ct = default);

    Task<IReadOnlyList<SalaryAdvance>> GetSalaryAdvancesForPeriodAsync(IReadOnlyCollection<long> staffIds, long payrollPeriodId, CancellationToken ct = default);

    Task<IReadOnlyList<EmployeeLoan>> GetActiveLoansForPeriodAsync(IReadOnlyCollection<long> staffIds, long payrollPeriodId, CancellationToken ct = default);

    Task<IReadOnlyList<PitProfile>> GetPitProfilesAsOfAsync(IReadOnlyCollection<long> staffIds, DateOnly asOfDate, CancellationToken ct = default);

    Task<IReadOnlyList<EmployeeDependent>> GetEmployeeDependentsAsOfAsync(IReadOnlyCollection<long> staffIds, DateOnly asOfDate, CancellationToken ct = default);

    Task<IReadOnlyList<CommissionTransaction>> GetPositiveCommissionTransactionsAsync(IReadOnlyCollection<long> staffIds, CancellationToken ct = default);

    Task<DateOnly?> GetStaffHireDateFallbackAsync(long staffId, CancellationToken ct = default);

    Task<List<CommissionTransaction>> GetCommissionTransactionsByIdsForUpdateAsync(IReadOnlyCollection<long> ids, CancellationToken ct = default);
}
