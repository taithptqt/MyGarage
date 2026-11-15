using System;
using System.Collections.Generic;

namespace MyGarage.Domain.Appointments;

public static class AppointmentLifecycleRules
{
    public const int DefaultLateCancelThresholdMinutes = 60;

    public static readonly HashSet<string> ClosedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "CANCELED",
        "NO_SHOW",
        "COMPLETED"
    };

    public static readonly HashSet<string> QueueingStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "PENDING",
        "CONFIRMED",
        "LATE",
        "ARRIVED",
        "ARRIVED_LATE"
    };

    public static bool CanConfirm(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        var s = status.Trim();
        return string.Equals(s, "PENDING", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "LATE", StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanCancel(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        return !ClosedStatuses.Contains(status.Trim());
    }

    public static bool CanCheckIn(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        var s = status.Trim();
        return string.Equals(s, "CONFIRMED", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "LATE", StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanArrive(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        return !ClosedStatuses.Contains(status.Trim());
    }

    public static string? BuildCancelReason(DateTime startTime, string? reason, DateTime now, int lateCancelMinutes = DefaultLateCancelThresholdMinutes)
    {
        var effectiveReason = reason;
        if (startTime > now && (startTime - now).TotalMinutes < lateCancelMinutes)
        {
            effectiveReason = string.IsNullOrWhiteSpace(reason) ? "[LATE_CANCEL]" : $"[LATE_CANCEL] {reason}";
        }
        return effectiveReason;
    }
}
