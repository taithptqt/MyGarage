using System;

namespace MyGarage.Infrastructure.Documents;

internal static class QuotationExcelConstants
{
    public const string MainWorksheetName = "Báo Giá";
    public const string MetaWorksheetName = "_MyGarageMeta";
    public const string ExportMarker = "MYGARAGE_QUOTATION_EXPORT_V1";
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const int TotalColumns = 11;

    public static string ResolveUnit(string? itemType)
    {
        var value = itemType?.Trim() ?? string.Empty;
        if (value.Equals("SERVICE", StringComparison.OrdinalIgnoreCase)
            || value.Equals("LABOR", StringComparison.OrdinalIgnoreCase))
        {
            return "Công";
        }

        return "Cái";
    }

    public static string NormalizeText(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    public static string NormalizeItemName(string? value)
    {
        var normalized = NormalizeText(value);
        if (normalized.StartsWith("└─", StringComparison.Ordinal))
        {
            normalized = normalized[2..].Trim();
        }
        else if (normalized.StartsWith("â””â”€", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["â””â”€".Length..].Trim();
        }
        else if (normalized.StartsWith("-"))
        {
            normalized = normalized[1..].Trim();
        }

        return normalized;
    }
}
