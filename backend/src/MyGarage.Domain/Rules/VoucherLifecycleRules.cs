using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MyGarage.Domain.Rules;

public static class VoucherLifecycleRules
{
    public const string StatusDraft = "DRAFT";
    public const string StatusPendingApproval = "PENDING_APPROVAL";
    public const string StatusScheduled = "SCHEDULED";
    public const string StatusActive = "ACTIVE";
    public const string StatusPaused = "PAUSED";
    public const string StatusEnded = "ENDED";
    public const string StatusExhausted = "EXHAUSTED";
    public const string StatusExpired = "EXPIRED";
    public const string StatusRevoked = "REVOKED";
    public const string StatusArchived = "ARCHIVED";

    public static readonly HashSet<string> TerminalOrInactiveStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        StatusArchived,
        StatusRevoked,
        StatusExpired,
        StatusEnded
    };

    public static string NormalizeStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status) ? string.Empty : status.Trim().ToUpperInvariant();
    }

    public static bool CanTransitionStatus(string? currentStatus, string? targetStatus)
    {
        if (string.IsNullOrWhiteSpace(currentStatus) || string.IsNullOrWhiteSpace(targetStatus))
            return false;

        var curr = NormalizeStatus(currentStatus);
        var target = NormalizeStatus(targetStatus);

        if (curr == target) return true;

        if (curr is StatusArchived)
        {
            return false; // Archived is terminal
        }

        return curr switch
        {
            StatusDraft => target is StatusPendingApproval or StatusScheduled or StatusActive or StatusArchived,
            StatusPendingApproval => target is StatusScheduled or StatusActive or StatusDraft or StatusArchived,
            StatusScheduled => target is StatusActive or StatusPaused or StatusArchived,
            StatusActive => target is StatusPaused or StatusRevoked or StatusExpired or StatusExhausted or StatusEnded or StatusArchived,
            StatusPaused => target is StatusActive or StatusRevoked or StatusExpired or StatusEnded or StatusArchived,
            StatusExhausted => target is StatusActive or StatusRevoked or StatusExpired or StatusArchived,
            StatusExpired => target is StatusArchived or StatusActive, // Reactivate if dates extended
            StatusRevoked => target is StatusArchived,
            StatusEnded => target is StatusArchived or StatusActive,
            _ => false
        };
    }

    public static decimal CalculateDiscount(
        string? type,
        decimal value,
        decimal? maxDiscountAmount,
        decimal eligibleAmount)
    {
        if (eligibleAmount <= 0m || value <= 0m)
        {
            return 0m;
        }

        var normalizedType = (type ?? string.Empty).Trim().ToUpperInvariant();
        decimal discount;

        if (normalizedType == "PERCENTAGE")
        {
            discount = Math.Round(eligibleAmount * (value / 100m), 2, MidpointRounding.AwayFromZero);
        }
        else
        {
            discount = value;
        }

        if (maxDiscountAmount.HasValue && maxDiscountAmount.Value >= 0m)
        {
            discount = Math.Min(discount, maxDiscountAmount.Value);
        }

        discount = Math.Min(discount, eligibleAmount);
        return Math.Max(0m, Math.Round(discount, 2, MidpointRounding.AwayFromZero));
    }

    public static bool IsEligibleSpend(decimal eligibleAmount, decimal? minOrderValue)
    {
        if (!minOrderValue.HasValue || minOrderValue.Value <= 0m)
        {
            return true;
        }

        return eligibleAmount >= minOrderValue.Value;
    }

    public static bool IsDateRangeValid(DateTime now, DateTime startDate, DateTime endDate)
    {
        return now >= startDate && now <= endDate;
    }

    public static bool IsTimeOfDayValid(TimeOnly nowTime, TimeOnly? validFrom, TimeOnly? validTo)
    {
        if (!validFrom.HasValue || !validTo.HasValue)
        {
            return true;
        }

        var from = validFrom.Value;
        var to = validTo.Value;

        if (from <= to)
        {
            return nowTime >= from && nowTime <= to;
        }

        // Window crosses midnight
        return nowTime >= from || nowTime <= to;
    }

    public static bool IsDayOfWeekValid(DayOfWeek dayOfWeek, string? validDaysOfWeek)
    {
        if (string.IsNullOrWhiteSpace(validDaysOfWeek))
        {
            return true;
        }

        var allowedDays = validDaysOfWeek
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (allowedDays.Count == 0) return true;

        // DayOfWeek enum: Sunday=0, Monday=1, ..., Saturday=6
        var numericDay = ((int)dayOfWeek).ToString(CultureInfo.InvariantCulture);
        var isoDay = dayOfWeek == DayOfWeek.Sunday ? "7" : numericDay; // ISO 8601: Mon=1, Sun=7
        var dayName = dayOfWeek.ToString();

        return allowedDays.Contains(numericDay)
            || allowedDays.Contains(isoDay)
            || allowedDays.Contains(dayName);
    }

    public static bool IsBranchAllowed(
        long branchId,
        IReadOnlyCollection<long>? whitelistBranchIds,
        IReadOnlyCollection<long>? blacklistBranchIds)
    {
        if (blacklistBranchIds != null && blacklistBranchIds.Contains(branchId))
        {
            return false;
        }

        if (whitelistBranchIds != null && whitelistBranchIds.Count > 0)
        {
            return whitelistBranchIds.Contains(branchId);
        }

        return true;
    }

    public static bool CanRedeemTotalQuota(int? usageLimit, int? usedCount)
    {
        if (!usageLimit.HasValue || usageLimit.Value <= 0)
        {
            return true;
        }

        return (usedCount ?? 0) < usageLimit.Value;
    }

    public static bool CanRedeemCustomerQuota(int maxUsesPerCustomer, int customerRedeemedCount)
    {
        if (maxUsesPerCustomer <= 0)
        {
            return true;
        }

        return customerRedeemedCount < maxUsesPerCustomer;
    }

    public static bool CanRedeemVehicleQuota(int maxUsesPerVehicle, int vehicleRedeemedCount)
    {
        if (maxUsesPerVehicle <= 0)
        {
            return true;
        }

        return vehicleRedeemedCount < maxUsesPerVehicle;
    }

    public static bool CanRedeemBudget(decimal? budgetLimit, decimal budgetUsed, decimal discountAmount)
    {
        if (!budgetLimit.HasValue || budgetLimit.Value <= 0m)
        {
            return true;
        }

        return (budgetUsed + discountAmount) <= budgetLimit.Value;
    }

    public static bool CanRedeemFirstUse(bool firstUseOnly, int priorCompletedOrdersCount)
    {
        if (!firstUseOnly)
        {
            return true;
        }

        return priorCompletedOrdersCount == 0;
    }

    public static string ResolveStatus(
        DateTime now,
        DateTime startDate,
        DateTime endDate,
        bool isActive,
        bool isDeleted,
        int? usageLimit,
        int? usedCount,
        decimal? budgetLimit,
        decimal budgetUsed)
    {
        if (isDeleted)
        {
            return StatusArchived;
        }

        if (!isActive)
        {
            return StatusPaused;
        }

        if (now < startDate)
        {
            return StatusScheduled;
        }

        if (now > endDate)
        {
            return StatusExpired;
        }

        if (usageLimit.HasValue && usageLimit.Value > 0 && (usedCount ?? 0) >= usageLimit.Value)
        {
            return StatusExhausted;
        }

        if (budgetLimit.HasValue && budgetLimit.Value > 0 && budgetUsed >= budgetLimit.Value)
        {
            return StatusExhausted;
        }

        return StatusActive;
    }

    public sealed record RollbackStateResult(
        int NewUsedCount,
        int NewRedeemedCount,
        decimal NewBudgetUsed,
        string ResolvedStatus,
        bool IsActive
    );

    public static RollbackStateResult ComputeRollbackState(
        int currentUsedCount,
        int currentRedeemedCount,
        decimal currentBudgetUsed,
        decimal discountToRollback,
        int? usageLimit,
        decimal? budgetLimit,
        DateTime now,
        DateTime startDate,
        DateTime endDate)
    {
        var newUsed = Math.Max(0, currentUsedCount - 1);
        var newRedeemed = Math.Max(0, currentRedeemedCount - 1);
        var newBudget = Math.Max(0m, currentBudgetUsed - discountToRollback);

        var status = ResolveStatus(
            now,
            startDate,
            endDate,
            isActive: true,
            isDeleted: false,
            usageLimit,
            newUsed,
            budgetLimit,
            newBudget);

        var isActive = status == StatusActive;

        return new RollbackStateResult(newUsed, newRedeemed, newBudget, status, isActive);
    }
}
