using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace MyGarage.Services;

internal sealed class ChatbotToolExecutionException : Exception
{
    public ChatbotToolExecutionException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

internal static class ChatbotToolJsonHelper
{
    public static JsonObject ParseIntentStateRoot(string? intentState)
    {
        if (string.IsNullOrWhiteSpace(intentState))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(intentState) as JsonObject ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }

    public static string? ReadString(JsonObject? root, string propertyName)
    {
        if (root is null || !root.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            return null;
        }

        if (node is JsonValue value && value.TryGetValue<string>(out var stringValue))
        {
            return stringValue?.Trim();
        }

        return node.ToJsonString().Trim('"').Trim();
    }

    public static long? ReadLong(JsonObject? root, string propertyName)
    {
        if (root is null || !root.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            return null;
        }

        if (node is JsonValue value)
        {
            if (value.TryGetValue<long>(out var longValue))
            {
                return longValue;
            }
            if (value.TryGetValue<int>(out var intValue))
            {
                return intValue;
            }
            if (value.TryGetValue<string>(out var stringValue)
                && long.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    public static int? ReadInt(JsonObject? root, string propertyName)
    {
        var value = ReadLong(root, propertyName);
        if (!value.HasValue || value.Value < int.MinValue || value.Value > int.MaxValue)
        {
            return null;
        }

        return (int)value.Value;
    }

    public static bool? ReadBool(JsonObject? root, string propertyName)
    {
        if (root is null || !root.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            return null;
        }

        if (node is JsonValue value)
        {
            if (value.TryGetValue<bool>(out var boolValue))
            {
                return boolValue;
            }
            if (value.TryGetValue<string>(out var stringValue)
                && bool.TryParse(stringValue, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    public static List<long> ReadLongArray(JsonObject? root, string propertyName)
    {
        var values = new List<long>();
        if (root is null || !root.TryGetPropertyValue(propertyName, out var node) || node is null || node is not JsonArray array)
        {
            return values;
        }

        foreach (var item in array)
        {
            if (item is not JsonValue value)
            {
                continue;
            }

            if (value.TryGetValue<long>(out var longValue) && longValue > 0)
            {
                values.Add(longValue);
                continue;
            }

            if (value.TryGetValue<int>(out var intValue) && intValue > 0)
            {
                values.Add(intValue);
                continue;
            }

            if (value.TryGetValue<string>(out var stringValue)
                && long.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                && parsed > 0)
            {
                values.Add(parsed);
            }
        }

        return values.Distinct().ToList();
    }

    public static DateTime? ReadDateTime(JsonObject? root, string propertyName)
    {
        var value = ReadString(root, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParse(value, null, DateTimeStyles.RoundtripKind, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    public static decimal? ReadDecimal(JsonObject? root, string propertyName)
    {
        if (root is null || !root.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            return null;
        }

        if (node is JsonValue value)
        {
            if (value.TryGetValue<decimal>(out var decimalValue))
            {
                return decimalValue;
            }

            if (value.TryGetValue<double>(out var doubleValue))
            {
                return (decimal)doubleValue;
            }

            if (value.TryGetValue<string>(out var stringValue)
                && decimal.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }
}
