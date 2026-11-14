using System;
using System.Collections.Generic;
using System.Linq;

namespace MyGarage.Domain.Rules;

public static class InvoiceLifecycleRules
{
    public const string StatusDraft = "DRAFT";
    public const string StatusIssued = "ISSUED";
    public const string StatusPaid = "PAID";
    public const string StatusCancelled = "CANCELLED";

    public const string PaymentStatusUnpaid = "UNPAID";
    public const string PaymentStatusPartial = "PARTIAL";
    public const string PaymentStatusPaid = "PAID";

    public static readonly HashSet<string> AllStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        StatusDraft,
        StatusIssued,
        StatusPaid,
        StatusCancelled
    };

    public static readonly HashSet<string> ImmutableStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        StatusIssued,
        StatusPaid,
        StatusCancelled
    };

    public static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return StatusDraft;
        var s = status.Trim().ToUpperInvariant();
        return s switch
        {
            "CANCELED" => StatusCancelled,
            _ when AllStatuses.Contains(s) => s,
            _ => StatusDraft
        };
    }

    public static bool IsImmutableStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        var norm = NormalizeStatus(status);
        return ImmutableStatuses.Contains(norm);
    }

    public static bool CanMutateInvoice(string? currentStatus, bool isServiceOrderClosed = false)
    {
        if (isServiceOrderClosed) return false;
        var norm = NormalizeStatus(currentStatus);
        return norm == StatusDraft;
    }

    public static void EnsureInvoiceCanBeMutated(string? currentStatus, bool isServiceOrderClosed = false)
    {
        if (isServiceOrderClosed)
        {
            throw new InvalidOperationException("Không thể chỉnh sửa hóa đơn vì đơn hàng dịch vụ liên kết đã đóng.");
        }

        var norm = NormalizeStatus(currentStatus);
        if (IsImmutableStatus(norm))
        {
            throw new InvalidOperationException("Hóa đơn là chứng từ bất biến và không được phép chỉnh sửa hoặc xóa trực tiếp.");
        }
    }

    public static bool CanTransition(string? currentStatus, string? targetStatus)
    {
        if (string.IsNullOrWhiteSpace(currentStatus) || string.IsNullOrWhiteSpace(targetStatus))
            return false;

        var curr = NormalizeStatus(currentStatus);
        var target = NormalizeStatus(targetStatus);

        if (curr == target) return true;

        return curr switch
        {
            StatusDraft => target is StatusIssued or StatusCancelled,
            StatusIssued => target is StatusPaid or StatusCancelled,
            StatusPaid => false, // Paid is terminal
            StatusCancelled => false, // Cancelled is terminal
            _ => false
        };
    }

    public static string ResolvePaymentStatus(decimal paidAmount, decimal totalAmount)
    {
        if (totalAmount <= 0m) return PaymentStatusPaid;
        if (paidAmount <= 0m) return PaymentStatusUnpaid;
        return paidAmount < totalAmount ? PaymentStatusPartial : PaymentStatusPaid;
    }

    public static decimal CalculateRemainingDue(decimal totalAmount, decimal paidAmount)
    {
        return Math.Max(0m, totalAmount - Math.Max(0m, paidAmount));
    }

    public sealed record LineAmounts(
        decimal SubTotal,
        decimal TaxAmount,
        decimal FinalAmount
    );

    public static LineAmounts CalculateLineAmounts(decimal unitPrice, decimal quantity, decimal taxRatePercent)
    {
        var price = Math.Max(0m, unitPrice);
        var qty = Math.Max(0m, quantity);
        var rate = Math.Max(0m, taxRatePercent);

        var subTotal = Math.Round(price * qty, 2, MidpointRounding.AwayFromZero);
        var effectiveRate = rate > 1m ? rate / 100m : rate;
        var taxAmount = Math.Round(subTotal * effectiveRate, 2, MidpointRounding.AwayFromZero);
        var finalAmount = subTotal + taxAmount;

        return new LineAmounts(subTotal, taxAmount, finalAmount);
    }

    public sealed record InvoiceTotals(
        decimal SubTotal,
        decimal TaxAmount,
        decimal DiscountAmount,
        decimal FinalAmount
    );

    public static InvoiceTotals CalculateInvoiceTotals(IEnumerable<LineAmounts> lines, decimal discountAmount = 0m)
    {
        var lineList = lines?.ToList() ?? new List<LineAmounts>();
        var subTotal = lineList.Sum(l => l.SubTotal);
        var taxAmount = lineList.Sum(l => l.TaxAmount);
        var discount = Math.Max(0m, discountAmount);
        var finalAmount = Math.Max(0m, subTotal + taxAmount - discount);

        return new InvoiceTotals(subTotal, taxAmount, discount, finalAmount);
    }

    public sealed record PaymentAllocationResult(
        decimal AllocatedAmount,
        decimal NewPaidAmount,
        decimal RemainingDue,
        string NewPaymentStatus
    );

    public static PaymentAllocationResult AllocatePaymentToInvoice(
        decimal invoiceTotal,
        decimal currentPaidAmount,
        decimal incomingPayment)
    {
        var total = Math.Max(0m, invoiceTotal);
        var currentPaid = Math.Max(0m, currentPaidAmount);
        var incoming = Math.Max(0m, incomingPayment);

        var remainingBefore = Math.Max(0m, total - currentPaid);
        var allocated = Math.Min(incoming, remainingBefore);
        var newPaid = currentPaid + incoming;
        var remainingAfter = Math.Max(0m, total - newPaid);
        var newStatus = ResolvePaymentStatus(newPaid, total);

        return new PaymentAllocationResult(allocated, newPaid, remainingAfter, newStatus);
    }
}
