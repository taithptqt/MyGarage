using System;
using System.Collections.Generic;
using System.Linq;

namespace MyGarage.Domain.Rules;

public static class ServiceOrderLifecycleRules
{
    // Terminal statuses (cannot transition to any other status)
    public static readonly HashSet<string> TerminalStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "CLOSED",
        "CANCELED"
    };

    // Milestone locked statuses (workshop modifications not allowed)
    public static readonly HashSet<string> MilestoneLockedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "READY",
        "WAITING_PAYMENT",
        "CLOSED",
        "CANCELED"
    };

    // Active execution statuses (workshop in progress)
    public static readonly HashSet<string> ActiveExecutionStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "CHECKED_IN",
        "WAITING",
        "APPROVED",
        "WAITING_PARTS",
        "IN_PROGRESS",
        "REPAIRING"
    };

    public static bool IsTerminalStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        return TerminalStatuses.Contains(status.Trim());
    }

    public static bool CanEditWorkshop(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        return !MilestoneLockedStatuses.Contains(status.Trim());
    }

    public static bool CanTransition(string? currentStatus, string? targetStatus)
    {
        if (string.IsNullOrWhiteSpace(currentStatus) || string.IsNullOrWhiteSpace(targetStatus))
            return false;

        var curr = currentStatus.Trim().ToUpperInvariant();
        var target = targetStatus.Trim().ToUpperInvariant();

        if (TerminalStatuses.Contains(curr))
            return false; // Terminal states cannot be changed

        if (curr == target)
            return true; // No-op transition is allowed

        return curr switch
        {
            "NEW" => target is "CHECKED_IN" or "CANCELED",
            "CHECKED_IN" => target is "DIAGNOSING" or "QUOTING" or "APPROVED" or "IN_PROGRESS" or "CANCELED",
            "DIAGNOSING" => target is "QUOTING" or "CANCELED",
            "QUOTING" => target is "WAITING_APPROVAL" or "APPROVED" or "CANCELED",
            "WAITING_APPROVAL" => target is "APPROVED" or "QUOTING" or "CANCELED",
            "APPROVED" => target is "WAITING_PARTS" or "IN_PROGRESS" or "REPAIRING" or "CANCELED",
            "WAITING_PARTS" => target is "IN_PROGRESS" or "REPAIRING" or "CANCELED",
            "IN_PROGRESS" => target is "REPAIRING" or "WAITING_PARTS" or "READY" or "CANCELED",
            "REPAIRING" => target is "READY" or "WAITING_PARTS" or "CANCELED",
            "READY" => target is "REPAIRING" /* QC Fail */ or "WAITING_PAYMENT" or "CLOSED" or "CANCELED",
            "WAITING_PAYMENT" => target is "CLOSED" or "READY" or "CANCELED",
            _ => false
        };
    }

    // Handover Readiness Gatekeeper
    public sealed record HandoverEvaluationInput(
        string? OrderStatus,
        string? PaymentStatus,
        string? QcStatus,
        IReadOnlyList<string> WorkDetailStatuses,
        bool RequiresOldPartsReturn = false,
        bool IsOldPartsReturned = false
    );

    public sealed record HandoverEvaluationResult(
        bool CanHandover,
        string? BlockingReason
    );

    public static HandoverEvaluationResult EvaluateHandoverReadiness(HandoverEvaluationInput input)
    {
        if (TerminalStatuses.Contains(input.OrderStatus ?? string.Empty))
            return new HandoverEvaluationResult(false, "Lệnh sửa chữa đã đóng hoặc đã hủy.");

        var isPaid = string.Equals(input.PaymentStatus, "PAID", StringComparison.OrdinalIgnoreCase);
        if (!isPaid)
            return new HandoverEvaluationResult(false, "Đơn hàng chưa được thanh toán đầy đủ (PaymentStatus != PAID).");

        var isQcPass = string.Equals(input.QcStatus, "PASS", StringComparison.OrdinalIgnoreCase);
        if (!isQcPass)
            return new HandoverEvaluationResult(false, "Chưa hoàn tất kiểm tra chất lượng hoặc QC không đạt (QcStatus != PASS).");

        var allTasksDone = input.WorkDetailStatuses != null &&
                           (input.WorkDetailStatuses.Count == 0 ||
                            input.WorkDetailStatuses.All(s => string.Equals(s, "COMPLETED", StringComparison.OrdinalIgnoreCase)));
        if (!allTasksDone)
            return new HandoverEvaluationResult(false, "Vẫn còn các hạng mục sửa chữa/nhân công chưa hoàn thành.");

        if (input.RequiresOldPartsReturn && !input.IsOldPartsReturned)
            return new HandoverEvaluationResult(false, "Khách yêu cầu lấy lại phụ tùng cũ nhưng chưa đóng gói bàn giao.");

        return new HandoverEvaluationResult(true, null);
    }

    // Bay Allocation Validation Rules
    public sealed record BayAllocationInput(
        long BayBranchId,
        long OrderExecuteBranchId,
        string? BayOperationalStatus,
        decimal? BayMaxTonnage,
        decimal? VehicleTonnage,
        bool IsBayCurrentlyOccupied
    );

    public sealed record BayAllocationValidationResult(
        bool IsValid,
        string? ErrorMessage
    );

    public static BayAllocationValidationResult ValidateBayAllocation(BayAllocationInput input)
    {
        if (input.BayBranchId != input.OrderExecuteBranchId)
            return new BayAllocationValidationResult(false, "Khoang sửa chữa không thuộc cùng chi nhánh thực hiện của lệnh.");

        if (!string.Equals(input.BayOperationalStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            return new BayAllocationValidationResult(false, $"Khoang không khả dụng (Trạng thái: {input.BayOperationalStatus ?? "UNKNOWN"}).");

        if (input.IsBayCurrentlyOccupied)
            return new BayAllocationValidationResult(false, "Khoang đang có phương tiện khác chiếm chỗ.");

        if (input.BayMaxTonnage.HasValue && input.VehicleTonnage.HasValue && input.VehicleTonnage.Value > input.BayMaxTonnage.Value)
            return new BayAllocationValidationResult(false, $"Tải trọng xe ({input.VehicleTonnage.Value}T) vượt quá giới hạn tải trọng khoang ({input.BayMaxTonnage.Value}T).");

        return new BayAllocationValidationResult(true, null);
    }

    // Technician Qualification & Assignment Rules
    public static bool IsTechnicianQualified(string? technicianSpecialty, string? requiredSpecialty)
    {
        if (string.IsNullOrWhiteSpace(requiredSpecialty) || string.Equals(requiredSpecialty.Trim(), "GENERAL", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.IsNullOrWhiteSpace(technicianSpecialty))
            return false;

        var tech = technicianSpecialty.Trim().ToUpperInvariant();
        var req = requiredSpecialty.Trim().ToUpperInvariant();

        if (tech == "MASTER" || tech == "ALL")
            return true;

        return tech.Contains(req, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ValidateSplitRatios(IEnumerable<decimal>? splitRatios)
    {
        var list = splitRatios?.ToList() ?? new List<decimal>();
        if (list.Count == 0) return false;
        if (list.Any(r => r <= 0m || r > 1.0m)) return false;
        var sum = list.Sum();
        return Math.Abs(sum - 1.0m) <= 0.001m;
    }
}
