using MyGarage.Application.Interfaces.Stores.Common;
using MyGarage.Models;

namespace MyGarage.Application.Bays;

internal static class BayAdminSettingsHelper
{
    private const string SettingsFileName = "bay-admin-settings.json";

    public static async Task<BayAdminSettings> ReadSettingsAsync(
        IAppConfigFileStore configStore,
        CancellationToken ct = default)
    {
        var settings = await configStore.ReadStrictOrDefaultAsync(SettingsFileName, BayAdminSettings.CreateDefault, ct);
        settings.Normalize();
        return settings;
    }

    public static Task WriteSettingsAsync(
        IAppConfigFileStore configStore,
        BayAdminSettings settings,
        CancellationToken ct = default)
    {
        settings.Normalize();
        settings.UpdatedAt = VietnamTime.Now;
        return configStore.WriteAsync(SettingsFileName, settings, ct);
    }

    public static BayMetadata ResolveMetadata(BayAdminSettings settings, long bayId)
    {
        if (!settings.BayMetadata.TryGetValue(bayId, out var metadata) || metadata == null)
        {
            return BayMetadata.CreateDefault();
        }

        metadata.Normalize();
        return metadata;
    }

    public static string ValidateBayForAssign(Bay bay, string normalizedStatus)
    {
        if (bay.IsDeleted == true) return "Khoang đã bị ẩn.";
        if (bay.IsActive == false) return "Khoang đang bị khóa.";
        if (normalizedStatus == BayAdminConstants.StatusMaintenance) return "Khoang đang bảo trì hoặc gặp sự cố.";
        if (normalizedStatus == BayAdminConstants.StatusSuspended) return "Khoang đang tạm ngưng.";
        if (normalizedStatus != BayAdminConstants.StatusEmpty && normalizedStatus != BayAdminConstants.StatusInUse)
            return "Trạng thái khoang không hợp lệ để gán xe.";
        return string.Empty;
    }

    public static string MergeNote(string? oldValue, string? newValue)
    {
        var oldText = string.IsNullOrWhiteSpace(oldValue) ? "" : oldValue.Trim();
        var newText = string.IsNullOrWhiteSpace(newValue) ? "" : newValue.Trim();
        if (string.IsNullOrWhiteSpace(newText)) return oldText;
        if (string.IsNullOrWhiteSpace(oldText)) return newText;
        return $"{oldText} | {newText}";
    }

    public static bool IsSegmentCompatible(string normalizedSegment, IReadOnlyList<string> supportedSegments)
    {
        if (supportedSegments.Count == 0) return true;
        if (string.IsNullOrWhiteSpace(normalizedSegment)) return false;

        var normalizedList = supportedSegments
            .Select(NormalizeSegment)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return normalizedList.Contains("ALL") || normalizedList.Contains(normalizedSegment);
    }

    public static int EstimateCodeDistance(string? sourceCode, string? targetCode)
    {
        var source = ExtractCodeNumber(sourceCode);
        var target = ExtractCodeNumber(targetCode);
        if (source.HasValue && target.HasValue)
        {
            return Math.Abs(source.Value - target.Value);
        }

        var a = string.IsNullOrWhiteSpace(sourceCode) ? "" : sourceCode.Trim().ToUpperInvariant();
        var b = string.IsNullOrWhiteSpace(targetCode) ? "" : targetCode.Trim().ToUpperInvariant();
        return LevenshteinDistance(a, b);
    }

    public static int AlertSeverityScore(string? severity)
    {
        return NormalizeKey(severity) switch
        {
            "CRITICAL" => 4,
            "HIGH" => 3,
            "MEDIUM" => 2,
            "LOW" => 1,
            _ => 0
        };
    }

    public static string NormalizeOperationalStatus(string? status)
    {
        var normalized = NormalizeKey(status);
        return normalized switch
        {
            "" => BayAdminConstants.StatusEmpty,
            BayAdminConstants.DbBayStatusActive or "AVAILABLE" or "READY" or "EMPTY" or "FREE" or "TRONG" => BayAdminConstants.StatusEmpty,
            "OCCUPIED" or "IN_USE" or "BUSY" or "USING" or "DANG_SU_DUNG" => BayAdminConstants.StatusInUse,
            BayAdminConstants.DbBayStatusUnderMaintenance or BayAdminConstants.DbBayStatusBroken or "MAINTENANCE" or "REPAIR" or "BAO_TRI" => BayAdminConstants.StatusMaintenance,
            "SUSPENDED" or "PAUSED" or "TEMP_STOP" or "TAM_NGUNG" => BayAdminConstants.StatusSuspended,
            _ => normalized
        };
    }

    public static bool IsKnownOperationalStatus(string? normalizedStatus)
    {
        var value = NormalizeKey(normalizedStatus);
        return value == BayAdminConstants.StatusActive ||
               value == BayAdminConstants.StatusUnderMaintenance ||
               value == BayAdminConstants.StatusBroken;
    }

    public static string NormalizeArea(string? area)
    {
        if (string.IsNullOrWhiteSpace(area)) return "Chưa phân khu";
        return area.Trim();
    }

    public static int NormalizeCapacity(int? capacity)
    {
        return Math.Clamp(capacity ?? 1, 1, 20);
    }

    public static List<string> NormalizeSegments(IEnumerable<string>? segments)
    {
        if (segments == null) return new List<string>();
        return segments
            .Select(NormalizeSegment)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }

    public static string NormalizeSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return value.Trim().ToUpperInvariant().Replace(" ", "_");
    }

    public static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return value.Trim().ToUpperInvariant();
    }

    public static string NormalizeForSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return value.Trim().ToUpperInvariant();
    }

    private static int? ExtractCodeNumber(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var digits = new string(code.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) ? value : null;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        if (a == b) return 0;
        if (string.IsNullOrEmpty(a)) return b.Length;
        if (string.IsNullOrEmpty(b)) return a.Length;

        var rows = a.Length + 1;
        var cols = b.Length + 1;
        var dp = new int[rows, cols];

        for (var i = 0; i < rows; i++) dp[i, 0] = i;
        for (var j = 0; j < cols; j++) dp[0, j] = j;

        for (var i = 1; i < rows; i++)
        {
            for (var j = 1; j < cols; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[rows - 1, cols - 1];
    }
}
