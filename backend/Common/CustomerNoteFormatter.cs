using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MyGarage.Common;

public static class CustomerNoteFormatter
{
    private static readonly Regex InternalMarkerRegex = new(
        @"\[(?:FLOW:[^\]]+|CLUSTER:[^\]]+|[A-Z][A-Z0-9_]{2,})\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> EmptyNotePlaceholders = new(StringComparer.OrdinalIgnoreCase)
    {
        "khong co",
        "khong co ghi chu",
        "khong co ghi chu gi",
        "khong co mo ta",
        "none",
        "no note",
        "n/a",
        "na"
    };

    public static string? ToDisplayText(string? rawNote)
    {
        var repaired = Utf8MojibakeRepair.Fix(rawNote);
        if (string.IsNullOrWhiteSpace(repaired))
        {
            return null;
        }

        var candidate = repaired.Trim();
        candidate = TryExtractLegacyJsonNote(candidate) ?? candidate;
        candidate = StripInternalMarkers(candidate);
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        candidate = Regex.Replace(candidate, @"\s{2,}", " ").Trim();
        if (IsPlaceholder(candidate))
        {
            return null;
        }

        return candidate;
    }

    private static string? TryExtractLegacyJsonNote(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var firstChar = value.TrimStart()[0];
        if (firstChar != '{' && firstChar != '[')
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(value);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                var values = root.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString()?.Trim())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToList();
                return values.Count > 0 ? string.Join("; ", values) : null;
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("symptoms", out var symptomsElement) && symptomsElement.ValueKind == JsonValueKind.Array)
                {
                    var values = symptomsElement.EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.String)
                        .Select(item => item.GetString()?.Trim())
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .ToList();
                    if (values.Count > 0)
                    {
                        return string.Join("; ", values);
                    }
                }

                if (root.TryGetProperty("note", out var noteElement) && noteElement.ValueKind == JsonValueKind.String)
                {
                    var note = noteElement.GetString()?.Trim();
                    return string.IsNullOrWhiteSpace(note) ? null : note;
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string StripInternalMarkers(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('[', StringComparison.Ordinal))
        {
            return value;
        }

        var cleaned = InternalMarkerRegex.Replace(value, " ");
        return cleaned.Trim(' ', ',', ';', '.', '|', '-', ':');
    }

    private static bool IsPlaceholder(string value)
    {
        var normalized = Normalize(value);
        return EmptyNotePlaceholders.Contains(normalized);
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = value.Trim().ToLowerInvariant().Replace('đ', 'd');
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ").Trim();
    }
}
