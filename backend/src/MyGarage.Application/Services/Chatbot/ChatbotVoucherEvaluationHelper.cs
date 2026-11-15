using System.Globalization;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

internal static class ChatbotVoucherEvaluationHelper
{
    internal static VoucherValidationResult ValidateVoucherState(Voucher voucher, DateTime now)
    {
        if (voucher.IsDeleted == true)
        {
            return VoucherValidationResult.Fail("VCH_DELETED", "Voucher is deleted.");
        }

        if (voucher.IsActive == false)
        {
            return VoucherValidationResult.Fail("VCH_INACTIVE", "Voucher is inactive.");
        }

        var status = NormalizeToken(voucher.Status);
        if (!string.Equals(status, nameof(VoucherStatusCode.ACTIVE), StringComparison.OrdinalIgnoreCase))
        {
            return VoucherValidationResult.Fail("VCH_STATUS_INVALID", $"Voucher status is {status}.");
        }

        if (now < voucher.StartDate)
        {
            return VoucherValidationResult.Fail("VCH_NOT_STARTED", "Voucher has not started.");
        }

        if (now > voucher.EndDate)
        {
            return VoucherValidationResult.Fail("VCH_EXPIRED", "Voucher has expired.");
        }

        if (!IsInValidTimeRange(voucher.ValidFrom, voucher.ValidTo, now.TimeOfDay))
        {
            return VoucherValidationResult.Fail("VCH_TIME_RANGE_INVALID", "Voucher is outside active time range.");
        }

        if (!IsDayAllowed(voucher.ValidDaysOfWeek, now.DayOfWeek))
        {
            return VoucherValidationResult.Fail("VCH_DAY_INVALID", "Voucher is not valid for current day.");
        }

        return VoucherValidationResult.Success();
    }

    internal static VoucherValidationResult ValidateCustomerAssignment(Voucher voucher, long customerId, DateTime now)
    {
        var customerAssignments = voucher.VoucherAssignments
            .Where(assignment => string.Equals(assignment.AssignmentType, "CUSTOMER", StringComparison.OrdinalIgnoreCase))
            .Where(assignment =>
                (!assignment.EffectiveFrom.HasValue || assignment.EffectiveFrom.Value <= now) &&
                (!assignment.EffectiveTo.HasValue || assignment.EffectiveTo.Value >= now))
            .ToList();

        if (customerAssignments.Count == 0)
        {
            return VoucherValidationResult.Success();
        }

        var includedCustomerIds = customerAssignments
            .Where(assignment => assignment.IsInclude)
            .Select(assignment => assignment.TargetId)
            .ToHashSet();
        var excludedCustomerIds = customerAssignments
            .Where(assignment => !assignment.IsInclude)
            .Select(assignment => assignment.TargetId)
            .ToHashSet();

        if (excludedCustomerIds.Contains(customerId))
        {
            return VoucherValidationResult.Fail("VCH_CUSTOMER_EXCLUDED", "Voucher excludes current customer.");
        }

        if (includedCustomerIds.Count > 0 && !includedCustomerIds.Contains(customerId))
        {
            return VoucherValidationResult.Fail("VCH_CUSTOMER_NOT_INCLUDED", "Voucher is not assigned to current customer.");
        }

        return VoucherValidationResult.Success();
    }

    internal static VoucherValidationResult ValidateUsageLimits(Voucher voucher)
    {
        if (voucher.UsageLimit.HasValue && voucher.UsageLimit.Value > 0)
        {
            var used = voucher.UsedCount ?? 0;
            if (used >= voucher.UsageLimit.Value)
            {
                return VoucherValidationResult.Fail("VCH_USAGE_LIMIT_REACHED", "Voucher usage limit reached.");
            }
        }

        if (voucher.BudgetLimit.HasValue && voucher.BudgetUsed >= voucher.BudgetLimit.Value)
        {
            return VoucherValidationResult.Fail("VCH_BUDGET_EXCEEDED", "Voucher budget limit exceeded.");
        }

        return VoucherValidationResult.Success();
    }

    internal static VoucherValidationResult ValidateVoucherConflictRules(Voucher voucher, ChatbotVoucherResolveRequest request)
    {
        if (request.TierDiscountAmount <= 0m)
        {
            return VoucherValidationResult.Success();
        }

        if (voucher.IsStackable != true)
        {
            return VoucherValidationResult.Fail("VCH_NOT_STACKABLE_WITH_TIER", "Voucher is not stackable with tier discount.");
        }

        var conflictRules = voucher.VoucherConflictRules
            .Where(rule => rule.IsActive)
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.Id)
            .ToList();

        foreach (var rule in conflictRules)
        {
            if (!IsTierDiscountConflict(rule))
            {
                continue;
            }

            var actionType = NormalizeToken(rule.ActionType);
            if (actionType is "BLOCK" or "REQUIRE_APPROVAL")
            {
                return VoucherValidationResult.Fail(
                    "VCH_CONFLICT_TIER_DISCOUNT",
                    string.IsNullOrWhiteSpace(rule.MessageText)
                        ? "Voucher conflicts with tier discount."
                        : rule.MessageText!);
            }
        }

        return VoucherValidationResult.Success();
    }

    internal static decimal CalculateDiscount(Voucher voucher, decimal eligibleAmount)
    {
        if (eligibleAmount <= 0m)
        {
            return 0m;
        }

        var voucherType = NormalizeToken(voucher.Type);
        var discountAmount = voucherType == "PERCENTAGE"
            ? Math.Round(eligibleAmount * (voucher.Value / 100m), 2, MidpointRounding.AwayFromZero)
            : voucher.Value;

        if (voucher.MaxDiscountAmount.HasValue)
        {
            discountAmount = Math.Min(discountAmount, voucher.MaxDiscountAmount.Value);
        }

        return Math.Max(0m, Math.Min(discountAmount, eligibleAmount));
    }

    internal static bool HasRemainingBudget(Voucher voucher, decimal discount)
    {
        if (!voucher.BudgetLimit.HasValue)
        {
            return true;
        }

        var budgetLimit = voucher.BudgetLimit.Value;
        return voucher.BudgetUsed < budgetLimit && voucher.BudgetUsed + discount <= budgetLimit;
    }

    internal static HashSet<string> ResolveOrderTypeTokens(ChatbotVoucherOrderType orderType)
    {
        return orderType switch
        {
            ChatbotVoucherOrderType.BOOKING => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "BOOKING"
            },
            ChatbotVoucherOrderType.SERVICE_ORDER_DRAFT => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "SERVICE_ORDER",
                "SERVICE_ORDER_DRAFT"
            },
            ChatbotVoucherOrderType.RETAIL_ORDER_DRAFT => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "RETAIL_ORDER",
                "RETAIL_ORDER_DRAFT",
                "PURCHASE"
            },
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                orderType.ToString()
            }
        };
    }

    internal static string NormalizeToken(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    internal static ChatbotVoucherResolveResult Fail(string reasonCode, string message)
    {
        return new ChatbotVoucherResolveResult
        {
            IsApplied = false,
            ReasonCode = reasonCode,
            Message = message
        };
    }

    private static bool IsTierDiscountConflict(VoucherConflictRule rule)
    {
        var blob = string.Join(
            "|",
            rule.ConflictType ?? string.Empty,
            rule.ConflictTarget ?? string.Empty,
            rule.MessageCode ?? string.Empty,
            rule.MessageText ?? string.Empty)
            .ToUpperInvariant();

        return blob.Contains("TIER", StringComparison.Ordinal) ||
               blob.Contains("MEMBERSHIP", StringComparison.Ordinal);
    }

    private static bool IsInValidTimeRange(TimeOnly? validFrom, TimeOnly? validTo, TimeSpan now)
    {
        if (!validFrom.HasValue && !validTo.HasValue)
        {
            return true;
        }

        var nowTime = TimeOnly.FromTimeSpan(now);

        if (validFrom.HasValue && validTo.HasValue)
        {
            return validFrom.Value <= validTo.Value
                ? nowTime >= validFrom.Value && nowTime <= validTo.Value
                : nowTime >= validFrom.Value || nowTime <= validTo.Value;
        }

        if (validFrom.HasValue)
        {
            return nowTime >= validFrom.Value;
        }

        return nowTime <= validTo!.Value;
    }

    private static bool IsDayAllowed(string? validDaysOfWeek, DayOfWeek currentDay)
    {
        if (string.IsNullOrWhiteSpace(validDaysOfWeek))
        {
            return true;
        }

        var tokens = validDaysOfWeek
            .Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.ToUpperInvariant())
            .ToList();
        if (tokens.Count == 0)
        {
            return true;
        }

        var dayNumber = ((int)currentDay).ToString(CultureInfo.InvariantCulture);
        var dayShort = currentDay.ToString().Substring(0, 3).ToUpperInvariant();
        var dayFull = currentDay.ToString().ToUpperInvariant();

        return tokens.Contains(dayNumber) || tokens.Contains(dayShort) || tokens.Contains(dayFull);
    }
}

internal sealed class VoucherVehicleContext
{
    public long VehicleId { get; set; }

    public long VehicleModelId { get; set; }

    public long VehicleBrandId { get; set; }
}

internal sealed class EvaluatedVoucher
{
    public Voucher Voucher { get; set; } = null!;

    public decimal DiscountAmount { get; set; }
}

internal sealed class VoucherValidationResult
{
    public bool IsSuccess { get; private set; }

    public string? ReasonCode { get; private set; }

    public string? Message { get; private set; }

    public static VoucherValidationResult Success() => new() { IsSuccess = true };

    public static VoucherValidationResult Fail(string reasonCode, string message) => new()
    {
        IsSuccess = false,
        ReasonCode = reasonCode,
        Message = message
    };
}
