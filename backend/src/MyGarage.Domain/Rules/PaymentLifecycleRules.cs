using System;
using System.Collections.Generic;

namespace MyGarage.Domain.Rules;

public static class PaymentLifecycleRules
{
    public const string StatusPending = "PENDING";
    public const string StatusProcessing = "PROCESSING";
    public const string StatusCompleted = "COMPLETED";
    public const string StatusFailed = "FAILED";
    public const string StatusCanceled = "CANCELED";

    public const string DirectionInflow = "INFLOW";
    public const string DirectionOutflow = "OUTFLOW";

    public static readonly HashSet<string> TerminalStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        StatusCompleted,
        StatusFailed,
        StatusCanceled
    };

    public static readonly HashSet<string> AllStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        StatusPending,
        StatusProcessing,
        StatusCompleted,
        StatusFailed,
        StatusCanceled
    };

    public static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return StatusPending;
        var s = status.Trim().ToUpperInvariant();
        return s switch
        {
            "PAID" => StatusCompleted,
            "SUCCESS" => StatusCompleted,
            "CANCELLED" => StatusCanceled,
            "ERROR" => StatusFailed,
            _ when AllStatuses.Contains(s) => s,
            _ => StatusPending
        };
    }

    public static bool IsTerminalStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        var norm = NormalizeStatus(status);
        return TerminalStatuses.Contains(norm);
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
            StatusPending => target is StatusProcessing or StatusCompleted or StatusFailed or StatusCanceled,
            StatusProcessing => target is StatusCompleted or StatusFailed or StatusCanceled,
            _ => false
        };
    }

    public static (bool IsValid, string? ErrorMessage) ValidateTransition(string? currentStatus, string? targetStatus)
    {
        if (string.IsNullOrWhiteSpace(targetStatus))
        {
            return (false, "Target status cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(currentStatus))
        {
            return (false, "Current status cannot be empty.");
        }

        var curr = NormalizeStatus(currentStatus);
        var target = NormalizeStatus(targetStatus);

        if (IsTerminalStatus(curr) && curr != target)
        {
            return (false, $"Cannot transition payment from immutable terminal status '{curr}'.");
        }

        if (!CanTransition(curr, target))
        {
            return (false, $"Invalid payment status transition from '{curr}' to '{target}'.");
        }

        return (true, null);
    }

    public static string NormalizeDirection(string? direction, long? invoiceId)
    {
        if (invoiceId.HasValue && invoiceId.Value > 0)
        {
            return DirectionInflow;
        }

        var normalized = (direction ?? string.Empty).Trim().ToUpperInvariant();
        return normalized is DirectionOutflow ? DirectionOutflow : DirectionInflow;
    }

    public static string NormalizeWebhookOutcome(string? providerStatus, decimal paidAmount, decimal expectedAmount)
    {
        var norm = (providerStatus ?? string.Empty).Trim().ToUpperInvariant();
        if (norm is "PAID" or "COMPLETED" or "SUCCESS")
        {
            return StatusCompleted;
        }

        if (norm is "CANCELED" or "CANCELLED")
        {
            return StatusCanceled;
        }

        if (norm is "FAILED" or "EXPIRED" or "ERROR")
        {
            return StatusFailed;
        }

        if (expectedAmount > 0m && paidAmount >= expectedAmount)
        {
            return StatusCompleted;
        }

        if (paidAmount > 0m && (expectedAmount <= 0m || paidAmount < expectedAmount))
        {
            return StatusProcessing;
        }

        return StatusPending;
    }

    public static bool IsDuplicateOrIdempotentWebhook(
        string? currentStatus,
        string? incomingStatus,
        decimal currentPaidAmount,
        decimal incomingPaidAmount)
    {
        var curr = NormalizeStatus(currentStatus);
        var incoming = NormalizeStatus(incomingStatus);

        // If current is already completed and incoming is also completed with same or lesser paid amount -> idempotent duplicate
        if (curr == StatusCompleted && incoming == StatusCompleted && incomingPaidAmount <= currentPaidAmount)
        {
            return true;
        }

        // If current is already terminal (Canceled/Failed) and incoming matches -> idempotent duplicate
        if (IsTerminalStatus(curr) && curr == incoming)
        {
            return true;
        }

        return false;
    }

    public sealed record RefundValidationResult(
        bool IsValid,
        string? ErrorMessage,
        decimal RemainingRefundable
    );

    public static RefundValidationResult ValidateRefund(
        decimal paidAmount,
        decimal totalPreviouslyRefunded,
        decimal requestedRefundAmount)
    {
        if (requestedRefundAmount <= 0m)
        {
            return new RefundValidationResult(false, "Số tiền hoàn phải lớn hơn 0.", 0m);
        }

        if (paidAmount <= 0m)
        {
            return new RefundValidationResult(false, "Giao dịch thanh toán gốc không có số tiền hợp lệ để hoàn.", 0m);
        }

        var availableRefundable = Math.Max(0m, paidAmount - Math.Max(0m, totalPreviouslyRefunded));
        if (requestedRefundAmount > availableRefundable)
        {
            return new RefundValidationResult(
                false,
                $"Số tiền hoàn ({requestedRefundAmount:N0} đ) vượt quá số dư khả dụng có thể hoàn ({availableRefundable:N0} đ).",
                availableRefundable);
        }

        var remaining = availableRefundable - requestedRefundAmount;
        return new RefundValidationResult(true, null, remaining);
    }
}
