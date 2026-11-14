using System;
using System.Collections.Generic;
using System.Linq;

namespace MyGarage.Domain.Rules;

public static class RetailOrderLifecycleRules
{
    public const decimal CurrencyTolerance = 0.01m;

    public static readonly HashSet<string> TerminalStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "COMPLETED",
        "CANCELED",
        "CANCELLED"
    };

    public static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return string.Empty;
        var s = status.Trim().ToUpperInvariant();
        return s switch
        {
            "CANCELLED" => "CANCELED",
            _ => s
        };
    }

    public static bool IsTerminalStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        return TerminalStatuses.Contains(status.Trim());
    }

    public static bool CanTransition(string? currentStatus, string? targetStatus)
    {
        if (string.IsNullOrWhiteSpace(currentStatus) || string.IsNullOrWhiteSpace(targetStatus))
            return false;

        var curr = NormalizeStatus(currentStatus);
        var target = NormalizeStatus(targetStatus);

        if (curr == target) return true;
        if (IsTerminalStatus(curr)) return false;

        return curr switch
        {
            "DRAFT" => target is "CONFIRMED" or "CANCELED",
            "CONFIRMED" => target is "PROCESSING" or "SHIPPING" or "CANCELED",
            "PROCESSING" => target is "SHIPPING" or "CANCELED",
            "SHIPPING" => target is "DELIVERED" or "CANCELED",
            "DELIVERED" => target is "COMPLETED" or "CANCELED",
            _ => false
        };
    }

    public static bool CanCancel(string? currentStatus)
    {
        if (string.IsNullOrWhiteSpace(currentStatus)) return false;
        var norm = NormalizeStatus(currentStatus);
        return !IsTerminalStatus(norm);
    }

    public static bool CanFulfillStock(decimal requestedQuantity, decimal availableQuantity)
    {
        if (requestedQuantity <= 0) return false;
        return availableQuantity >= requestedQuantity;
    }

    public sealed record StockDeductionResult(
        bool NeedsDeduction,
        decimal QuantityToDeduct,
        bool CanFulfill
    );

    public static StockDeductionResult EvaluateStockDeduction(
        decimal requestedQuantity,
        decimal alreadyDeductedQuantity,
        decimal availableQuantity)
    {
        var remainingNeeded = Math.Max(0m, requestedQuantity - alreadyDeductedQuantity);
        if (remainingNeeded <= 0m)
        {
            return new StockDeductionResult(false, 0m, true);
        }

        var canFulfill = availableQuantity >= remainingNeeded;
        return new StockDeductionResult(true, remainingNeeded, canFulfill);
    }

    public sealed record LineAmounts(
        decimal SubTotal,
        decimal TaxAmount,
        decimal FinalAmount
    );

    public static LineAmounts CalculateLineAmounts(decimal unitPrice, decimal quantity, decimal taxRate)
    {
        var price = Math.Max(0m, unitPrice);
        var qty = Math.Max(0m, quantity);
        var rate = Math.Max(0m, taxRate);

        var subTotal = Math.Round(price * qty, 2, MidpointRounding.AwayFromZero);
        var taxAmount = Math.Round(subTotal * (rate / 100m), 2, MidpointRounding.AwayFromZero);
        var finalAmount = Math.Round(subTotal + taxAmount, 2, MidpointRounding.AwayFromZero);

        return new LineAmounts(subTotal, taxAmount, finalAmount);
    }

    public sealed record OrderTotals(
        decimal SubTotal,
        decimal TotalDiscount,
        decimal TaxAmount,
        decimal FinalAmount
    );

    public static OrderTotals CalculateOrderTotals(
        decimal subTotal,
        decimal taxAmount,
        decimal voucherDiscount,
        decimal tierDiscount = 0m)
    {
        var safeSubTotal = Math.Max(0m, subTotal);
        var safeTax = Math.Max(0m, taxAmount);
        var safeVoucher = Math.Max(0m, voucherDiscount);
        var safeTier = Math.Max(0m, tierDiscount);

        var totalDiscount = Math.Round(safeVoucher + safeTier, 2, MidpointRounding.AwayFromZero);
        var cappedDiscount = Math.Min(totalDiscount, safeSubTotal);
        var finalAmount = Math.Max(0m, Math.Round(safeSubTotal - cappedDiscount + safeTax, 2, MidpointRounding.AwayFromZero));

        return new OrderTotals(safeSubTotal, totalDiscount, safeTax, finalAmount);
    }

    public sealed record ReturnValidationResult(
        bool IsValid,
        string? ErrorMessage
    );

    public static ReturnValidationResult ValidateReturnLine(
        decimal soldQuantity,
        decimal alreadyReturnedQuantity,
        decimal currentReturnQuantity)
    {
        if (currentReturnQuantity <= 0)
        {
            return new ReturnValidationResult(false, "Số lượng trả lại phải lớn hơn 0.");
        }

        if (soldQuantity <= 0)
        {
            return new ReturnValidationResult(false, "Số lượng sản phẩm bán ra không hợp lệ.");
        }

        if (alreadyReturnedQuantity < 0)
        {
            alreadyReturnedQuantity = 0;
        }

        if (alreadyReturnedQuantity + currentReturnQuantity > soldQuantity)
        {
            var remainingAllowable = Math.Max(0m, soldQuantity - alreadyReturnedQuantity);
            return new ReturnValidationResult(
                false,
                $"Số lượng trả ({currentReturnQuantity}) vượt quá số lượng tối đa có thể trả ({remainingAllowable}). Đã bán: {soldQuantity}, đã trả: {alreadyReturnedQuantity}.");
        }

        return new ReturnValidationResult(true, null);
    }
}
