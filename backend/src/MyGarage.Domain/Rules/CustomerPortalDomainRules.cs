using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MyGarage.Domain.Rules;

public sealed record CustomerPortalTimelineStep(string Code, string Label, string State);

public static class CustomerPortalDomainRules
{
    public const string StageReceived = "RECEIVED";
    public const string StageInspecting = "INSPECTING";
    public const string StageQuote = "QUOTE";
    public const string StageRepairing = "REPAIRING";
    public const string StageWaitingParts = "WAITING_PARTS";
    public const string StageReady = "READY";
    public const string StageDelivered = "DELIVERED";
    public const string StageCanceled = "CANCELED";

    public const int MaxChatMessageLength = 4000;
    public const int MaxAttachmentsPerUpload = 6;
    public const int MaxAttachmentsPerMessage = 10;
    public const long MaxAttachmentFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public const string ConversationStatusOpen = "OPEN";
    public const string ConversationStatusWaitingForStaff = "WAITING_FOR_STAFF";
    public const string ConversationStatusWaitingForCustomer = "WAITING_FOR_CUSTOMER";
    public const string ConversationStatusInProgress = "IN_PROGRESS";
    public const string ConversationStatusClosed = "CLOSED";

    private static readonly HashSet<string> AllowedAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp",
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".csv",
        ".zip", ".rar"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif", "image/bmp",
        "application/pdf", "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "text/plain", "text/csv",
        "application/zip", "application/x-zip-compressed", "application/x-rar-compressed",
        "application/octet-stream"
    };

    private static readonly HashSet<string> ValidConversationStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        ConversationStatusOpen,
        ConversationStatusWaitingForStaff,
        ConversationStatusWaitingForCustomer,
        ConversationStatusInProgress,
        ConversationStatusClosed
    };

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
        return u == StageCanceled;
    }

    public static string ResolveStage(
        string? orderStatus,
        bool hasActiveDiagnosis,
        bool hasWaitingPartsLine)
    {
        if (IsCanceledStatus(orderStatus))
            return StageCanceled;

        var o = orderStatus?.Trim().ToUpperInvariant() ?? string.Empty;

        if (o is "NEW"
            or "WAITING_RECEIVE"
            or "RECEIVED"
            or "CHECKED_IN"
            or "WAITING")
            return StageReceived;

        if (hasActiveDiagnosis || o == "DIAGNOSING")
            return StageInspecting;

        if (o == "WAITING_PARTS")
            return StageWaitingParts;

        if (o is "QUOTING" or "WAITING_APPROVAL" or "APPROVED")
            return StageQuote;

        if (hasWaitingPartsLine)
            return StageWaitingParts;

        if (o is "IN_PROGRESS" or "REPAIRING")
            return StageRepairing;

        if (o is "READY" or "WAITING_PAYMENT" or "COMPLETED")
            return StageReady;

        if (o is "CLOSED" or "DELIVERED")
            return StageDelivered;

        return StageReceived;
    }

    public static IReadOnlyList<CustomerPortalTimelineStep> BuildTimeline(string currentStageCode, bool canceled)
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
                new CustomerPortalTimelineStep(StageCanceled, ToVietnameseLabel(StageCanceled), "current")
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
            return new CustomerPortalTimelineStep(code, label, state);
        }).ToList();
    }

    public static (bool IsValid, string? Error) ValidateCustomerVehicleInput(
        string? licensePlate,
        string? vin,
        string? color,
        int? manufactureYear)
    {
        var normPlate = VehicleDomainRules.NormalizePlate(licensePlate);
        if (string.IsNullOrWhiteSpace(normPlate))
        {
            return (false, "Biển số xe không được để trống.");
        }

        if (!VehicleDomainRules.IsValidPlate(normPlate))
        {
            return (false, "Biển số xe không đúng định dạng chuẩn.");
        }

        var normVin = VehicleDomainRules.NormalizeVin(vin);
        if (!string.IsNullOrEmpty(normVin) && !VehicleDomainRules.IsValidVin(normVin))
        {
            return (false, "Số VIN phải gồm đúng 17 ký tự chữ và số (không chứa I, O, Q).");
        }

        if (manufactureYear.HasValue && !VehicleDomainRules.ValidateManufactureYear(manufactureYear))
        {
            return (false, "Năm sản xuất không hợp lệ.");
        }

        return (true, null);
    }

    public static (bool IsValid, string? Error) ValidateChatMessage(
        string? content,
        IReadOnlyCollection<string>? attachmentUrls,
        bool hasOrderCard)
    {
        var trimmedContent = content?.Trim();
        var hasContent = !string.IsNullOrEmpty(trimmedContent);
        var attachmentCount = attachmentUrls?.Count ?? 0;
        var hasAttachments = attachmentCount > 0;

        if (!hasContent && !hasAttachments && !hasOrderCard)
        {
            return (false, "Tin nhắn phải có nội dung văn bản, tệp đính kèm hoặc thẻ đơn dịch vụ.");
        }

        if (hasOrderCard && hasAttachments)
        {
            return (false, "Thẻ đơn dịch vụ không được kèm tệp đính kèm.");
        }

        if (hasContent && trimmedContent!.Length > MaxChatMessageLength)
        {
            return (false, $"Nội dung tin nhắn không được vượt quá {MaxChatMessageLength} ký tự.");
        }

        if (attachmentCount > MaxAttachmentsPerMessage)
        {
            return (false, $"Tin nhắn không được đính kèm quá {MaxAttachmentsPerMessage} tệp.");
        }

        return (true, null);
    }

    public static bool IsValidAttachmentExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return false;
        var normalized = extension.Trim();
        if (!normalized.StartsWith("."))
        {
            normalized = "." + normalized;
        }
        return AllowedAttachmentExtensions.Contains(normalized);
    }

    public static bool IsValidAttachmentContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return false;
        return AllowedContentTypes.Contains(contentType.Trim());
    }

    public static bool IsValidAttachmentFileSize(long fileSizeBytes)
    {
        return fileSizeBytes > 0 && fileSizeBytes <= MaxAttachmentFileSizeBytes;
    }

    public static bool CanTransitionConversationStatus(string? currentStatus, string? targetStatus, bool isStaff)
    {
        if (string.IsNullOrWhiteSpace(targetStatus)) return false;
        var normTarget = targetStatus.Trim().ToUpperInvariant();
        if (!ValidConversationStatuses.Contains(normTarget)) return false;

        var normCurrent = currentStatus?.Trim().ToUpperInvariant() ?? ConversationStatusOpen;

        if (string.Equals(normCurrent, normTarget, StringComparison.OrdinalIgnoreCase))
        {
            return true; // Idempotent
        }

        if (isStaff)
        {
            // Staff can transition across operational statuses
            return true;
        }

        // Customer can reopen a closed conversation or indicate message sent
        if (normTarget == ConversationStatusWaitingForStaff)
        {
            return true;
        }

        return false;
    }
}
