using System;
using System.Collections.Generic;

namespace MyGarage.Domain.Rules;

public static class SalaryDomainRules
{
    public const decimal DefaultSocialInsuranceReferenceBase = 2_340_000m;
    public const decimal DefaultRegionalMinimumWageZoneI = 4_960_000m;

    public const decimal EmployeeBhxhRate = 0.08m;
    public const decimal EmployeeBhytRate = 0.015m;
    public const decimal EmployeeBhtnRate = 0.01m;

    public const decimal EmployerBhxhRate = 0.175m;
    public const decimal EmployerBhytRate = 0.03m;
    public const decimal EmployerBhtnRate = 0.01m;
    public const decimal EmployerOaidRate = 0.005m;

    public const decimal Pre2026PersonalDeduction = 11_000_000m;
    public const decimal Pre2026DependentDeduction = 4_400_000m;

    public const decimal Year2026PersonalDeduction = 15_500_000m;
    public const decimal Year2026DependentDeduction = 6_200_000m;

    private static readonly (decimal UpperLimit, decimal Rate)[] VietnamPitBrackets =
    [
        (5_000_000m, 0.05m),
        (10_000_000m, 0.10m),
        (18_000_000m, 0.15m),
        (32_000_000m, 0.20m),
        (52_000_000m, 0.25m),
        (80_000_000m, 0.30m),
        (decimal.MaxValue, 0.35m)
    ];

    public static bool ValidateSalaryProfile(
        decimal? baseSalary,
        DateOnly? effectiveFrom,
        DateOnly? effectiveTo,
        out string? errorMessage)
    {
        errorMessage = null;

        if (!baseSalary.HasValue || baseSalary.Value <= 0m)
        {
            errorMessage = "Lương cơ bản (baseSalary) phải lớn hơn 0.";
            return false;
        }

        if (!effectiveFrom.HasValue)
        {
            errorMessage = "Ngày hiệu lực từ (effectiveFrom) là bắt buộc.";
            return false;
        }

        if (effectiveTo.HasValue && effectiveTo.Value < effectiveFrom.Value)
        {
            errorMessage = "Ngày kết thúc hiệu lực (effectiveTo) phải lớn hơn hoặc bằng ngày bắt đầu (effectiveFrom).";
            return false;
        }

        return true;
    }

    public static bool ValidateSalaryProfile(
        decimal? baseSalary,
        DateTime? effectiveFrom,
        DateTime? effectiveTo,
        out string? errorMessage)
    {
        DateOnly? fromDate = effectiveFrom.HasValue ? DateOnly.FromDateTime(effectiveFrom.Value) : null;
        DateOnly? toDate = effectiveTo.HasValue ? DateOnly.FromDateTime(effectiveTo.Value) : null;
        return ValidateSalaryProfile(baseSalary, fromDate, toDate, out errorMessage);
    }

    public static bool CheckProfileDateOverlap(
        DateOnly start1,
        DateOnly? end1,
        DateOnly start2,
        DateOnly? end2)
    {
        var effectiveEnd1 = end1 ?? DateOnly.MaxValue;
        var effectiveEnd2 = end2 ?? DateOnly.MaxValue;

        return start1 <= effectiveEnd2 && start2 <= effectiveEnd1;
    }

    public static bool ValidateCompensationComponents(
        decimal baseSalary,
        decimal allowance,
        decimal commission,
        decimal overtime,
        decimal bonus,
        decimal deductions,
        decimal penalty,
        out string? errorMessage)
    {
        errorMessage = null;

        if (baseSalary < 0m)
        {
            errorMessage = "Lương cơ bản không được âm.";
            return false;
        }

        if (allowance < 0m || commission < 0m || overtime < 0m || bonus < 0m)
        {
            errorMessage = "Các khoản phụ cấp, hoa hồng, làm thêm giờ và thưởng không được âm.";
            return false;
        }

        if (deductions < 0m || penalty < 0m)
        {
            errorMessage = "Các khoản khấu trừ và phạt không được âm.";
            return false;
        }

        return true;
    }

    public static decimal CalculateNetSalary(
        decimal baseSalary,
        decimal allowance,
        decimal commission,
        decimal overtime,
        decimal bonus,
        decimal deductions,
        decimal penalty)
    {
        var gross = Math.Max(0m, baseSalary)
            + Math.Max(0m, allowance)
            + Math.Max(0m, commission)
            + Math.Max(0m, overtime)
            + Math.Max(0m, bonus);

        var totalDeductions = Math.Max(0m, deductions) + Math.Max(0m, penalty);

        return Math.Max(0m, gross - totalDeductions);
    }

    public static (decimal EmployeeTotal, decimal EmployerTotal, decimal EmployeeBhxh, decimal EmployeeBhyt, decimal EmployeeBhtn, decimal EmployerBhxh, decimal EmployerBhyt, decimal EmployerBhtn, decimal EmployerOaid)
        CalculateVietnamStatutoryInsurance(
            decimal baseSalaryForInsurance,
            decimal socialInsuranceBaseRef = DefaultSocialInsuranceReferenceBase,
            decimal regionalMinWage = DefaultRegionalMinimumWageZoneI)
    {
        var baseSalary = Math.Max(0m, baseSalaryForInsurance);
        var socialInsuranceCap = 20m * Math.Max(0m, socialInsuranceBaseRef);
        var unemploymentCap = 20m * Math.Max(0m, regionalMinWage);

        var socialInsurableBase = Math.Min(baseSalary, socialInsuranceCap);
        var unemploymentInsurableBase = Math.Min(baseSalary, unemploymentCap);

        var employeeBhxh = RoundVnd(socialInsurableBase * EmployeeBhxhRate);
        var employeeBhyt = RoundVnd(socialInsurableBase * EmployeeBhytRate);
        var employeeBhtn = RoundVnd(unemploymentInsurableBase * EmployeeBhtnRate);
        var employeeTotal = RoundVnd(employeeBhxh + employeeBhyt + employeeBhtn);

        var employerBhxh = RoundVnd(socialInsurableBase * EmployerBhxhRate);
        var employerBhyt = RoundVnd(socialInsurableBase * EmployerBhytRate);
        var employerBhtn = RoundVnd(unemploymentInsurableBase * EmployerBhtnRate);
        var employerOaid = RoundVnd(socialInsurableBase * EmployerOaidRate);
        var employerTotal = RoundVnd(employerBhxh + employerBhyt + employerBhtn + employerOaid);

        return (employeeTotal, employerTotal, employeeBhxh, employeeBhyt, employeeBhtn, employerBhxh, employerBhyt, employerBhtn, employerOaid);
    }

    public static decimal CalculateVietnamPersonalIncomeTax(decimal taxableIncome)
    {
        var remaining = Math.Max(0m, taxableIncome);
        if (remaining <= 0m) return 0m;

        decimal lowerBound = 0m;
        decimal totalTax = 0m;

        foreach (var (upperLimit, rate) in VietnamPitBrackets)
        {
            if (remaining <= 0m) break;
            var range = upperLimit == decimal.MaxValue ? remaining : upperLimit - lowerBound;
            var taxableInRange = Math.Min(remaining, range);
            if (taxableInRange > 0m)
            {
                totalTax += taxableInRange * rate;
                remaining -= taxableInRange;
            }
            lowerBound = upperLimit;
        }

        return RoundVnd(totalTax);
    }

    public static (decimal EmployeeInsurance, decimal EmployerInsurance, decimal TaxableIncome, decimal PitAmount)
        CalculateStatutoryDeductions(
            decimal baseSalaryForInsurance,
            decimal grossIncome,
            int dependentCount,
            bool is2026OrLater)
    {
        var (employeeInsurance, employerInsurance, _, _, _, _, _, _, _) =
            CalculateVietnamStatutoryInsurance(baseSalaryForInsurance);

        var personalDeduction = is2026OrLater ? Year2026PersonalDeduction : Pre2026PersonalDeduction;
        var dependentDeduction = is2026OrLater ? Year2026DependentDeduction : Pre2026DependentDeduction;
        var normalizedDependents = Math.Max(0, dependentCount);

        var taxableIncome = Math.Max(
            0m,
            Math.Max(0m, grossIncome) - employeeInsurance - personalDeduction - (normalizedDependents * dependentDeduction));

        var pitAmount = CalculateVietnamPersonalIncomeTax(taxableIncome);

        return (employeeInsurance, employerInsurance, taxableIncome, pitAmount);
    }

    public static bool ValidateApprovalDecision(
        string? currentRequestStatus,
        decimal? baseSalary,
        DateTime? effectiveFrom,
        out string? errorMessage)
    {
        errorMessage = null;

        var status = (currentRequestStatus ?? string.Empty).Trim().ToUpperInvariant();
        if (status != "PENDING")
        {
            errorMessage = $"Chỉ yêu cầu ở trạng thái PENDING mới có thể phê duyệt hoặc từ chối (hiện tại: {currentRequestStatus}).";
            return false;
        }

        if (baseSalary.HasValue && baseSalary.Value <= 0m)
        {
            errorMessage = "Lương cơ bản được duyệt phải lớn hơn 0.";
            return false;
        }

        return true;
    }

    public static decimal RoundVnd(decimal value)
    {
        return Math.Max(0m, Math.Round(value, 0, MidpointRounding.AwayFromZero));
    }
}
