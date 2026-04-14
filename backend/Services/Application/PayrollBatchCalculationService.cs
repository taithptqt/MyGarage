using Microsoft.EntityFrameworkCore;
using System.Linq;
using MyGarage.Data;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class PayrollBatchCalculationService : IPayrollBatchCalculationService
{
    private const string PayrollPeriodStatusOpen = nameof(PayrollPeriodStatusCode.OPEN);
    private const string PayrollRunStatusDraft = nameof(PayrollRunStatusCode.DRAFT);
    private const string PayrollItemStatusDraft = nameof(PayrollItemStatusCode.DRAFT);
    private const string StaffSalaryProfileStatusActive = nameof(StaffSalaryProfileStatusCode.ACTIVE);
    private const string StaffContractStatusActive = nameof(StaffContractStatusCode.ACTIVE);
    private const string EmployeeLoanStatusActive = nameof(EmployeeLoanStatusCode.ACTIVE);
    private const string StaffStatusActive = nameof(StaffStatusCode.ACTIVE);
    private const string StaffStatusOnLeave = nameof(StaffStatusCode.ON_LEAVE);
    private readonly IGarageContext _db;

    public PayrollBatchCalculationService(IGarageContext db)
    {
        _db = db;
    }

    private static readonly IReadOnlyDictionary<string, decimal> VietnamRegionalMinimumWageByZone =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["I"] = 4_960_000m,
            ["II"] = 4_410_000m,
            ["III"] = 3_860_000m,
            ["IV"] = 3_450_000m
        };

    private static readonly VietnamPayrollPolicy VietnamPayrollPolicyPre2026 = new(
        EmployeeBhxhRate: 0.08m,
        EmployeeBhytRate: 0.015m,
        EmployeeBhtnRate: 0.01m,
        EmployerBhxhRate: 0.175m,
        EmployerBhytRate: 0.03m,
        EmployerBhtnRate: 0.01m,
        EmployerOaidRate: 0.005m,
        PersonalDeduction: 11_000_000m,
        DependentDeduction: 4_400_000m,
        SocialInsuranceBaseReference: 2_340_000m,
        RegionalMinimumWageByZone: VietnamRegionalMinimumWageByZone);

    private static readonly VietnamPayrollPolicy VietnamPayrollPolicy2026 = VietnamPayrollPolicyPre2026 with
    {
        PersonalDeduction = 15_500_000m,
        DependentDeduction = 6_200_000m
    };

    private static readonly IReadOnlyList<PitBracket> VietnamPitBrackets =
    [
        new PitBracket(5_000_000m, 0.05m),
        new PitBracket(10_000_000m, 0.10m),
        new PitBracket(18_000_000m, 0.15m),
        new PitBracket(32_000_000m, 0.20m),
        new PitBracket(52_000_000m, 0.25m),
        new PitBracket(80_000_000m, 0.30m),
        new PitBracket(decimal.MaxValue, 0.35m)
    ];

    private static readonly HashSet<string> AcceptedApprovalStatuses =
    [
        nameof(AttendanceAdjustmentStatusCode.APPROVED),
        nameof(AttendanceAdjustmentStatusCode.APPLIED),
        nameof(OvertimeEntryStatusCode.PAID),
        nameof(JournalEntryStatusCode.POSTED),
        nameof(PaymentStatusCode.COMPLETED)
    ];

    private static readonly HashSet<string> AttendanceDeductionTypes =
    [
        "DEDUCTION",
        "ABSENT",
        "LATE",
        "UNPAID_LEAVE",
        "EARLY_LEAVE",
        "MISSED_CHECKIN"
    ];

    private static readonly HashSet<string> BonusAdjustmentTypes =
    [
        "BONUS",
        "INCENTIVE",
        "REWARD"
    ];

    private static readonly HashSet<string> PenaltyAdjustmentTypes =
    [
        "PENALTY",
        "DISCIPLINE",
        "FINE"
    ];

    private static readonly HashSet<string> OtherDeductionTypes =
    [
        "OTHER_DEDUCTION",
        "DEDUCTION_OTHER",
        "MISC_DEDUCTION",
        "WITHHOLDING"
    ];

    private static readonly HashSet<string> AcceptedCommissionStatuses =
    [
        nameof(CommissionTransactionStatusCode.APPROVED),
        nameof(CommissionTransactionStatusCode.PAID),
        "POSTED",
        nameof(PaymentStatusCode.COMPLETED)
    ];

    private static readonly HashSet<string> AcceptedAdvanceStatuses =
    [
        nameof(SalaryAdvanceStatusCode.APPROVED),
        nameof(SalaryAdvanceStatusCode.DEDUCTING)
    ];

    private static readonly HashSet<string> AcceptedLoanStatuses =
    [
        nameof(EmployeeLoanStatusCode.ACTIVE)
    ];

    private static readonly HashSet<string> ActiveStaffStatuses =
    [
        StaffStatusActive,
        StaffStatusOnLeave
    ];

    public async Task<PayrollBatchCalculationResult> CalculatePayrollForAllAsync(
        PayrollBatchCalculationCommand command,
        CancellationToken cancellationToken = default)
    {
        var now = VietnamTime.Now;
        var period = await ResolvePayrollPeriodAsync(command, cancellationToken);

        if (!string.Equals(NormalizeKey(period.Status), PayrollPeriodStatusOpen, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Batch payroll calculation only supports OPEN payroll period.");
        }

        var periodStartDate = period.StartDate;
        var periodEndDate = period.EndDate != default ? period.EndDate : period.PayrollDate;
        if (periodEndDate < periodStartDate)
        {
            throw new InvalidOperationException("Payroll period date range is invalid.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var runResolution = await ResolveOrCreateDraftPayrollRunAsync(
                period.Id,
                command.BranchId,
                command.UserId,
                command.PayrollRunId,
                cancellationToken);

            var staffRows = await LoadStaffRowsAsync(command.BranchId, cancellationToken);
            if (!command.IncludeInactiveStaff)
            {
                staffRows = staffRows
                    .Where(s => IsPayrollActiveStaff(s, periodEndDate))
                    .ToList();
            }

            if (staffRows.Count == 0)
            {
                runResolution.Run.CalculatedBy = command.UserId;
                runResolution.Run.CalculatedAt = now;
                runResolution.Run.UpdatedAt = now;
                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                return new PayrollBatchCalculationResult
                {
                    PayrollPeriodId = period.Id,
                    PayrollRunId = runResolution.Run.Id,
                    RunNo = runResolution.Run.RunNo,
                    CreatedRun = runResolution.CreatedNew,
                    PeriodStartDate = periodStartDate.ToString("yyyy-MM-dd"),
                    PeriodEndDate = periodEndDate.ToString("yyyy-MM-dd"),
                    ProcessedStaffCount = 0,
                    CalculatedStaffCount = 0,
                    Inserted = 0,
                    Updated = 0,
                    SkippedNoBaseSalary = 0,
                    SkippedExisting = 0,
                    IssuePayslips = command.IssuePayslips,
                    PayslipsIssued = 0,
                    GrossTotal = 0m,
                    NetTotal = 0m,
                    Message = "Batch payroll calculation completed."
                };
            }

            var staffIdSet = staffRows.Select(s => s.StaffId).ToHashSet();
            var coefficientThresholds = await _db.SalaryCoefficients
                .AsNoTracking()
                .Where(x => x.IsDeleted != true && x.IsActive != false && x.YearsOfService.HasValue)
                .OrderBy(x => x.YearsOfService)
                .Select(x => new SalaryCoefficientThreshold(x.YearsOfService!.Value, x.CoefficientValue))
                .ToListAsync(cancellationToken);

            var profileBaseSalaryMap = await LoadProfileBaseSalaryMapAsync(staffIdSet, command.BranchId, periodEndDate, cancellationToken);
            var contractCompensationMap = await LoadContractCompensationMapAsync(staffIdSet, periodEndDate, cancellationToken);
            var staffTierBaseSalaryMap = await LoadStaffTierBaseSalaryMapAsync(staffIdSet, cancellationToken);
            var overtimeAmountMap = await LoadOvertimeAmountMapAsync(staffIdSet, periodStartDate, periodEndDate, cancellationToken);
            var attendanceDeductionMap = await LoadAttendanceDeductionMapAsync(staffIdSet, periodStartDate, periodEndDate, cancellationToken);
            var commissionAmountMap = await LoadCommissionAmountMapAsync(staffIdSet, periodStartDate, periodEndDate, cancellationToken);
            var bonusAmountMap = await LoadBonusAmountMapAsync(staffIdSet, periodStartDate, periodEndDate, cancellationToken);
            var penaltyAmountMap = await LoadPenaltyAmountMapAsync(staffIdSet, periodStartDate, periodEndDate, cancellationToken);
            var otherDeductionMap = await LoadOtherDeductionAmountMapAsync(staffIdSet, periodStartDate, periodEndDate, cancellationToken);
            var salaryAdvanceMap = await LoadSalaryAdvanceDeductionMapAsync(staffIdSet, period.Id, cancellationToken);
            var loanDeductionMap = await LoadLoanDeductionMapAsync(staffIdSet, period.Id, cancellationToken);
            var dependentCountMap = await LoadDependentCountMapAsync(staffIdSet, periodEndDate, cancellationToken);

            var existingItems = await _db.PayrollItems
                .Where(i => i.PayrollRunId == runResolution.Run.Id)
                .ToListAsync(cancellationToken);
            var existingItemByStaffId = existingItems.ToDictionary(i => i.StaffId);

            var policy = ResolveVietnamPayrollPolicy(periodEndDate);
            var inserted = 0;
            var updated = 0;
            var skippedNoBaseSalary = 0;
            var skippedExisting = 0;
            var grossTotal = 0m;
            var netTotal = 0m;
            var affectedItems = new List<PayrollItem>(staffRows.Count);

            foreach (var staff in staffRows)
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

                var hireDate = staff.HireDate ?? await ResolveStaffHireDateAsync(staff.StaffId, cancellationToken);
                var yearsOfService = hireDate.HasValue && periodEndDate >= hireDate.Value
                    ? CalculateCompletedYears(hireDate.Value, periodEndDate)
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
                var commissionAmount = commissionAmountMap.TryGetValue(staff.StaffId, out var commission)
                    ? RoundVnd(commission)
                    : 0m;
                var bonusAmount = bonusAmountMap.TryGetValue(staff.StaffId, out var bonus)
                    ? RoundVnd(bonus)
                    : 0m;
                var penaltyAmount = penaltyAmountMap.TryGetValue(staff.StaffId, out var penalty)
                    ? RoundVnd(penalty)
                    : 0m;
                var otherDeduction = otherDeductionMap.TryGetValue(staff.StaffId, out var otherDeductionAmount)
                    ? RoundVnd(otherDeductionAmount)
                    : 0m;
                var dependentCount = dependentCountMap.TryGetValue(staff.StaffId, out var deps)
                    ? Math.Max(0, deps)
                    : 0;

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
                    if (!command.OverwriteExisting)
                    {
                        skippedExisting++;
                        continue;
                    }

                    item = existingItem;
                    item.BaseSalary = effectiveBaseSalary;
                    item.AttendanceDeduction = attendanceDeduction;
                    item.OvertimeAmount = overtimeAmount;
                    item.AllowanceAmount = allowanceAmount;
                    item.CommissionAmount = commissionAmount;
                    item.BonusAmount = bonusAmount;
                    item.PenaltyAmount = penaltyAmount;
                    item.AdvanceDeduction = advanceDeduction;
                    item.LoanDeduction = appliedLoanDeduction;
                    item.InsuranceEmployeeAmount = insuranceEmployeeAmount;
                    item.InsuranceCompanyAmount = insuranceCompanyAmount;
                    item.PitAmount = pitAmount;
                    item.OtherDeduction = otherDeduction;
                    item.NetPay = netPay;
                    item.Status = PayrollItemStatusDraft;
                    item.UpdatedAt = now;
                    updated++;
                }
                else
                {
                    item = new PayrollItem
                    {
                        PayrollRunId = runResolution.Run.Id,
                        StaffId = staff.StaffId,
                        BaseSalary = effectiveBaseSalary,
                        AttendanceDeduction = attendanceDeduction,
                        OvertimeAmount = overtimeAmount,
                        AllowanceAmount = allowanceAmount,
                        CommissionAmount = commissionAmount,
                        BonusAmount = bonusAmount,
                        PenaltyAmount = penaltyAmount,
                        AdvanceDeduction = advanceDeduction,
                        LoanDeduction = appliedLoanDeduction,
                        InsuranceEmployeeAmount = insuranceEmployeeAmount,
                        InsuranceCompanyAmount = insuranceCompanyAmount,
                        PitAmount = pitAmount,
                        OtherDeduction = otherDeduction,
                        NetPay = netPay,
                        Status = PayrollItemStatusDraft,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _db.PayrollItems.Add(item);
                    existingItemByStaffId[staff.StaffId] = item;
                    inserted++;
                }

                grossTotal += grossPay;
                netTotal += netPay;
                affectedItems.Add(item);
            }

            runResolution.Run.CalculatedBy = command.UserId;
            runResolution.Run.CalculatedAt = now;
            runResolution.Run.UpdatedAt = now;

            await _db.SaveChangesAsync(cancellationToken);

            var payslipsIssued = 0;
            if (command.IssuePayslips)
            {
                payslipsIssued = await IssuePayslipsAsync(
                    runResolution.Run.RunNo,
                    now,
                    command.UserId,
                    affectedItems,
                    cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);

            return new PayrollBatchCalculationResult
            {
                PayrollPeriodId = period.Id,
                PayrollRunId = runResolution.Run.Id,
                RunNo = runResolution.Run.RunNo,
                CreatedRun = runResolution.CreatedNew,
                PeriodStartDate = periodStartDate.ToString("yyyy-MM-dd"),
                PeriodEndDate = periodEndDate.ToString("yyyy-MM-dd"),
                ProcessedStaffCount = staffRows.Count,
                CalculatedStaffCount = inserted + updated,
                Inserted = inserted,
                Updated = updated,
                SkippedNoBaseSalary = skippedNoBaseSalary,
                SkippedExisting = skippedExisting,
                IssuePayslips = command.IssuePayslips,
                PayslipsIssued = payslipsIssued,
                GrossTotal = RoundVnd(grossTotal),
                NetTotal = RoundVnd(netTotal),
                Message = "Batch payroll calculation completed."
            };
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<PayrollPeriod> ResolvePayrollPeriodAsync(PayrollBatchCalculationCommand command, CancellationToken cancellationToken)
    {
        if (command.PayrollPeriodId.HasValue && command.PayrollPeriodId.Value > 0)
        {
            var selected = await _db.PayrollPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == command.PayrollPeriodId.Value, cancellationToken);
            if (selected is null)
            {
                throw new KeyNotFoundException($"Payroll period #{command.PayrollPeriodId.Value} not found.");
            }

            return selected;
        }

        var openPeriod = await _db.PayrollPeriods
            .AsNoTracking()
            .Where(x => x.Status != null && x.Status.ToUpper() == PayrollPeriodStatusOpen)
            .OrderByDescending(x => x.StartDate)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (openPeriod is null)
        {
            throw new InvalidOperationException("No OPEN payroll period found. Please create/open one first.");
        }

        return openPeriod;
    }

    private async Task<PayrollRunResolution> ResolveOrCreateDraftPayrollRunAsync(
        long payrollPeriodId,
        long? branchId,
        long calculatedByUserId,
        long? requestedRunId,
        CancellationToken cancellationToken)
    {
        if (requestedRunId.HasValue && requestedRunId.Value > 0)
        {
            var provided = await _db.PayrollRuns
                .FirstOrDefaultAsync(x => x.Id == requestedRunId.Value, cancellationToken);
            if (provided is null || (branchId.HasValue && provided.BranchId != branchId.Value))
            {
                throw new KeyNotFoundException($"Payroll run #{requestedRunId.Value} not found.");
            }

            if (provided.PayrollPeriodId != payrollPeriodId)
            {
                throw new InvalidOperationException("Selected payroll run does not belong to the selected payroll period.");
            }

            if (!string.Equals(NormalizeKey(provided.Status), PayrollRunStatusDraft, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Batch calculation can only update a DRAFT payroll run.");
            }

            return new PayrollRunResolution(provided, false);
        }

        var draftQuery = _db.PayrollRuns
            .Where(x => x.PayrollPeriodId == payrollPeriodId && x.Status != null && x.Status.ToUpper() == PayrollRunStatusDraft);
        if (branchId.HasValue)
        {
            draftQuery = draftQuery.Where(x => x.BranchId == branchId.Value);
        }

        var existingDraft = await draftQuery
            .OrderByDescending(x => x.RunNo)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingDraft is not null)
        {
            return new PayrollRunResolution(existingDraft, false);
        }

        var runNoQuery = _db.PayrollRuns.Where(x => x.PayrollPeriodId == payrollPeriodId);
        if (branchId.HasValue)
        {
            runNoQuery = runNoQuery.Where(x => x.BranchId == branchId.Value);
        }

        var maxRunNo = await runNoQuery
            .Select(x => (int?)x.RunNo)
            .MaxAsync(cancellationToken);
        var nextRunNo = Math.Max(1, (maxRunNo ?? 0) + 1);

        var now = VietnamTime.Now;
        var run = new PayrollRun
        {
            PayrollPeriodId = payrollPeriodId,
            BranchId = branchId,
            RunNo = nextRunNo,
            Status = PayrollRunStatusDraft,
            CalculatedBy = calculatedByUserId,
            CalculatedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.PayrollRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);
        return new PayrollRunResolution(run, true);
    }

    private async Task<List<StaffPayrollSnapshot>> LoadStaffRowsAsync(long? branchId, CancellationToken cancellationToken)
    {
        var staffQuery = _db.Staffs
            .AsNoTracking()
            .Where(s => s.IsDeleted != true);
        if (branchId.HasValue)
        {
            staffQuery = staffQuery.Where(s => s.BranchId == branchId.Value);
        }

        return await staffQuery
            .Select(s => new StaffPayrollSnapshot
            {
                StaffId = s.Id,
                BranchId = s.BranchId,
                StaffName = s.FullName,
                HireDate = s.HireDate,
                TerminationDate = s.TerminationDate,
                Status = s.Status
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<Dictionary<long, decimal>> LoadProfileBaseSalaryMapAsync(
        IReadOnlyCollection<long> staffIds,
        long? branchId,
        DateOnly asOfDate,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<long, decimal>();
        if (staffIds.Count == 0) return result;

        var query = _db.StaffSalaryProfiles
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                x.EffectiveFrom <= asOfDate &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= asOfDate));
        if (branchId.HasValue)
        {
            query = query.Where(x => x.BranchId == branchId.Value || !x.BranchId.HasValue);
        }

        var rows = await query
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

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

        var rows = await _db.StaffContracts
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                x.EffectiveFrom <= asOfDate &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= asOfDate))
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

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

    private async Task<Dictionary<long, decimal>> LoadStaffTierBaseSalaryMapAsync(
        IReadOnlyCollection<long> staffIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<long, decimal>();
        if (staffIds.Count == 0) return result;

        var staffIdSet = staffIds.Where(id => id > 0).ToHashSet();
        if (staffIdSet.Count == 0) return result;

        var rows = await (
            from staff in _db.Staffs.AsNoTracking()
            join tier in _db.StaffTiers.AsNoTracking() on staff.StaffTierId equals tier.Id
            where staffIdSet.Contains(staff.Id)
                  && staff.IsDeleted != true
                  && tier.IsDeleted != true
                  && tier.BaseSalary.HasValue
                  && tier.BaseSalary.Value > 0m
            select new
            {
                staff.Id,
                BaseSalary = tier.BaseSalary!.Value
            }).ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            result[row.Id] = row.BaseSalary;
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

        var rows = await _db.OvertimeEntries
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                x.WorkDate >= periodStartDate &&
                x.WorkDate <= periodEndDate)
            .ToListAsync(cancellationToken);

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

        var rows = await _db.AttendanceAdjustments
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                x.WorkDate >= periodStartDate &&
                x.WorkDate <= periodEndDate)
            .ToListAsync(cancellationToken);

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

    private async Task<Dictionary<long, decimal>> LoadCommissionAmountMapAsync(
        IReadOnlyCollection<long> staffIds,
        DateOnly periodStartDate,
        DateOnly periodEndDate,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<long, decimal>();
        if (staffIds.Count == 0) return result;

        var periodStart = periodStartDate.ToDateTime(TimeOnly.MinValue);
        var periodEndExclusive = periodEndDate.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var rows = await _db.CommissionTransactions
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                x.CreatedAt.HasValue &&
                x.CreatedAt.Value >= periodStart &&
                x.CreatedAt.Value < periodEndExclusive)
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            var normalizedStatus = NormalizeKey(row.Status);
            if (!string.IsNullOrWhiteSpace(normalizedStatus) && !AcceptedCommissionStatuses.Contains(normalizedStatus))
            {
                continue;
            }

            var amount = Math.Max(0m, row.CommissionAmount);
            if (amount <= 0m)
            {
                continue;
            }

            var normalizedLogType = NormalizeKey(row.LogType);
            if (normalizedLogType.Contains("REVERSE", StringComparison.Ordinal) || normalizedLogType.Contains("REFUND", StringComparison.Ordinal))
            {
                amount = -amount;
            }

            if (!result.TryAdd(row.StaffId, amount))
            {
                result[row.StaffId] += amount;
            }
        }

        foreach (var key in result.Keys.ToList())
        {
            result[key] = Math.Max(0m, result[key]);
        }

        return result;
    }

    private Task<Dictionary<long, decimal>> LoadBonusAmountMapAsync(
        IReadOnlyCollection<long> staffIds,
        DateOnly periodStartDate,
        DateOnly periodEndDate,
        CancellationToken cancellationToken)
    {
        return LoadAdjustmentAmountMapAsync(staffIds, periodStartDate, periodEndDate, BonusAdjustmentTypes, cancellationToken);
    }

    private Task<Dictionary<long, decimal>> LoadPenaltyAmountMapAsync(
        IReadOnlyCollection<long> staffIds,
        DateOnly periodStartDate,
        DateOnly periodEndDate,
        CancellationToken cancellationToken)
    {
        return LoadAdjustmentAmountMapAsync(staffIds, periodStartDate, periodEndDate, PenaltyAdjustmentTypes, cancellationToken);
    }

    private Task<Dictionary<long, decimal>> LoadOtherDeductionAmountMapAsync(
        IReadOnlyCollection<long> staffIds,
        DateOnly periodStartDate,
        DateOnly periodEndDate,
        CancellationToken cancellationToken)
    {
        return LoadAdjustmentAmountMapAsync(staffIds, periodStartDate, periodEndDate, OtherDeductionTypes, cancellationToken);
    }

    private async Task<Dictionary<long, decimal>> LoadAdjustmentAmountMapAsync(
        IReadOnlyCollection<long> staffIds,
        DateOnly periodStartDate,
        DateOnly periodEndDate,
        IReadOnlySet<string> acceptedTypes,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<long, decimal>();
        if (staffIds.Count == 0 || acceptedTypes.Count == 0) return result;

        var rows = await _db.AttendanceAdjustments
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                x.WorkDate >= periodStartDate &&
                x.WorkDate <= periodEndDate)
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            if (!IsAdjustmentStatusAccepted(row.Status))
            {
                continue;
            }

            var normalizedType = NormalizeKey(row.AdjustmentType);
            if (!acceptedTypes.Contains(normalizedType))
            {
                continue;
            }

            var amount = Math.Abs(row.AmountImpact);
            if (amount <= 0m)
            {
                continue;
            }

            if (!result.TryAdd(row.StaffId, amount))
            {
                result[row.StaffId] += amount;
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

        var rows = await _db.SalaryAdvances
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                x.DeductFromPayrollPeriodId == payrollPeriodId)
            .ToListAsync(cancellationToken);

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

        var rows = await _db.EmployeeLoans
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                (!x.StartPayrollPeriodId.HasValue || x.StartPayrollPeriodId.Value <= payrollPeriodId) &&
                x.OutstandingAmount > 0m)
            .ToListAsync(cancellationToken);

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

        var pitRows = await _db.PitProfiles
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                x.EffectiveFrom <= asOfDate &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= asOfDate))
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var row in pitRows)
        {
            if (result.ContainsKey(row.StaffId)) continue;
            result[row.StaffId] = Math.Max(0, row.DependentCount);
        }

        var missingStaffIds = staffIds.Where(id => !result.ContainsKey(id)).ToHashSet();
        if (missingStaffIds.Count == 0) return result;

        var dependentRows = await _db.EmployeeDependents
            .AsNoTracking()
            .Where(x =>
                missingStaffIds.Contains(x.StaffId) &&
                x.EffectiveFrom <= asOfDate &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= asOfDate))
            .ToListAsync(cancellationToken);

        foreach (var group in dependentRows.GroupBy(x => x.StaffId))
        {
            result[group.Key] = group.Count();
        }

        return result;
    }

    private async Task<int> IssuePayslipsAsync(
        int runNo,
        DateTime now,
        long issuedBy,
        IReadOnlyCollection<PayrollItem> affectedItems,
        CancellationToken cancellationToken)
    {
        var affectedItemIds = affectedItems
            .Select(x => x.Id)
            .Where(x => x > 0)
            .Distinct()
            .ToList();
        if (affectedItemIds.Count == 0) return 0;

        var existingPayslipItemIds = await _db.Payslips
            .Where(p => affectedItemIds.Contains(p.PayrollItemId))
            .Select(p => p.PayrollItemId)
            .ToListAsync(cancellationToken);
        var existingSet = existingPayslipItemIds.ToHashSet();

        var created = 0;
        foreach (var payrollItemId in affectedItemIds)
        {
            if (existingSet.Contains(payrollItemId))
            {
                continue;
            }

            _db.Payslips.Add(new Payslip
            {
                PayrollItemId = payrollItemId,
                SlipNo = $"PS-{runNo}-{payrollItemId}-{now:yyMMddHHmm}",
                IssuedAt = now,
                IssuedBy = issuedBy,
                FileUrl = string.Empty
            });
            created++;
        }

        if (created > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return created;
    }

    private async Task<DateOnly?> ResolveStaffHireDateAsync(long staffId, CancellationToken cancellationToken)
    {
        var staff = await _db.Staffs
            .AsNoTracking()
            .Where(x => x.Id == staffId)
            .Select(x => new { x.HireDate, x.CreatedAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (staff is null) return null;
        if (staff.HireDate.HasValue) return staff.HireDate.Value;
        if (staff.CreatedAt.HasValue) return DateOnly.FromDateTime(staff.CreatedAt.Value);

        var contractHireDate = await _db.StaffContracts
            .AsNoTracking()
            .Where(x => x.StaffId == staffId)
            .OrderBy(x => x.EffectiveFrom)
            .Select(x => (DateOnly?)x.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
        if (contractHireDate.HasValue) return contractHireDate.Value;

        return await _db.StaffSalaryProfiles
            .AsNoTracking()
            .Where(x => x.StaffId == staffId)
            .OrderBy(x => x.EffectiveFrom)
            .Select(x => (DateOnly?)x.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static decimal ResolveBaseSalary(
        long staffId,
        IReadOnlyDictionary<long, decimal> profileBaseSalaryMap,
        IReadOnlyDictionary<long, ContractCompensationSnapshot> contractCompensationMap,
        IReadOnlyDictionary<long, decimal> staffTierBaseSalaryMap)
    {
        if (profileBaseSalaryMap.TryGetValue(staffId, out var profileBaseSalary) && profileBaseSalary > 0m)
        {
            return profileBaseSalary;
        }

        if (contractCompensationMap.TryGetValue(staffId, out var compensation) && compensation.BaseSalary > 0m)
        {
            return compensation.BaseSalary;
        }

        if (staffTierBaseSalaryMap.TryGetValue(staffId, out var tierBaseSalary) && tierBaseSalary > 0m)
        {
            return tierBaseSalary;
        }

        return 0m;
    }

    private static bool IsPayrollActiveStaff(StaffPayrollSnapshot staff, DateOnly asOfDate)
    {
        if (staff.TerminationDate.HasValue && staff.TerminationDate.Value < asOfDate)
        {
            return false;
        }

        var normalizedStatus = NormalizeKey(staff.Status);
        if (string.IsNullOrWhiteSpace(normalizedStatus))
        {
            return true;
        }

        if (ActiveStaffStatuses.Contains(normalizedStatus))
        {
            return true;
        }

        return false;
    }

    private static bool IsAdjustmentStatusAccepted(string? status)
    {
        var normalized = NormalizeKey(status);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        return AcceptedApprovalStatuses.Contains(normalized);
    }

    private static decimal ResolveSalaryCoefficientForYears(int yearsOfService, IReadOnlyList<SalaryCoefficientThreshold> thresholds)
    {
        var coefficient = 1.0000m;
        foreach (var threshold in thresholds)
        {
            if (threshold.YearsOfService <= yearsOfService && threshold.CoefficientValue > 0m)
            {
                coefficient = threshold.CoefficientValue;
            }
        }

        return coefficient > 0m ? coefficient : 1.0000m;
    }

    private static int CalculateCompletedYears(DateOnly fromDate, DateOnly toDate)
    {
        if (toDate < fromDate) return 0;
        var years = toDate.Year - fromDate.Year;
        if (toDate < fromDate.AddYears(years)) years--;
        return Math.Max(0, years);
    }

    private static VietnamPayrollPolicy ResolveVietnamPayrollPolicy(DateOnly asOfDate)
    {
        return asOfDate >= new DateOnly(2026, 1, 1) ? VietnamPayrollPolicy2026 : VietnamPayrollPolicyPre2026;
    }

    private static string NormalizeInsuranceZone(string? insuranceZone)
    {
        var text = NormalizeKey(insuranceZone);
        return text is "II" or "III" or "IV" ? text : "I";
    }

    private static decimal CalculatePitByProgressiveBracket(decimal taxableIncome)
    {
        var remaining = Math.Max(0m, taxableIncome);
        if (remaining <= 0m) return 0m;

        decimal lowerBound = 0m;
        decimal totalTax = 0m;
        foreach (var bracket in VietnamPitBrackets)
        {
            if (remaining <= 0m) break;
            var range = bracket.UpperLimit == decimal.MaxValue ? remaining : bracket.UpperLimit - lowerBound;
            var taxableInRange = Math.Min(remaining, range);
            if (taxableInRange > 0m)
            {
                totalTax += taxableInRange * bracket.Rate;
                remaining -= taxableInRange;
            }
            lowerBound = bracket.UpperLimit;
        }

        return RoundVnd(totalTax);
    }

    private static VietnamStatutoryAmounts CalculateVietnamStatutoryAmounts(
        decimal baseSalaryForInsurance,
        decimal grossIncome,
        int dependentCount,
        VietnamPayrollPolicy policy,
        string insuranceZone)
    {
        var zone = NormalizeInsuranceZone(insuranceZone);
        var normalizedDependents = Math.Max(0, dependentCount);
        var socialInsuranceCap = 20m * policy.SocialInsuranceBaseReference;
        var regionalMinimumWage = policy.RegionalMinimumWageByZone.TryGetValue(zone, out var wage) && wage > 0m
            ? wage
            : policy.RegionalMinimumWageByZone["I"];
        var unemploymentCap = 20m * regionalMinimumWage;
        var baseSalary = Math.Max(0m, baseSalaryForInsurance);
        var socialInsurableBase = Math.Min(baseSalary, socialInsuranceCap);
        var unemploymentInsurableBase = Math.Min(baseSalary, unemploymentCap);

        var employeeBhxh = RoundVnd(socialInsurableBase * policy.EmployeeBhxhRate);
        var employeeBhyt = RoundVnd(socialInsurableBase * policy.EmployeeBhytRate);
        var employeeBhtn = RoundVnd(unemploymentInsurableBase * policy.EmployeeBhtnRate);
        var employeeInsuranceTotal = RoundVnd(employeeBhxh + employeeBhyt + employeeBhtn);

        var employerBhxh = RoundVnd(socialInsurableBase * policy.EmployerBhxhRate);
        var employerBhyt = RoundVnd(socialInsurableBase * policy.EmployerBhytRate);
        var employerBhtn = RoundVnd(unemploymentInsurableBase * policy.EmployerBhtnRate);
        var employerOaid = RoundVnd(socialInsurableBase * policy.EmployerOaidRate);
        var employerInsuranceTotal = RoundVnd(employerBhxh + employerBhyt + employerBhtn + employerOaid);

        var taxableIncome = Math.Max(
            0m,
            Math.Max(0m, grossIncome) -
            employeeInsuranceTotal -
            policy.PersonalDeduction -
            normalizedDependents * policy.DependentDeduction);
        var pitAmount = CalculatePitByProgressiveBracket(taxableIncome);

        return new VietnamStatutoryAmounts(employeeInsuranceTotal, employerInsuranceTotal, pitAmount);
    }

    private static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value.Trim().ToUpperInvariant();
    }

    private static string NormalizeStatus(string? status, string fallback)
    {
        var normalized = NormalizeKey(status);
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static decimal RoundVnd(decimal value)
    {
        return Math.Max(0m, Math.Round(value, 0, MidpointRounding.AwayFromZero));
    }

    private sealed record PayrollRunResolution(PayrollRun Run, bool CreatedNew);
    private sealed record SalaryCoefficientThreshold(int YearsOfService, decimal CoefficientValue);
    private sealed record ContractCompensationSnapshot(decimal BaseSalary, decimal AllowanceAmount);
    private sealed record PitBracket(decimal UpperLimit, decimal Rate);
    private sealed record VietnamPayrollPolicy(
        decimal EmployeeBhxhRate,
        decimal EmployeeBhytRate,
        decimal EmployeeBhtnRate,
        decimal EmployerBhxhRate,
        decimal EmployerBhytRate,
        decimal EmployerBhtnRate,
        decimal EmployerOaidRate,
        decimal PersonalDeduction,
        decimal DependentDeduction,
        decimal SocialInsuranceBaseReference,
        IReadOnlyDictionary<string, decimal> RegionalMinimumWageByZone);

    private sealed record VietnamStatutoryAmounts(decimal EmployeeInsuranceTotal, decimal EmployerInsuranceTotal, decimal PitAmount);

    private sealed class StaffPayrollSnapshot
    {
        public long StaffId { get; init; }
        public long? BranchId { get; init; }
        public string StaffName { get; init; } = "";
        public DateOnly? HireDate { get; init; }
        public DateOnly? TerminationDate { get; init; }
        public string? Status { get; init; }
    }
}

public sealed class PayrollBatchCalculationCommand
{
    public long UserId { get; set; }
    public long? BranchId { get; set; }
    public long? PayrollPeriodId { get; set; }
    public long? PayrollRunId { get; set; }
    public bool OverwriteExisting { get; set; } = true;
    public bool IncludeInactiveStaff { get; set; }
    public bool IssuePayslips { get; set; }
}

public sealed class PayrollBatchCalculationResult
{
    public long PayrollPeriodId { get; set; }
    public long PayrollRunId { get; set; }
    public int RunNo { get; set; }
    public bool CreatedRun { get; set; }
    public string PeriodStartDate { get; set; } = "";
    public string PeriodEndDate { get; set; } = "";
    public int ProcessedStaffCount { get; set; }
    public int CalculatedStaffCount { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int SkippedNoBaseSalary { get; set; }
    public int SkippedExisting { get; set; }
    public bool IssuePayslips { get; set; }
    public int PayslipsIssued { get; set; }
    public decimal GrossTotal { get; set; }
    public decimal NetTotal { get; set; }
    public string Message { get; set; } = "";
}
