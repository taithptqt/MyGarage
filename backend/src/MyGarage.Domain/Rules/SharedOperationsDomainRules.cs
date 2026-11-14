using System;
using System.Collections.Generic;

namespace MyGarage.Domain.Rules;

public static class SharedOperationsDomainRules
{
    // =========================================================================
    // 1. GatePass Rules
    // =========================================================================

    public const string StatusPending = "PENDING";
    public const string StatusApproved = "APPROVED";
    public const string StatusScanned = "SCANNED";
    public const string StatusCanceled = "CANCELED";

    private static readonly HashSet<string> ValidGatePassStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        StatusPending,
        StatusApproved,
        StatusScanned,
        StatusCanceled
    };

    public static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return string.Empty;
        }

        var normalized = status
            .Trim()
            .ToUpperInvariant()
            .Replace("-", "_")
            .Replace(" ", "_");

        return normalized == "CANCELLED" ? StatusCanceled : normalized;
    }

    public static bool IsGatePassFinalized(string? status, DateTime? passedAt)
    {
        if (passedAt.HasValue)
        {
            return true;
        }

        return string.Equals(NormalizeStatus(status), StatusScanned, StringComparison.OrdinalIgnoreCase);
    }

    public static (bool IsValid, string? ErrorMessage) ValidateGatePassMutation(
        string? existingStatus,
        DateTime? passedAt,
        bool isServiceOrderClosedLocked)
    {
        if (IsGatePassFinalized(existingStatus, passedAt))
        {
            return (false, "Không thể chỉnh sửa phiếu xuất cổng vì xe đã được xác nhận ra cổng.");
        }

        if (isServiceOrderClosedLocked)
        {
            return (false, "Không thể chỉnh sửa phiếu xuất cổng vì service order đã đóng.");
        }

        return (true, null);
    }

    public static bool CanTransitionGatePassStatus(string? currentStatus, string? targetStatus)
    {
        var normTarget = NormalizeStatus(targetStatus);
        if (string.IsNullOrEmpty(normTarget) || !ValidGatePassStatuses.Contains(normTarget))
        {
            return false;
        }

        var normCurrent = NormalizeStatus(currentStatus);
        if (string.IsNullOrEmpty(normCurrent))
        {
            normCurrent = StatusPending;
        }

        if (!ValidGatePassStatuses.Contains(normCurrent))
        {
            return false;
        }

        if (string.Equals(normCurrent, normTarget, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return normCurrent switch
        {
            StatusPending => normTarget is StatusApproved or StatusScanned or StatusCanceled,
            StatusApproved => normTarget is StatusScanned or StatusCanceled,
            StatusScanned or StatusCanceled => false,
            _ => false
        };
    }

    // =========================================================================
    // 2. ApprovalRequest Rules
    // =========================================================================

    public const string ApprovalPending = "PENDING";
    public const string ApprovalApproved = "APPROVED";
    public const string ApprovalRejected = "REJECTED";
    public const string ApprovalCanceled = "CANCELED";

    private static readonly HashSet<string> ValidApprovalStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        ApprovalPending,
        ApprovalApproved,
        ApprovalRejected,
        ApprovalCanceled
    };

    public static bool CanTransitionApprovalStatus(string? currentStatus, string? targetStatus)
    {
        var normTarget = NormalizeStatus(targetStatus);
        if (string.IsNullOrEmpty(normTarget) || !ValidApprovalStatuses.Contains(normTarget))
        {
            return false;
        }

        var normCurrent = NormalizeStatus(currentStatus);
        if (string.IsNullOrEmpty(normCurrent))
        {
            normCurrent = ApprovalPending;
        }

        if (!ValidApprovalStatuses.Contains(normCurrent))
        {
            return false;
        }

        if (string.Equals(normCurrent, normTarget, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normCurrent == ApprovalPending)
        {
            return normTarget is ApprovalApproved or ApprovalRejected or ApprovalCanceled;
        }

        return false;
    }

    public static (bool IsValid, string? ErrorMessage) ValidateApprovalRequestInput(
        string? requestType,
        long? targetId,
        string? reason)
    {
        if (string.IsNullOrWhiteSpace(requestType))
        {
            return (false, "Loại yêu cầu phê duyệt không được để trống.");
        }

        if (!targetId.HasValue || targetId.Value <= 0)
        {
            return (false, "Mã đối tượng phê duyệt không hợp lệ.");
        }

        return (true, null);
    }

    // =========================================================================
    // 3. AuditLog & Security Rules
    // =========================================================================

    private static readonly HashSet<string> SensitiveAuditFieldTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "passwordhash",
        "salt",
        "apikey",
        "securitystamp",
        "token",
        "secret",
        "otp"
    };

    public static (int Page, int PageSize) NormalizeAuditPageSize(
        int page,
        int pageSize,
        int defaultSize = 50,
        int maxSize = 200)
    {
        var normalizedPage = Math.Max(1, page);
        var safeMax = Math.Max(1, maxSize);
        var safeDefault = Math.Clamp(defaultSize <= 0 ? 50 : defaultSize, 1, safeMax);
        var rawSize = pageSize <= 0 ? safeDefault : pageSize;
        var normalizedSize = Math.Clamp(rawSize, 1, safeMax);

        return (normalizedPage, normalizedSize);
    }

    public static bool IsSensitiveAuditField(string? fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return false;
        }

        var normalized = fieldName
            .Trim()
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);

        if (SensitiveAuditFieldTokens.Contains(normalized))
        {
            return true;
        }

        foreach (var token in SensitiveAuditFieldTokens)
        {
            if (normalized.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
