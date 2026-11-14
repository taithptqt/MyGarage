using System;
using System.Collections.Generic;
using System.IO;

namespace MyGarage.Domain.Rules;

public static class AiDomainRules
{
    public const int MinForecastDays = 1;
    public const int MaxForecastDays = 365;

    public const int MaxKnowledgeTitleLength = 250;
    public const int MaxKnowledgeContentLength = 20000;

    public const decimal MinTemperature = 0m;
    public const decimal MaxTemperature = 2m;
    public const int MinMaxTokens = 1;
    public const int MaxMaxTokens = 128000;

    public const string DispatchStatusPending = "PENDING";
    public const string DispatchStatusApplied = "APPLIED";
    public const string DispatchStatusRejected = "REJECTED";
    public const string DispatchStatusExpired = "EXPIRED";

    public const string SessionStatusActive = "ACTIVE";
    public const string SessionStatusHandedOff = "HANDED_OFF";
    public const string SessionStatusLocked = "LOCKED";
    public const string SessionStatusClosed = "CLOSED";

    public const string CustomerMessageRole = "USER";
    public const long MaxChatbotImageSizeBytes = 8 * 1024 * 1024; // 8 MB

    private static readonly HashSet<string> ValidProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "OPENAI",
        "GEMINI",
        "AZURE_OPENAI",
        "ANTHROPIC",
        "LOCAL"
    };

    private static readonly HashSet<string> ValidDispatchStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        DispatchStatusPending,
        DispatchStatusApplied,
        DispatchStatusRejected,
        DispatchStatusExpired
    };

    private static readonly HashSet<string> ValidChatbotSessionStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        SessionStatusActive,
        SessionStatusHandedOff,
        SessionStatusLocked,
        SessionStatusClosed
    };

    private static readonly HashSet<string> AllowedChatbotImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    // =========================================================================
    // 1. AI Forecast Rules
    // =========================================================================

    public static bool IsValidForecastDays(int forecastDays)
    {
        return forecastDays >= MinForecastDays && forecastDays <= MaxForecastDays;
    }

    public static bool IsValidConfidenceScore(decimal confidenceScore)
    {
        return confidenceScore >= 0m && confidenceScore <= 1m;
    }

    public static bool IsValidConfidenceScore(decimal? confidenceScore)
    {
        return !confidenceScore.HasValue || IsValidConfidenceScore(confidenceScore.Value);
    }

    public static int CalculateRecommendedOrderQuantity(int currentStock, int forecastDemand, int safetyStock)
    {
        return Math.Max(0, forecastDemand + safetyStock - Math.Max(0, currentStock));
    }

    // =========================================================================
    // 2. AI Knowledge Base Rules
    // =========================================================================

    public static (bool IsValid, string? Error) ValidateKnowledgeEntry(
        string? title,
        string? content,
        string? category = "GENERAL")
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return (false, "Tiêu đề tri thức không được để trống.");
        }

        var trimmedTitle = title.Trim();
        if (trimmedTitle.Length > MaxKnowledgeTitleLength)
        {
            return (false, $"Tiêu đề tri thức không được vượt quá {MaxKnowledgeTitleLength} ký tự.");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return (false, "Nội dung tri thức không được để trống.");
        }

        var trimmedContent = content.Trim();
        if (trimmedContent.Length > MaxKnowledgeContentLength)
        {
            return (false, $"Nội dung tri thức không được vượt quá {MaxKnowledgeContentLength} ký tự.");
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            return (false, "Danh mục tri thức không được để trống.");
        }

        return (true, null);
    }

    // =========================================================================
    // 3. AI Model Config Rules
    // =========================================================================

    public static bool IsValidProvider(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider)) return false;
        return ValidProviders.Contains(provider.Trim());
    }

    public static bool IsValidTemperature(decimal temperature)
    {
        return temperature >= MinTemperature && temperature <= MaxTemperature;
    }

    public static bool IsValidTemperature(decimal? temperature)
    {
        return !temperature.HasValue || IsValidTemperature(temperature.Value);
    }

    public static bool IsValidMaxTokens(int maxTokens)
    {
        return maxTokens >= MinMaxTokens && maxTokens <= MaxMaxTokens;
    }

    public static bool IsValidMaxTokens(int? maxTokens)
    {
        return !maxTokens.HasValue || IsValidMaxTokens(maxTokens.Value);
    }

    public static (bool IsValid, string? Error) ValidateModelConfig(
        string? provider,
        string? modelName,
        decimal? temperature,
        int? maxTokens)
    {
        if (!IsValidProvider(provider))
        {
            return (false, "Nhà cung cấp mô hình AI không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(modelName))
        {
            return (false, "Tên mô hình AI không được để trống.");
        }

        if (!IsValidTemperature(temperature))
        {
            return (false, $"Tham số temperature phải nằm trong khoảng {MinTemperature} đến {MaxTemperature}.");
        }

        if (!IsValidMaxTokens(maxTokens))
        {
            return (false, $"Tham số maxTokens phải nằm trong khoảng {MinMaxTokens} đến {MaxMaxTokens}.");
        }

        return (true, null);
    }

    public static string? MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var trimmed = apiKey.Trim();
        if (trimmed.Length < 8)
        {
            return "****";
        }

        return "****" + trimmed[^4..];
    }

    // =========================================================================
    // 4. AI Smart Dispatching Rules
    // =========================================================================

    public static bool IsValidDispatchStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        return ValidDispatchStatuses.Contains(status.Trim());
    }

    public static bool CanTransitionDispatchStatus(string? currentStatus, string? targetStatus)
    {
        if (!IsValidDispatchStatus(targetStatus))
        {
            return false;
        }

        var normTarget = targetStatus!.Trim().ToUpperInvariant();
        var normCurrent = string.IsNullOrWhiteSpace(currentStatus)
            ? DispatchStatusPending
            : currentStatus.Trim().ToUpperInvariant();

        if (!IsValidDispatchStatus(normCurrent))
        {
            return false;
        }

        if (string.Equals(normCurrent, normTarget, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normCurrent == DispatchStatusPending)
        {
            return normTarget is DispatchStatusApplied
                or DispatchStatusRejected
                or DispatchStatusExpired;
        }

        return false;
    }

    // =========================================================================
    // 5. Chatbot Rules
    // =========================================================================

    public static bool IsValidChatbotSessionStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        return ValidChatbotSessionStatuses.Contains(status.Trim());
    }

    public static bool CanCustomerSendMessageToSession(string? sessionStatus, bool allowSendToLockedSession = false)
    {
        if (!IsValidChatbotSessionStatus(sessionStatus))
        {
            return false;
        }

        var norm = sessionStatus!.Trim().ToUpperInvariant();
        if (norm == SessionStatusActive)
        {
            return true;
        }

        if (norm == SessionStatusLocked)
        {
            return allowSendToLockedSession;
        }

        return false;
    }

    public static bool IsValidCustomerMessageRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        return string.Equals(role.Trim(), CustomerMessageRole, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsValidChatbotImageExtension(string? fileNameOrExtension)
    {
        if (string.IsNullOrWhiteSpace(fileNameOrExtension)) return false;
        var trimmed = fileNameOrExtension.Trim();
        var ext = Path.GetExtension(trimmed);
        if (string.IsNullOrEmpty(ext))
        {
            ext = trimmed.StartsWith(".") ? trimmed : "." + trimmed;
        }

        return AllowedChatbotImageExtensions.Contains(ext);
    }

    public static bool IsValidChatbotImageSize(long fileSizeBytes)
    {
        return fileSizeBytes >= 1 && fileSizeBytes <= MaxChatbotImageSizeBytes;
    }

    public static (bool IsValid, string? Error) ValidateChatbotImageUpload(string? fileNameOrExtension, long fileSizeBytes)
    {
        if (!IsValidChatbotImageExtension(fileNameOrExtension))
        {
            return (false, "Định dạng ảnh không hợp lệ (chỉ chấp nhận .jpg, .jpeg, .png, .webp).");
        }

        if (!IsValidChatbotImageSize(fileSizeBytes))
        {
            return (false, "Kích thước ảnh phải từ 1 byte đến tối đa 8MB.");
        }

        return (true, null);
    }
}
