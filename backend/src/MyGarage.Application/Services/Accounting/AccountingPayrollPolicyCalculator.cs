using MyGarage.Enums;

namespace MyGarage.Application.Accounting;

public interface IAccountingPayrollPolicyCalculator
{
    bool IsPayrollActiveStaff(StaffPayrollSnapshot staff, DateOnly asOfDate);
    decimal ResolveSalaryCoefficientForYears(int yearsOfService, IReadOnlyList<SalaryCoefficientThreshold> thresholds);
    VietnamPayrollPolicy ResolveVietnamPayrollPolicy(DateOnly asOfDate);
    string NormalizeInsuranceZone(string? insuranceZone);
    decimal CalculatePitByProgressiveBracket(decimal taxableIncome);
    VietnamStatutoryAmounts CalculateVietnamStatutoryAmounts(
        decimal baseSalaryForInsurance,
        decimal grossIncome,
        int dependentCount,
        VietnamPayrollPolicy policy,
        string insuranceZone);
    decimal RoundVnd(decimal value);
    int CalculateCompletedYears(DateOnly fromDate, DateOnly toDate);
}

public sealed class AccountingPayrollPolicyCalculator : IAccountingPayrollPolicyCalculator
{
    private const string StaffStatusActive = nameof(StaffStatusCode.ACTIVE);
    private const string StaffStatusOnLeave = nameof(StaffStatusCode.ON_LEAVE);

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

    public bool IsPayrollActiveStaff(StaffPayrollSnapshot staff, DateOnly asOfDate)
    {
        if (staff.TerminationDate.HasValue && staff.TerminationDate.Value < asOfDate)
        {
            return false;
        }

        var normalizedStatus = (staff.Status ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalizedStatus))
        {
            return true;
        }

        if (normalizedStatus is StaffStatusActive or StaffStatusOnLeave)
        {
            return true;
        }

        return false;
    }

    public decimal ResolveSalaryCoefficientForYears(int yearsOfService, IReadOnlyList<SalaryCoefficientThreshold> thresholds)
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

    public VietnamPayrollPolicy ResolveVietnamPayrollPolicy(DateOnly asOfDate)
    {
        return asOfDate >= new DateOnly(2026, 1, 1) ? VietnamPayrollPolicy2026 : VietnamPayrollPolicyPre2026;
    }

    public string NormalizeInsuranceZone(string? insuranceZone)
    {
        var text = (insuranceZone ?? "").Trim().ToUpperInvariant();
        return text is "II" or "III" or "IV" ? text : "I";
    }

    public decimal CalculatePitByProgressiveBracket(decimal taxableIncome)
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

    public VietnamStatutoryAmounts CalculateVietnamStatutoryAmounts(
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

    public decimal RoundVnd(decimal value)
    {
        return Math.Max(0m, Math.Round(value, 0, MidpointRounding.AwayFromZero));
    }

    public int CalculateCompletedYears(DateOnly fromDate, DateOnly toDate)
    {
        if (toDate < fromDate) return 0;
        var years = toDate.Year - fromDate.Year;
        if (toDate < fromDate.AddYears(years)) years--;
        return Math.Max(0, years);
    }
}

public sealed record SalaryCoefficientThreshold(int YearsOfService, decimal CoefficientValue);

public sealed record PitBracket(decimal UpperLimit, decimal Rate);

public sealed record VietnamPayrollPolicy(
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

public sealed record VietnamStatutoryAmounts(decimal EmployeeInsuranceTotal, decimal EmployerInsuranceTotal, decimal PitAmount);

public sealed class StaffPayrollSnapshot
{
    public long StaffId { get; init; }
    public long? BranchId { get; init; }
    public string StaffName { get; init; } = "";
    public DateOnly? HireDate { get; init; }
    public DateOnly? TerminationDate { get; init; }
    public string? Status { get; init; }
}
