using MyGarage.Application.Payroll;
using MyGarage.Application.Payroll.DTOs;
using MyGarage.Interfaces;
using MyGarage.Models;
using static MyGarage.Services.PayrollBatchCalculationHelpers;

namespace MyGarage.Services;

public sealed class PayrollEmployeeCalculationService : IPayrollEmployeeCalculationService
{
    private readonly IPayrollStore _store;

    public PayrollEmployeeCalculationService(IPayrollStore store)
    {
        _store = store;
    }

    public async Task<PayrollEmployeeBatchCalculationOutput> CalculateAndApplyAsync(
        PayrollEmployeeBatchCalculationInput input,
        CancellationToken cancellationToken = default)
    {
        var staffIdSet = input.StaffRows.Select(s => s.StaffId).ToHashSet();
        var coefficientThresholds = await _store.GetActiveSalaryCoefficientThresholdsAsync(cancellationToken);

        var profileBaseSalaryMap = await LoadProfileBaseSalaryMapAsync(staffIdSet, input.BranchId, input.PeriodEndDate, cancellationToken);
        var contractCompensationMap = await LoadContractCompensationMapAsync(staffIdSet, input.PeriodEndDate, cancellationToken);
        var staffTierBaseSalaryMap = await _store.GetStaffTierBaseSalariesAsync(staffIdSet, cancellationToken);
        var overtimeAmountMap = await LoadOvertimeAmountMapAsync(staffIdSet, input.PeriodStartDate, input.PeriodEndDate, cancellationToken);
        var attendanceDeductionMap = await LoadAttendanceDeductionMapAsync(staffIdSet, input.PeriodStartDate, input.PeriodEndDate, cancellationToken);
        var salaryAdvanceMap = await LoadSalaryAdvanceDeductionMapAsync(staffIdSet, input.PayrollPeriodId, cancellationToken);
        var loanDeductionMap = await LoadLoanDeductionMapAsync(staffIdSet, input.PayrollPeriodId, cancellationToken);
        var dependentCountMap = await LoadDependentCountMapAsync(staffIdSet, input.PeriodEndDate, cancellationToken);
        var commissionAggregation = await LoadCommissionAggregationAsync(staffIdSet, input.PeriodStartDate, input.PeriodEndDate, cancellationToken);

        var existingItems = await _store.GetPayrollItemsByRunIdAsync(input.PayrollRunId, cancellationToken);
        var existingItemByStaffId = existingItems.ToDictionary(i => i.StaffId);

        var policy = ResolveVietnamPayrollPolicy(input.PeriodEndDate);
        var inserted = 0;
        var updated = 0;
        var skippedNoBaseSalary = 0;
        var skippedExisting = 0;
        var grossTotal = 0m;
        var netTotal = 0m;
        var commissionTotal = 0m;
        var affectedItems = new List<PayrollItem>(input.StaffRows.Count);
        var processedCommissionIds = new HashSet<long>();

        foreach (var staff in input.StaffRows)
        {
            var baseSalary = ResolveBaseSalary(
                staff.StaffId,
                profileBaseSalaryMap,
                contractCompensationMap,
                staffTierBaseSalaryMap);
            if (baseSalary <= 0m)
            {
                skippedNoBaseSalary++;
                continue;
            }

            var hireDate = staff.HireDate ?? await _store.GetStaffHireDateFallbackAsync(staff.StaffId, cancellationToken);
            var yearsOfService = hireDate.HasValue && input.PeriodEndDate >= hireDate.Value
                ? CalculateCompletedYears(hireDate.Value, input.PeriodEndDate)
                : 0;

            var coefficient = ResolveSalaryCoefficientForYears(yearsOfService, coefficientThresholds);
            var effectiveBaseSalary = RoundVnd(baseSalary * coefficient);
            var allowanceAmount = contractCompensationMap.TryGetValue(staff.StaffId, out var comp)
                ? RoundVnd(comp.AllowanceAmount)
                : 0m;
            var overtimeAmount = overtimeAmountMap.TryGetValue(staff.StaffId, out var overtime)
                ? RoundVnd(overtime)
                : 0m;
            var attendanceDeduction = attendanceDeductionMap.TryGetValue(staff.StaffId, out var attendance)
                ? RoundVnd(attendance)
                : 0m;
            var advanceDeduction = salaryAdvanceMap.TryGetValue(staff.StaffId, out var advance)
                ? RoundVnd(advance)
                : 0m;
            var plannedLoanDeduction = loanDeductionMap.TryGetValue(staff.StaffId, out var loan)
                ? RoundVnd(loan)
                : 0m;
            var dependentCount = dependentCountMap.TryGetValue(staff.StaffId, out var deps)
                ? Math.Max(0, deps)
                : 0;

            var commissionAmount = commissionAggregation.AmountByStaffId.TryGetValue(staff.StaffId, out var commission)
                ? RoundVnd(commission)
                : 0m;
            const decimal bonusAmount = 0m;
            const decimal penaltyAmount = 0m;
            const decimal otherDeduction = 0m;
            const string insuranceZone = "I";

            var grossPay = RoundVnd(effectiveBaseSalary + overtimeAmount + allowanceAmount + commissionAmount + bonusAmount);
            var statutory = CalculateVietnamStatutoryAmounts(effectiveBaseSalary, grossPay, dependentCount, policy, insuranceZone);
            var insuranceEmployeeAmount = statutory.EmployeeInsuranceTotal;
            var insuranceCompanyAmount = statutory.EmployerInsuranceTotal;
            var pitAmount = statutory.PitAmount;

            var nonLoanDeductions = RoundVnd(
                attendanceDeduction +
                penaltyAmount +
                advanceDeduction +
                insuranceEmployeeAmount +
                pitAmount +
                otherDeduction);

            var remainingForLoan = RoundVnd(Math.Max(0m, grossPay - nonLoanDeductions));
            var appliedLoanDeduction = RoundVnd(Math.Min(plannedLoanDeduction, remainingForLoan));
            var totalDeduction = RoundVnd(nonLoanDeductions + appliedLoanDeduction);
            var netPay = RoundVnd(Math.Max(0m, grossPay - totalDeduction));

            PayrollItem item;
            if (existingItemByStaffId.TryGetValue(staff.StaffId, out var existingItem))
            {
                if (!input.OverwriteExisting)
                {
                    skippedExisting++;
                    continue;
                }

                item = existingItem;
                ApplyCalculatedAmounts(
                    item,
                    effectiveBaseSalary,
                    attendanceDeduction,
                    overtimeAmount,
                    allowanceAmount,
                    commissionAmount,
                    advanceDeduction,
                    appliedLoanDeduction,
                    insuranceEmployeeAmount,
                    insuranceCompanyAmount,
                    pitAmount,
                    otherDeduction,
                    netPay,
                    input.Now);
                updated++;
            }
            else
            {
                item = new PayrollItem
                {
                    PayrollRunId = input.PayrollRunId,
                    StaffId = staff.StaffId,
                    CreatedAt = input.Now
                };
                ApplyCalculatedAmounts(
                    item,
                    effectiveBaseSalary,
                    attendanceDeduction,
                    overtimeAmount,
                    allowanceAmount,
                    commissionAmount,
                    advanceDeduction,
                    appliedLoanDeduction,
                    insuranceEmployeeAmount,
                    insuranceCompanyAmount,
                    pitAmount,
                    otherDeduction,
                    netPay,
                    input.Now);
                _store.AddPayrollItem(item);
                existingItemByStaffId[staff.StaffId] = item;
                inserted++;
            }

            grossTotal += grossPay;
            netTotal += netPay;
            commissionTotal += commissionAmount;
            if (commissionAggregation.TransactionIdsByStaffId.TryGetValue(staff.StaffId, out var commissionIds))
            {
                foreach (var commissionId in commissionIds)
                {
                    processedCommissionIds.Add(commissionId);
                }
            }
            affectedItems.Add(item);
        }

        return new PayrollEmployeeBatchCalculationOutput(
            Inserted: inserted,
            Updated: updated,
            SkippedNoBaseSalary: skippedNoBaseSalary,
            SkippedExisting: skippedExisting,
            GrossTotal: RoundVnd(grossTotal),
            NetTotal: RoundVnd(netTotal),
            CommissionTotal: RoundVnd(commissionTotal),
            AffectedItems: affectedItems,
            ProcessedCommissionIds: processedCommissionIds);
    }

    private static void ApplyCalculatedAmounts(
        PayrollItem item,
        decimal effectiveBaseSalary,
        decimal attendanceDeduction,
        decimal overtimeAmount,
        decimal allowanceAmount,
        decimal commissionAmount,
        decimal advanceDeduction,
        decimal appliedLoanDeduction,
        decimal insuranceEmployeeAmount,
        decimal insuranceCompanyAmount,
        decimal pitAmount,
        decimal otherDeduction,
        decimal netPay,
        DateTime now)
    {
        item.BaseSalary = effectiveBaseSalary;
        item.AttendanceDeduction = attendanceDeduction;
        item.OvertimeAmount = overtimeAmount;
        item.AllowanceAmount = allowanceAmount;
        item.CommissionAmount = commissionAmount;
        item.BonusAmount = 0m;
        item.PenaltyAmount = 0m;
        item.AdvanceDeduction = advanceDeduction;
        item.LoanDeduction = appliedLoanDeduction;
        item.InsuranceEmployeeAmount = insuranceEmployeeAmount;
        item.InsuranceCompanyAmount = insuranceCompanyAmount;
        item.PitAmount = pitAmount;
        item.OtherDeduction = otherDeduction;
        item.NetPay = netPay;
        item.Status = PayrollItemStatusDraft;
        item.UpdatedAt = now;
    }

    private async Task<Dictionary<long, decimal>> LoadProfileBaseSalaryMapAsync(
        IReadOnlyCollection<long> staffIds,
        long? branchId,
        DateOnly asOfDate,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<long, decimal>();
        if (staffIds.Count == 0) return result;

        var rows = await _store.GetSalaryProfilesAsOfAsync(staffIds, branchId, asOfDate, cancellationToken);

        foreach (var row in rows)
        {
            if (result.ContainsKey(row.StaffId)) continue;
            if (!string.Equals(NormalizeStatus(row.Status, StaffSalaryProfileStatusActive), StaffSalaryProfileStatusActive, StringComparison.Ordinal)) continue;
            if (row.BaseSalary <= 0m) continue;
            result[row.StaffId] = row.BaseSalary;
        }

        return result;
    }

    private async Task<Dictionary<long, ContractCompensationSnapshot>> LoadContractCompensationMapAsync(
        IReadOnlyCollection<long> staffIds,
        DateOnly asOfDate,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<long, ContractCompensationSnapshot>();
        if (staffIds.Count == 0) return result;

        var rows = await _store.GetContractsAsOfAsync(staffIds, asOfDate, cancellationToken);

        foreach (var row in rows)
        {
            if (result.ContainsKey(row.StaffId)) continue;
            if (!string.Equals(NormalizeStatus(row.Status, StaffContractStatusActive), StaffContractStatusActive, StringComparison.Ordinal)) continue;
            result[row.StaffId] = new ContractCompensationSnapshot(
                BaseSalary: Math.Max(0m, row.BaseSalary),
                AllowanceAmount: Math.Max(0m, row.AllowanceAmount));
        }

        return result;
    }

    private async Task<Dictionary<long, decimal>> LoadOvertimeAmountMapAsync(
        IReadOnlyCollection<long> staffIds,
        DateOnly periodStartDate,
        DateOnly periodEndDate,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<long, decimal>();
        if (staffIds.Count == 0) return result;

        var rows = await _store.GetOvertimeEntriesInRangeAsync(staffIds, periodStartDate, periodEndDate, cancellationToken);

        foreach (var row in rows)
        {
            if (!IsAdjustmentStatusAccepted(row.Status)) continue;
            var amount = Math.Max(0m, row.Amount);
            if (!result.TryAdd(row.StaffId, amount))
            {
                result[row.StaffId] += amount;
            }
        }

        return result;
    }

    private async Task<Dictionary<long, decimal>> LoadAttendanceDeductionMapAsync(
        IReadOnlyCollection<long> staffIds,
        DateOnly periodStartDate,
        DateOnly periodEndDate,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<long, decimal>();
        if (staffIds.Count == 0) return result;

        var rows = await _store.GetAttendanceAdjustmentsInRangeAsync(staffIds, periodStartDate, periodEndDate, cancellationToken);

        foreach (var row in rows)
        {
            if (!IsAdjustmentStatusAccepted(row.Status)) continue;

            var normalizedType = NormalizeKey(row.AdjustmentType);
            var impact = row.AmountImpact;
            decimal deduction;

            if (impact < 0m)
            {
                deduction = Math.Abs(impact);
            }
            else if (impact > 0m && AttendanceDeductionTypes.Contains(normalizedType))
            {
                deduction = impact;
            }
            else
            {
                deduction = 0m;
            }

            if (deduction <= 0m) continue;
            if (!result.TryAdd(row.StaffId, deduction))
            {
                result[row.StaffId] += deduction;
            }
        }

        return result;
    }

    private async Task<Dictionary<long, decimal>> LoadSalaryAdvanceDeductionMapAsync(
        IReadOnlyCollection<long> staffIds,
        long payrollPeriodId,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<long, decimal>();
        if (staffIds.Count == 0) return result;

        var rows = await _store.GetSalaryAdvancesForPeriodAsync(staffIds, payrollPeriodId, cancellationToken);

        foreach (var row in rows)
        {
            var normalizedStatus = NormalizeKey(row.Status);
            if (!string.IsNullOrWhiteSpace(normalizedStatus) && !AcceptedAdvanceStatuses.Contains(normalizedStatus)) continue;
            var amount = Math.Max(0m, row.Amount);
            if (amount <= 0m) continue;
            if (!result.TryAdd(row.StaffId, amount))
            {
                result[row.StaffId] += amount;
            }
        }

        return result;
    }

    private async Task<Dictionary<long, decimal>> LoadLoanDeductionMapAsync(
        IReadOnlyCollection<long> staffIds,
        long payrollPeriodId,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<long, decimal>();
        if (staffIds.Count == 0) return result;

        var rows = await _store.GetActiveLoansForPeriodAsync(staffIds, payrollPeriodId, cancellationToken);

        foreach (var row in rows)
        {
            if (!AcceptedLoanStatuses.Contains(NormalizeStatus(row.Status, EmployeeLoanStatusActive))) continue;
            var plannedDeduction = row.MonthlyDeduction > 0m
                ? Math.Min(row.MonthlyDeduction, row.OutstandingAmount)
                : row.OutstandingAmount;
            plannedDeduction = Math.Max(0m, plannedDeduction);

            if (plannedDeduction <= 0m) continue;
            if (!result.TryAdd(row.StaffId, plannedDeduction))
            {
                result[row.StaffId] += plannedDeduction;
            }
        }

        return result;
    }

    private async Task<Dictionary<long, int>> LoadDependentCountMapAsync(
        IReadOnlyCollection<long> staffIds,
        DateOnly asOfDate,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<long, int>();
        if (staffIds.Count == 0) return result;

        var pitRows = await _store.GetPitProfilesAsOfAsync(staffIds, asOfDate, cancellationToken);

        foreach (var row in pitRows)
        {
            if (result.ContainsKey(row.StaffId)) continue;
            result[row.StaffId] = Math.Max(0, row.DependentCount);
        }

        var missingStaffIds = staffIds.Where(id => !result.ContainsKey(id)).ToHashSet();
        if (missingStaffIds.Count == 0) return result;

        var dependentRows = await _store.GetEmployeeDependentsAsOfAsync(missingStaffIds, asOfDate, cancellationToken);

        foreach (var group in dependentRows.GroupBy(x => x.StaffId))
        {
            result[group.Key] = group.Count();
        }

        return result;
    }

    private async Task<CommissionAggregationSnapshot> LoadCommissionAggregationAsync(
        IReadOnlyCollection<long> staffIds,
        DateOnly periodStartDate,
        DateOnly periodEndDate,
        CancellationToken cancellationToken)
    {
        var amountByStaffId = new Dictionary<long, decimal>();
        var transactionIdsByStaffId = new Dictionary<long, List<long>>();
        if (staffIds.Count == 0)
        {
            return new CommissionAggregationSnapshot(amountByStaffId, transactionIdsByStaffId);
        }

        var periodStart = periodStartDate.ToDateTime(TimeOnly.MinValue);
        var periodEndExclusive = periodEndDate.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var rows = await _store.GetPositiveCommissionTransactionsAsync(staffIds, cancellationToken);

        foreach (var row in rows)
        {
            var status = NormalizeStatus(row.Status, CommissionStatusPending);
            if (status != CommissionStatusPending && status != CommissionStatusApproved)
            {
                continue;
            }

            var effectiveAt = row.ApprovedAt ?? row.CreatedAt ?? row.UpdatedAt;
            if (!effectiveAt.HasValue)
            {
                continue;
            }

            if (effectiveAt.Value < periodStart || effectiveAt.Value >= periodEndExclusive)
            {
                continue;
            }

            var amount = Math.Max(0m, row.CommissionAmount);
            if (amount <= 0m)
            {
                continue;
            }

            if (!amountByStaffId.TryAdd(row.StaffId, amount))
            {
                amountByStaffId[row.StaffId] += amount;
            }

            if (!transactionIdsByStaffId.TryGetValue(row.StaffId, out var ids))
            {
                ids = [];
                transactionIdsByStaffId[row.StaffId] = ids;
            }
            ids.Add(row.Id);
        }

        return new CommissionAggregationSnapshot(amountByStaffId, transactionIdsByStaffId);
    }
}
