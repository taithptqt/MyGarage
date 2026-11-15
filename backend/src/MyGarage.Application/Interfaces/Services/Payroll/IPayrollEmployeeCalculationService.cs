using MyGarage.Application.Payroll.DTOs;
using MyGarage.Models;

namespace MyGarage.Interfaces;

public interface IPayrollEmployeeCalculationService
{
    Task<PayrollEmployeeBatchCalculationOutput> CalculateAndApplyAsync(
        PayrollEmployeeBatchCalculationInput input,
        CancellationToken cancellationToken = default);
}

public sealed record PayrollEmployeeBatchCalculationInput(
    long PayrollPeriodId,
    long PayrollRunId,
    long? BranchId,
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    bool OverwriteExisting,
    DateTime Now,
    IReadOnlyList<StaffPayrollSnapshot> StaffRows);

public sealed record PayrollEmployeeBatchCalculationOutput(
    int Inserted,
    int Updated,
    int SkippedNoBaseSalary,
    int SkippedExisting,
    decimal GrossTotal,
    decimal NetTotal,
    decimal CommissionTotal,
    List<PayrollItem> AffectedItems,
    HashSet<long> ProcessedCommissionIds);
