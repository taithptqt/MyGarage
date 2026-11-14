using System;
using System.Collections.Generic;

namespace MyGarage.Domain.Rules;

public static class NotificationDomainRules
{
    public const string StatusScheduled = "SCHEDULED";
    public const string StatusSent = "SENT";
    public const string StatusConfirmed = "CONFIRMED";
    public const string StatusDeclined = "DECLINED";
    public const string StatusExpired = "EXPIRED";
    public const string StatusCancelled = "CANCELLED";

    public const string ChannelEmail = "EMAIL";
    public const string ChannelInApp = "IN_APP";
    public const string ChannelSms = "SMS";
    public const string ChannelZalo = "ZALO";

    private static readonly HashSet<string> ValidReminderStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        StatusScheduled,
        StatusSent,
        StatusConfirmed,
        StatusDeclined,
        StatusExpired,
        StatusCancelled
    };

    private static readonly HashSet<string> ValidReminderChannels = new(StringComparer.OrdinalIgnoreCase)
    {
        ChannelEmail,
        ChannelInApp,
        ChannelSms,
        ChannelZalo
    };

    private static readonly string[] ApprovedLinkActionPrefixes =
    {
        "/my-garage-dashboard",
        "/my-vehicles",
        "/my-notifications",
        "/my-appointments",
        "/public/quotations/",
        "/my-feedbacks"
    };

    public static bool CanTransitionNotificationReadState(bool? currentRead, bool targetRead)
    {
        // Monotonic rule: once read (true), cannot be transitioned back to unread (false)
        if (currentRead == true && !targetRead)
        {
            return false;
        }

        return true;
    }

    public static bool SanitizeAndValidateLinkAction(string? linkAction, out string? sanitizedLink)
    {
        if (string.IsNullOrWhiteSpace(linkAction))
        {
            sanitizedLink = null;
            return true;
        }

        var trimmed = linkAction.Trim();

        // Must be a relative path starting with '/'
        if (!trimmed.StartsWith("/"))
        {
            sanitizedLink = null;
            return false;
        }

        // Prevent protocol-relative URLs (//evil.com) and suspicious schemes
        if (trimmed.StartsWith("//") ||
            trimmed.Contains("://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("/javascript:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("/data:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("/vbscript:", StringComparison.OrdinalIgnoreCase))
        {
            sanitizedLink = null;
            return false;
        }

        foreach (var prefix in ApprovedLinkActionPrefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                sanitizedLink = trimmed;
                return true;
            }
        }

        sanitizedLink = null;
        return false;
    }

    public static string BuildMaintenanceNotificationKey(long customerId, long vehicleId, string semanticToken)
    {
        if (customerId <= 0)
            throw new ArgumentOutOfRangeException(nameof(customerId), "Mã khách hàng phải lớn hơn 0.");
        if (vehicleId <= 0)
            throw new ArgumentOutOfRangeException(nameof(vehicleId), "Mã xe phải lớn hơn 0.");

        var token = string.IsNullOrWhiteSpace(semanticToken) ? "DEFAULT" : semanticToken.Trim().ToUpperInvariant();
        return $"NOTIF:MAINT:{customerId}:{vehicleId}:{token}";
    }

    public static string BuildAppointmentReminderNotificationKey(long customerId, long appointmentId, string reminderType)
    {
        if (customerId <= 0)
            throw new ArgumentOutOfRangeException(nameof(customerId), "Mã khách hàng phải lớn hơn 0.");
        if (appointmentId <= 0)
            throw new ArgumentOutOfRangeException(nameof(appointmentId), "Mã lịch hẹn phải lớn hơn 0.");

        var type = string.IsNullOrWhiteSpace(reminderType) ? "STANDARD" : reminderType.Trim().ToUpperInvariant();
        return $"NOTIF:APPT:{customerId}:{appointmentId}:{type}";
    }

    public static bool CanTransitionReminderStatus(string? currentStatus, string? targetStatus)
    {
        if (string.IsNullOrWhiteSpace(targetStatus)) return false;
        var normTarget = targetStatus.Trim().ToUpperInvariant();
        if (!ValidReminderStatuses.Contains(normTarget)) return false;

        var normCurrent = currentStatus?.Trim().ToUpperInvariant() ?? StatusScheduled;

        if (string.Equals(normCurrent, normTarget, StringComparison.OrdinalIgnoreCase))
        {
            return true; // Idempotent
        }

        return normCurrent switch
        {
            StatusScheduled => normTarget is StatusSent or StatusCancelled,
            StatusSent => normTarget is StatusConfirmed or StatusDeclined or StatusExpired or StatusCancelled,
            StatusConfirmed or StatusDeclined or StatusExpired or StatusCancelled => false,
            _ => false
        };
    }

    public static bool IsReminderDue(DateTime scheduledAt, string? status, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        var normStatus = status.Trim().ToUpperInvariant();
        return normStatus == StatusScheduled && scheduledAt <= now;
    }

    public static bool IsValidReminderChannel(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel)) return false;
        return ValidReminderChannels.Contains(channel.Trim().ToUpperInvariant());
    }
}
