using System;

namespace MyGarage.Domain.Rules;

public static class InventoryDomainRules
{
    public const decimal QuantityTolerance = 0.0001m;

    // 1. Stock Availability & Reservations
    public static decimal CalculateAvailableQuantity(decimal onHandQuantity, decimal? reservedQuantity)
    {
        var reserved = reservedQuantity ?? 0m;
        return Math.Max(0m, onHandQuantity - reserved);
    }

    public static bool CanReserveQuantity(decimal onHandQuantity, decimal? reservedQuantity, decimal requiredQuantity)
    {
        if (requiredQuantity <= QuantityTolerance) return false;
        var available = CalculateAvailableQuantity(onHandQuantity, reservedQuantity);
        return available + QuantityTolerance >= requiredQuantity;
    }

    public static decimal CalculateReservationDelta(decimal onHandQuantity, decimal currentReserved, decimal remainingNeeded)
    {
        if (remainingNeeded <= QuantityTolerance) return 0m;
        var available = Math.Max(0m, onHandQuantity - currentReserved);
        if (available <= QuantityTolerance) return 0m;
        return Math.Min(available, remainingNeeded);
    }

    public static decimal CalculateReleaseDelta(decimal currentReserved, decimal remainingToRelease)
    {
        if (remainingToRelease <= QuantityTolerance) return 0m;
        if (currentReserved <= QuantityTolerance) return 0m;
        return Math.Min(currentReserved, remainingToRelease);
    }

    // 2. Material Issuance Gatekeeper
    public sealed record MaterialIssuanceInput(
        string? RequestStatus,
        decimal OnHandQuantity,
        decimal QuantityToIssue,
        decimal RemainingRequestedQuantity
    );

    public sealed record MaterialIssuanceResult(
        bool CanIssue,
        string? BlockingReason,
        decimal DeductionFromReserved
    );

    public static MaterialIssuanceResult EvaluateMaterialIssuance(
        MaterialIssuanceInput input,
        decimal currentReservedQuantity)
    {
        if (string.Equals(input.RequestStatus, "ISSUED", StringComparison.OrdinalIgnoreCase))
            return new MaterialIssuanceResult(false, "Phiếu yêu cầu vật tư đã được xuất kho hoàn tất.", 0m);

        if (string.Equals(input.RequestStatus, "REJECTED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(input.RequestStatus, "CANCELED", StringComparison.OrdinalIgnoreCase))
            return new MaterialIssuanceResult(false, $"Phiếu yêu cầu ở trạng thái '{input.RequestStatus}', không thể xuất kho.", 0m);

        if (input.QuantityToIssue <= QuantityTolerance)
            return new MaterialIssuanceResult(false, "Số lượng xuất kho phải lớn hơn 0.", 0m);

        if (input.QuantityToIssue > input.RemainingRequestedQuantity + QuantityTolerance)
            return new MaterialIssuanceResult(false, $"Số lượng xuất ({input.QuantityToIssue:N2}) vượt quá số lượng còn thiếu của yêu cầu ({input.RemainingRequestedQuantity:N2}).", 0m);

        if (input.OnHandQuantity + QuantityTolerance < input.QuantityToIssue)
            return new MaterialIssuanceResult(false, $"Tồn kho không đủ để xuất (Tồn hiện tại: {input.OnHandQuantity:N2}, Yêu cầu xuất: {input.QuantityToIssue:N2}).", 0m);

        var deductionFromReserved = Math.Min(Math.Max(0m, currentReservedQuantity), input.QuantityToIssue);
        return new MaterialIssuanceResult(true, null, deductionFromReserved);
    }

    // 3. Material Return Gatekeeper
    public sealed record MaterialReturnInput(
        decimal IssuedQuantity,
        decimal AlreadyReturnedQuantity,
        decimal QuantityToReturn,
        string? ItemCondition
    );

    public sealed record MaterialReturnResult(
        bool CanReturn,
        string? BlockingReason,
        bool ShouldRestock
    );

    public static MaterialReturnResult EvaluateMaterialReturn(MaterialReturnInput input)
    {
        if (input.QuantityToReturn <= QuantityTolerance)
            return new MaterialReturnResult(false, "Số lượng trả lại phải lớn hơn 0.", false);

        var maxReturnable = Math.Max(0m, input.IssuedQuantity - input.AlreadyReturnedQuantity);
        if (input.QuantityToReturn > maxReturnable + QuantityTolerance)
            return new MaterialReturnResult(false, $"Số lượng trả ({input.QuantityToReturn:N2}) vượt quá số lượng đã xuất có thể trả ({maxReturnable:N2}).", false);

        var isGood = string.Equals(input.ItemCondition, "GOOD", StringComparison.OrdinalIgnoreCase);
        return new MaterialReturnResult(true, null, isGood);
    }

    // 4. Stock Alert & Threshold Calculations
    public static decimal ResolveEffectiveMinStock(decimal? inventoryMinStock, decimal? productMinStock)
    {
        if (inventoryMinStock.HasValue) return Math.Max(0m, inventoryMinStock.Value);
        if (productMinStock.HasValue) return Math.Max(0m, productMinStock.Value);
        return 0m;
    }

    public static decimal ResolveEffectiveReorderPoint(decimal? productReorderPoint, decimal effectiveMinStock)
    {
        if (productReorderPoint.HasValue) return Math.Max(0m, productReorderPoint.Value);
        return effectiveMinStock;
    }

    public static string? EvaluateStockAlertType(decimal quantity, decimal minStock, decimal reorderPoint)
    {
        if (quantity <= QuantityTolerance) return "OutOfStock";
        if (minStock > QuantityTolerance && quantity <= minStock) return "BelowMinStock";
        if (reorderPoint > QuantityTolerance && quantity <= reorderPoint) return "BelowReorder";
        return null;
    }

    public static string ResolveAlertSeverity(string? alertType)
    {
        return alertType switch
        {
            "OutOfStock" => "CRITICAL",
            "BelowMinStock" => "HIGH",
            "BelowReorder" => "MEDIUM",
            _ => "LOW"
        };
    }

    // 5. Moving Average Cost (MAC) & Valuation
    public static decimal CalculateMovingAverageCost(
        decimal currentQuantity,
        decimal currentMacPrice,
        decimal incomingQuantity,
        decimal incomingUnitPrice)
    {
        if (incomingQuantity <= QuantityTolerance)
        {
            return currentMacPrice;
        }

        if (currentQuantity <= QuantityTolerance)
        {
            return Math.Max(0m, incomingUnitPrice);
        }

        var totalQty = currentQuantity + incomingQuantity;
        if (totalQty <= QuantityTolerance)
        {
            return Math.Max(0m, incomingUnitPrice > 0m ? incomingUnitPrice : currentMacPrice);
        }

        var totalCost = (currentQuantity * currentMacPrice) + (incomingQuantity * incomingUnitPrice);
        return Math.Round(Math.Max(0m, totalCost / totalQty), 4);
    }

    public static decimal CalculateTotalInventoryValue(
        decimal quantity,
        decimal? macPrice,
        decimal? fallbackCostPrice)
    {
        if (quantity <= QuantityTolerance)
        {
            return 0m;
        }

        var effectiveUnitCost = macPrice ?? fallbackCostPrice ?? 0m;
        return Math.Round(quantity * Math.Max(0m, effectiveUnitCost), 2);
    }
}
