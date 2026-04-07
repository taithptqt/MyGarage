namespace MyGarage.Enums;

public static class StatusEnumParser
{
    public static T ParseOrDefault<T>(string? raw, T fallback) where T : struct, System.Enum
    {
        if (!string.IsNullOrWhiteSpace(raw) && System.Enum.TryParse<T>(raw.Trim(), true, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    public static T? ParseNullable<T>(string? raw) where T : struct, System.Enum
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return System.Enum.TryParse<T>(raw.Trim(), true, out var parsed) ? parsed : null;
    }

    public static string? ToDbValue<T>(T? value) where T : struct, System.Enum
    {
        return value?.ToString();
    }
}
