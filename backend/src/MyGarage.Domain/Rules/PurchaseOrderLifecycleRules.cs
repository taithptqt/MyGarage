using System;
using System.Collections.Generic;
using System.Linq;

namespace MyGarage.Domain.Rules;

public static class PurchaseOrderLifecycleRules
{
    public const decimal QuantityTolerance = 0.0001m;

    public static readonly HashSet<string> TerminalStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "COMPLETED",
        "RECEIVED",
        "CANCELED",
        "CANCELLED"
    };

    public static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return string.Empty;
        var s = status.Trim().ToUpperInvariant();
        return s switch
        {
            "PENDING_APPROVAL" => "PENDING",
            "PARTIALLY_RECEIVED" => "PARTIAL_RECEIVED",
            "RECEIVED" => "COMPLETED",
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

        if (IsTerminalStatus(curr)) return false;
        if (curr == target) return true;

        return curr switch
        {
            "DRAFT" => target is "PENDING" or "CANCELED",
            "PENDING" => target is "APPROVED" or "CANCELED" or "DRAFT",
            "APPROVED" => target is "PARTIAL_RECEIVED" or "COMPLETED" or "CANCELED",
            "PARTIAL_RECEIVED" => target is "COMPLETED" or "CANCELED",
            _ => false
        };
    }

    public static bool CanApprove(string? currentStatus)
    {
        var norm = NormalizeStatus(currentStatus);
        return norm is "PENDING" or "DRAFT";
    }

    public static bool CanReceive(string? currentStatus)
    {
        var norm = NormalizeStatus(currentStatus);
        return norm is "APPROVED" or "PARTIAL_RECEIVED";
    }

    // Cancellation & Deletion Validation
    public sealed record PoCancellationInput(
        string? CurrentStatus,
        decimal TotalReceivedQuantity,
        int GoodsReceiptNoteCount
    );

    public sealed record PoCancellationResult(
        bool CanCancel,
        string? BlockingReason
    );

    public static PoCancellationResult EvaluateCancellation(PoCancellationInput input)
    {
        var norm = NormalizeStatus(input.CurrentStatus);
        if (norm == "CANCELED")
            return new PoCancellationResult(false, "Đơn mua hàng đã ở trạng thái hủy.");

        if (norm == "COMPLETED")
            return new PoCancellationResult(false, "Đơn mua hàng đã hoàn tất nhập kho, không thể hủy.");

        if (input.GoodsReceiptNoteCount > 0 || input.TotalReceivedQuantity > QuantityTolerance)
            return new PoCancellationResult(false, $"Đơn mua hàng đã phát sinh {input.GoodsReceiptNoteCount} phiếu nhập kho ({input.TotalReceivedQuantity:N2} sản phẩm), không thể hủy hoặc xóa trực tiếp.");

        return new PoCancellationResult(true, null);
    }

    // Receipt Line Evaluation & Reconciliation
    public sealed record PoReceiptLineInput(
        decimal OrderedQuantity,
        decimal CurrentReceivedQuantity,
        decimal IncomingQuantity,
        decimal? IncomingUnitPrice,
        decimal FallbackUnitPrice
    );

    public sealed record PoReceiptLineResult(
        bool HasAcceptedQuantity,
        decimal AcceptedQuantity,
        decimal EffectiveUnitPrice,
        decimal LineTotalAmount,
        decimal NewReceivedQuantity,
        decimal RemainingQuantity,
        bool IsLineComplete
    );

    public static PoReceiptLineResult EvaluateReceiptLine(PoReceiptLineInput input)
    {
        var unitPrice = Math.Max(0m, input.IncomingUnitPrice ?? input.FallbackUnitPrice);
        var remainingBefore = Math.Max(0m, input.OrderedQuantity - input.CurrentReceivedQuantity);

        if (input.IncomingQuantity <= QuantityTolerance || remainingBefore <= QuantityTolerance)
        {
            return new PoReceiptLineResult(
                HasAcceptedQuantity: false,
                AcceptedQuantity: 0m,
                EffectiveUnitPrice: unitPrice,
                LineTotalAmount: 0m,
                NewReceivedQuantity: input.CurrentReceivedQuantity,
                RemainingQuantity: remainingBefore,
                IsLineComplete: remainingBefore <= QuantityTolerance
            );
        }

        // Over-receipt capping: AcceptedQuantity = Math.Min(remainingNeeded, incomingQuantity)
        var accepted = Math.Min(remainingBefore, input.IncomingQuantity);
        var lineAmount = Math.Round(accepted * unitPrice, 2);
        var newReceived = input.CurrentReceivedQuantity + accepted;
        var remainingAfter = Math.Max(0m, input.OrderedQuantity - newReceived);

        return new PoReceiptLineResult(
            HasAcceptedQuantity: accepted > QuantityTolerance,
            AcceptedQuantity: accepted,
            EffectiveUnitPrice: unitPrice,
            LineTotalAmount: lineAmount,
            NewReceivedQuantity: newReceived,
            RemainingQuantity: remainingAfter,
            IsLineComplete: remainingAfter <= QuantityTolerance
        );
    }

    // PO Status After Batch Receipt
    public sealed record PoLineStatusCheck(
        decimal OrderedQuantity,
        decimal ReceivedQuantity
    );

    public static string DetermineStatusAfterReceipt(IEnumerable<PoLineStatusCheck> lines)
    {
        var lineList = lines.ToList();
        if (lineList.Count == 0) return "APPROVED";

        var allCompleted = lineList.All(x => x.ReceivedQuantity + QuantityTolerance >= x.OrderedQuantity);
        if (allCompleted) return "COMPLETED";

        var hasAnyReceived = lineList.Any(x => x.ReceivedQuantity > QuantityTolerance);
        return hasAnyReceived ? "PARTIAL_RECEIVED" : "APPROVED";
    }

    // Genuine Moving Average Cost (MAC) Formula:
    // ((oldQty * oldMac) + (incomingQty * incomingPrice)) / (oldQty + incomingQty)
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

    public static decimal CalculateTotalInventoryValue(decimal quantity, decimal macPrice)
    {
        if (quantity <= QuantityTolerance) return 0m;
        return Math.Round(quantity * Math.Max(0m, macPrice), 2);
    }

    // Order Totals Recalculation
    public sealed record PoItemTotalInput(
        decimal OrderedQuantity,
        decimal CostPrice
    );

    public static decimal CalculateOrderTotal(IEnumerable<PoItemTotalInput> items)
    {
        if (items == null) return 0m;
        return Math.Round(items.Sum(i => Math.Max(0m, i.OrderedQuantity) * Math.Max(0m, i.CostPrice)), 2);
    }

    // Fulfillment Metrics
    public static decimal CalculateFulfillmentPercentage(decimal orderedQty, decimal receivedQty)
    {
        if (orderedQty <= QuantityTolerance) return 0m;
        return Math.Round((receivedQty / orderedQty) * 100m, 2, MidpointRounding.AwayFromZero);
    }
}
