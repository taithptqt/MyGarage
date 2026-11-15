namespace MyGarage.Services;

internal static class CustomerNotificationDedupeHelper
{
    public const string MaintenancePrefix = "/jobs/maintenance/";
    public const string SurveyPrefix = "/jobs/survey/service-order/";
    public const string InsurancePrefix = "/jobs/insurance/";

    public static string BuildMaintenanceNotificationKey(long customerId, long vehicleId, string dedupeToken)
    {
        return $"{customerId}:maintenance:{vehicleId}:{NormalizeNotificationToken(dedupeToken)}";
    }

    public static string BuildSurveyNotificationKey(long customerId, long serviceOrderId)
    {
        return $"{customerId}:survey:{serviceOrderId}";
    }

    public static string BuildInsuranceNotificationKey(long customerId, long vehicleInsuranceId, DateOnly expiryDate)
    {
        return $"{customerId}:insurance:{vehicleInsuranceId}:{expiryDate:yyyyMMdd}";
    }

    public static bool TryBuildMaintenanceNotificationKey(long customerId, string linkAction, out string key)
    {
        key = string.Empty;
        if (!TryParseMaintenanceIdentity(linkAction, out var vehicleId, out var dedupeToken))
        {
            return false;
        }

        key = BuildMaintenanceNotificationKey(customerId, vehicleId, dedupeToken);
        return true;
    }

    public static bool TryBuildSurveyNotificationKey(long customerId, string linkAction, out string key)
    {
        key = string.Empty;
        if (!TryParseSurveyIdentity(linkAction, out var serviceOrderId))
        {
            return false;
        }

        key = BuildSurveyNotificationKey(customerId, serviceOrderId);
        return true;
    }

    public static bool TryBuildInsuranceNotificationKey(long customerId, string linkAction, out string key)
    {
        key = string.Empty;
        if (!TryParseInsuranceIdentity(linkAction, out var vehicleInsuranceId, out var expiryDate))
        {
            return false;
        }

        key = BuildInsuranceNotificationKey(customerId, vehicleInsuranceId, expiryDate);
        return true;
    }

    public static List<string> NormalizeList(IEnumerable<string>? values, params string[] defaults)
    {
        var source = values?.ToList() ?? defaults.ToList();
        var normalized = source
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return normalized.Count > 0 ? normalized : defaults.ToList();
    }

    private static bool TryParseMaintenanceIdentity(string linkAction, out long vehicleId, out string dedupeToken)
    {
        vehicleId = 0;
        dedupeToken = string.Empty;
        var trimmed = linkAction?.Trim() ?? string.Empty;
        if (trimmed.StartsWith(MaintenancePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var parts = trimmed[MaintenancePrefix.Length..]
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2 || !long.TryParse(parts[0], out vehicleId))
            {
                return false;
            }

            dedupeToken = NormalizeNotificationToken(parts[1]);
            return !string.IsNullOrWhiteSpace(dedupeToken);
        }

        if (!trimmed.Contains("notificationType=maintenance", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TryParseVehicleIdFromMyVehiclesPath(trimmed, out vehicleId))
        {
            return false;
        }

        if (!TryGetQueryParameter(trimmed, "dedupe", out dedupeToken))
        {
            return false;
        }

        dedupeToken = NormalizeNotificationToken(dedupeToken);
        return !string.IsNullOrWhiteSpace(dedupeToken);
    }

    private static bool TryParseSurveyIdentity(string linkAction, out long serviceOrderId)
    {
        serviceOrderId = 0;
        var trimmed = linkAction?.Trim() ?? string.Empty;
        if (trimmed.StartsWith(SurveyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var rawOrderId = trimmed[SurveyPrefix.Length..]
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            return long.TryParse(rawOrderId, out serviceOrderId);
        }

        if (!trimmed.Contains("notificationType=survey", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TryGetQueryParameter(trimmed, "serviceOrderId", out var rawServiceOrderId))
        {
            return false;
        }

        return long.TryParse(rawServiceOrderId, out serviceOrderId);
    }

    private static bool TryParseInsuranceIdentity(
        string linkAction,
        out long vehicleInsuranceId,
        out DateOnly expiryDate)
    {
        vehicleInsuranceId = 0;
        expiryDate = default;
        var trimmed = linkAction?.Trim() ?? string.Empty;
        if (trimmed.StartsWith(InsurancePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var parts = trimmed[InsurancePrefix.Length..]
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2 || !long.TryParse(parts[0], out vehicleInsuranceId))
            {
                return false;
            }

            return DateOnly.TryParseExact(
                parts[1],
                "yyyyMMdd",
                null,
                System.Globalization.DateTimeStyles.None,
                out expiryDate);
        }

        if (!trimmed.Contains("notificationType=insurance", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TryGetQueryParameter(trimmed, "vehicleInsuranceId", out var rawInsuranceId) ||
            !long.TryParse(rawInsuranceId, out vehicleInsuranceId))
        {
            return false;
        }

        if (!TryGetQueryParameter(trimmed, "expiryDate", out var rawExpiryDate))
        {
            return false;
        }

        return DateOnly.TryParseExact(
            rawExpiryDate,
            "yyyyMMdd",
            null,
            System.Globalization.DateTimeStyles.None,
            out expiryDate);
    }

    private static bool TryParseVehicleIdFromMyVehiclesPath(string path, out long vehicleId)
    {
        vehicleId = 0;
        var pathOnly = path.Split('?', 2)[0];
        var parts = pathOnly.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 ||
            !string.Equals(parts[0], "my-vehicles", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return long.TryParse(parts[1], out vehicleId);
    }

    private static bool TryGetQueryParameter(string path, string key, out string value)
    {
        value = string.Empty;
        var queryIndex = path.IndexOf('?');
        if (queryIndex < 0 || queryIndex == path.Length - 1)
        {
            return false;
        }

        var query = path[(queryIndex + 1)..];
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = pair.Split('=', 2);
            if (!string.Equals(kv[0], key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        return false;
    }

    private static string NormalizeNotificationToken(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        return rawValue.Trim().ToUpperInvariant();
    }
}
