using MyGarage.Enums;

namespace MyGarage.Support;

/// <summary>
/// Maps workshop / service-order state to customer-facing Vietnamese stages.
/// Handles both CANCELED and CANCELLED spellings.
/// </summary>
public static class CustomerPortalStageMapper
{
    public const string StageReceived = "RECEIVED";
    public const string StageInspecting = "INSPECTING";
    public const string StageQuote = "QUOTE";
    public const string StageRepairing = "REPAIRING";
    public const string StageWaitingParts = "WAITING_PARTS";
    public const string StageReady = "READY";
    public const string StageDelivered = "DELIVERED";
    public const string StageCanceled = "CANCELED";

    public static string ToVietnameseLabel(string stageCode) => stageCode switch
    {
        StageReceived => "Đã tiếp nhận",
        StageInspecting => "Đang kiểm tra",
        StageQuote => "Chờ báo giá / chờ duyệt",
        StageRepairing => "Đang sửa",
        StageWaitingParts => "Chờ phụ tùng",
        StageReady => "Sẵn sàng",
        StageDelivered => "Đã giao",
        StageCanceled => "Đã hủy",
        _ => "Đang xử lý"
    };

    public static bool IsCanceledStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        var u = status.Trim().ToUpperInvariant();
        return u is "CANCELED" or "CANCELLED";
    }

    public static bool IsTerminalDelivered(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        var u = status.Trim().ToUpperInvariant();
        return u is "CLOSED" or "DELIVERED";
    }

    public static bool IsActiveServiceOrderStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        if (IsCanceledStatus(status)) return false;
        var u = status.Trim().ToUpperInvariant();
        return u is not ("CLOSED" or "DELIVERED");
    }

    /// <summary>
    /// Active diagnosis: not completed, not cancelled decision.
    /// </summary>
    public static bool IsDiagnosisInProgress(string? diagnosisStatus, string? cancellationStatus)
    {
        if (string.IsNullOrWhiteSpace(diagnosisStatus)) return false;
        if (string.Equals(diagnosisStatus, nameof(DiagnosisStatus.COMPLETED), StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.IsNullOrWhiteSpace(cancellationStatus) &&
            !string.Equals(cancellationStatus, "NONE", StringComparison.OrdinalIgnoreCase))
            return false;
        return string.Equals(diagnosisStatus, nameof(DiagnosisStatus.PENDING), StringComparison.OrdinalIgnoreCase)
               || string.Equals(diagnosisStatus, nameof(DiagnosisStatus.ASSIGNED), StringComparison.OrdinalIgnoreCase);
    }

    public static string ResolveStage(
        string? orderStatus,
        bool hasActiveDiagnosis,
        bool hasWaitingPartsLine)
    {
        if (IsCanceledStatus(orderStatus))
            return StageCanceled;

        var o = orderStatus?.Trim().ToUpperInvariant() ?? "";

        if (o is "NEW"
            or "WAITING_RECEIVE"
            or "RECEIVED"
            or "CHECKED_IN"
            or nameof(ServiceOrderLifecycleStatus.CHECKED_IN))
            return StageReceived;

        if (o == nameof(ServiceOrderLifecycleStatus.WAITING))
            return StageReceived;

        if (hasActiveDiagnosis || o == nameof(ServiceOrderLifecycleStatus.DIAGNOSING))
            return StageInspecting;

        if (o == "WAITING_PARTS")
            return StageWaitingParts;

        if (o is nameof(ServiceOrderLifecycleStatus.QUOTING)
            or nameof(ServiceOrderLifecycleStatus.WAITING_APPROVAL)
            or nameof(ServiceOrderLifecycleStatus.APPROVED))
            return StageQuote;

        if (hasWaitingPartsLine)
            return StageWaitingParts;

        if (o is nameof(ServiceOrderLifecycleStatus.IN_PROGRESS)
            or nameof(ServiceOrderLifecycleStatus.REPAIRING)
            or "REPAIRING")
            return StageRepairing;

        if (o is nameof(ServiceOrderLifecycleStatus.READY)
            or nameof(ServiceOrderLifecycleStatus.WAITING_PAYMENT)
            or nameof(ServiceOrderDetailStatusCode.COMPLETED)
            or "COMPLETED")
            return StageReady;

        if (o is nameof(ServiceOrderLifecycleStatus.CLOSED) or "DELIVERED")
            return StageDelivered;

        return StageReceived;
    }

    public static IReadOnlyList<PortalTimelineStep> BuildTimeline(string currentStageCode, bool canceled)
    {
        var order = new[]
        {
            StageReceived,
            StageInspecting,
            StageQuote,
            StageRepairing,
            StageReady,
            StageDelivered
        };

        if (canceled || currentStageCode == StageCanceled)
        {
            return new[]
            {
                new PortalTimelineStep(StageCanceled, ToVietnameseLabel(StageCanceled), "current")
            };
        }

        var idx = currentStageCode switch
        {
            StageWaitingParts => Array.IndexOf(order, StageRepairing),
            _ => Array.IndexOf(order, currentStageCode)
        };
        if (idx < 0) idx = 0;

        return order.Select((code, i) =>
        {
            var state = i < idx ? "past" : i == idx ? "current" : "upcoming";
            var label = code == StageRepairing && currentStageCode == StageWaitingParts
                ? "Chờ phụ tùng"
                : ToVietnameseLabel(code);
            return new PortalTimelineStep(code, label, state);
        }).ToList();
    }
}

public sealed record PortalTimelineStep(string Code, string Label, string State);
