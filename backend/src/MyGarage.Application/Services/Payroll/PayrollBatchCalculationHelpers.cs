using MyGarage.Application.Payroll.DTOs;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services;

internal static class PayrollBatchCalculationHelpers
{
    public const string PayrollPeriodStatusOpen = nameof(PayrollPeriodStatusCode.OPEN);
    public const string PayrollRunStatusDraft = nameof(PayrollRunStatusCode.DRAFT);
    public const string PayrollRunStatusCalculated = nameof(PayrollRunStatusCode.CALCULATED);
    public const string PayrollItemStatusDraft = nameof(PayrollItemStatusCode.DRAFT);
    public const string CommissionStatusPending = nameof(CommissionTransactionStatusCode.PENDING);
    public const string CommissionStatusApproved = nameof(CommissionTransactionStatusCode.APPROVED);
    public const string CommissionStatusProcessed = "PROCESSED";
    public const string StaffSalaryProfileStatusActive = nameof(StaffSalaryProfileStatusCode.ACTIVE);
    public const string StaffContractStatusActive = nameof(StaffContractStatusCode.ACTIVE);
    public const string EmployeeLoanStatusActive = nameof(EmployeeLoanStatusCode.ACTIVE);
    public const string StaffStatusActive = nameof(StaffStatusCode.ACTIVE);
    public const string StaffStatusOnLeave = nameof(StaffStatusCode.ON_LEAVE);

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

    public static readonly HashSet<string> AcceptedApprovalStatuses =
    [
        nameof(AttendanceAdjustmentStatusCode.APPROVED),
        nameof(AttendanceAdjustmentStatusCode.APPLIED),
        nameof(OvertimeEntryStatusCode.PAID),
        nameof(JournalEntryStatusCode.POSTED),
        nameof(PaymentStatusCode.COMPLETED)
    ];

    public static readonly HashSet<string> AttendanceDeductionTypes =
    [
        "DEDUCTION",
        "ABSENT",
        "LATE",
        "UNPAID_LEAVE",
        "PENALTY",
        "EARLY_LEAVE",
        "MISSED_CHECKIN"
    ];

    public static readonly HashSet<string> AcceptedAdvanceStatuses =
    [
        nameof(SalaryAdvanceStatusCode.APPROVED),
        nameof(SalaryAdvanceStatusCode.DEDUCTING)
    ];

    public static readonly HashSet<string> AcceptedLoanStatuses =
    [
        nameof(EmployeeLoanStatusCode.ACTIVE)
    ];

    public static readonly HashSet<string> ActiveStaffStatuses =
    [
        StaffStatusActive,
        StaffStatusOnLeave
    ];

    public static decimal ResolveBaseSalary(
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

    public static bool IsPayrollActiveStaff(StaffPayrollSnapshot staff, DateOnly asOfDate)
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

        return ActiveStaffStatuses.Contains(normalizedStatus);
    }

    public static bool IsAdjustmentStatusAccepted(string? status)
    {
        var normalized = NormalizeKey(status);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        return AcceptedApprovalStatuses.Contains(normalized);
    }

    public static decimal ResolveSalaryCoefficientForYears(int yearsOfService, IReadOnlyList<SalaryCoefficientThreshold> thresholds)
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

    public static int CalculateCompletedYears(DateOnly fromDate, DateOnly toDate)
    {
        if (toDate < fromDate) return 0;
        var years = toDate.Year - fromDate.Year;
        if (toDate < fromDate.AddYears(years)) years--;
        return Math.Max(0, years);
    }

    public static VietnamPayrollPolicy ResolveVietnamPayrollPolicy(DateOnly asOfDate)
    {
        return asOfDate >= new DateOnly(2026, 1, 1) ? VietnamPayrollPolicy2026 : VietnamPayrollPolicyPre2026;
    }

    public static string NormalizeInsuranceZone(string? insuranceZone)
    {
        var text = NormalizeKey(insuranceZone);
        return text is "II" or "III" or "IV" ? text : "I";
    }

    public static decimal CalculatePitByProgressiveBracket(decimal taxableIncome)
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

    public static VietnamStatutoryAmounts CalculateVietnamStatutoryAmounts(
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

    public static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value.Trim().ToUpperInvariant();
    }

    public static string NormalizeStatus(string? status, string fallback)
    {
        var normalized = NormalizeKey(status);
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    public static decimal RoundVnd(decimal value)
    {
        return Math.Max(0m, Math.Round(value, 0, MidpointRounding.AwayFromZero));
    }

    internal sealed record ContractCompensationSnapshot(decimal BaseSalary, decimal AllowanceAmount);

    internal sealed record CommissionAggregationSnapshot(
        Dictionary<long, decimal> AmountByStaffId,
        Dictionary<long, List<long>> TransactionIdsByStaffId);

    internal sealed record PitBracket(decimal UpperLimit, decimal Rate);

    internal sealed record VietnamPayrollPolicy(
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

    internal sealed record VietnamStatutoryAmounts(
        decimal EmployeeInsuranceTotal,
        decimal EmployerInsuranceTotal,
        decimal PitAmount);
}
