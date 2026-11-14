using System;
using System.Collections.Generic;

namespace MyGarage.Domain.Rules;

public static class BayLifecycleRules
{
    public static readonly HashSet<string> ValidOperationalStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ACTIVE",
        "AVAILABLE",
        "IN_USE",
        "INUSE",
        "UNDER_MAINTENANCE",
        "MAINTENANCE",
        "BROKEN",
        "SUSPENDED"
    };

    public static string NormalizeStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status)) return string.Empty;
        var s = status.Trim().ToUpperInvariant();
        if (s == "INUSE") return "IN_USE";
        if (s == "MAINTENANCE") return "UNDER_MAINTENANCE";
        if (s == "AVAILABLE") return "ACTIVE";
        return s;
    }

    public static bool CanTransitionStatus(string? currentStatus, string? targetStatus)
    {
        if (string.IsNullOrWhiteSpace(currentStatus) || string.IsNullOrWhiteSpace(targetStatus))
            return false;

        var curr = NormalizeStatus(currentStatus);
        var target = NormalizeStatus(targetStatus);

        if (!ValidOperationalStatuses.Contains(curr) || !ValidOperationalStatuses.Contains(target))
            return false;

        if (curr == target) return true;

        return curr switch
        {
            "ACTIVE" => target is "IN_USE" or "UNDER_MAINTENANCE" or "BROKEN" or "SUSPENDED",
            "IN_USE" => target is "ACTIVE" or "UNDER_MAINTENANCE" or "BROKEN",
            "UNDER_MAINTENANCE" => target is "ACTIVE" or "BROKEN",
            "BROKEN" => target is "UNDER_MAINTENANCE" or "ACTIVE",
            "SUSPENDED" => target is "ACTIVE",
            _ => false
        };
    }

    public sealed record BayAllocationValidationInput(
        long BayBranchId,
        long ServiceOrderExecuteBranchId,
        string? BayOperationalStatus,
        bool IsCurrentlyOccupied,
        long? CurrentOccupiedVehicleId,
        decimal? BayMaxTonnage,
        decimal? VehicleTonnage
    );

    public sealed record BayAllocationValidationResult(
        bool IsValid,
        string? ErrorMessage
    );

    public static BayAllocationValidationResult ValidateAllocation(BayAllocationValidationInput input)
    {
        if (input.BayBranchId != input.ServiceOrderExecuteBranchId)
            return new BayAllocationValidationResult(false, "Khoang sửa chữa không thuộc cùng chi nhánh thực hiện của lệnh sửa chữa.");

        var normalizedStatus = NormalizeStatus(input.BayOperationalStatus ?? string.Empty);
        if (normalizedStatus != "ACTIVE")
            return new BayAllocationValidationResult(false, $"Khoang không ở trạng thái sẵn sàng (Trạng thái hiện tại: {input.BayOperationalStatus ?? "UNKNOWN"}).");

        if (input.IsCurrentlyOccupied || input.CurrentOccupiedVehicleId.HasValue)
            return new BayAllocationValidationResult(false, "Khoang đang có phương tiện khác chiếm chỗ.");

        if (input.BayMaxTonnage.HasValue && input.VehicleTonnage.HasValue && input.VehicleTonnage.Value > input.BayMaxTonnage.Value)
            return new BayAllocationValidationResult(false, $"Tải trọng xe ({input.VehicleTonnage.Value:N1}T) vượt quá giới hạn cho phép của khoang ({input.BayMaxTonnage.Value:N1}T).");

        return new BayAllocationValidationResult(true, null);
    }

    public static bool CanReleaseBay(string? operationalStatus, bool hasActiveAllocation)
    {
        if (!hasActiveAllocation) return false;
        var norm = NormalizeStatus(operationalStatus ?? string.Empty);
        return norm is "IN_USE" or "ACTIVE";
    }

    public sealed record BayTransferInput(
        long SourceBayBranchId,
        long TargetBayBranchId,
        long VehicleId,
        long? SourceCurrentVehicleId,
        string? TargetBayOperationalStatus,
        bool IsTargetOccupied,
        decimal? TargetBayMaxTonnage,
        decimal? VehicleTonnage
    );

    public static BayAllocationValidationResult ValidateTransfer(BayTransferInput input)
    {
        if (input.SourceBayBranchId != input.TargetBayBranchId)
            return new BayAllocationValidationResult(false, "Không thể chuyển phương tiện giữa hai khoang ở khác chi nhánh.");

        if (input.SourceCurrentVehicleId != input.VehicleId)
            return new BayAllocationValidationResult(false, "Phương tiện không khớp với xe đang ở khoang gốc.");

        var targetNorm = NormalizeStatus(input.TargetBayOperationalStatus ?? string.Empty);
        if (targetNorm != "ACTIVE")
            return new BayAllocationValidationResult(false, $"Khoang đích không ở trạng thái sẵn sàng (Trạng thái: {input.TargetBayOperationalStatus ?? "UNKNOWN"}).");

        if (input.IsTargetOccupied)
            return new BayAllocationValidationResult(false, "Khoang đích hiện đang bị chiếm chỗ.");

        if (input.TargetBayMaxTonnage.HasValue && input.VehicleTonnage.HasValue && input.VehicleTonnage.Value > input.TargetBayMaxTonnage.Value)
            return new BayAllocationValidationResult(false, $"Tải trọng xe ({input.VehicleTonnage.Value:N1}T) vượt quá giới hạn của khoang đích ({input.TargetBayMaxTonnage.Value:N1}T).");

        return new BayAllocationValidationResult(true, null);
    }
}
