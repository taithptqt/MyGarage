using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class PiiAnonymizerService : IPiiAnonymizerService
{
    private static readonly Regex EmailRegex = new(
        @"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}",
        RegexOptions.Compiled);

    private static readonly Regex PhoneRegex = new(
        @"(?<!\d)(?:\+?84|0)(?:[\s.\-]?\d){8,10}(?!\d)",
        RegexOptions.Compiled);

    private static readonly Regex LicensePlateRegex = new(
        @"\b\d{2}[A-Z]{1,2}\d?[-\s]?\d{3,5}(?:\.\d{2})?\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex NationalIdRegex = new(
        @"(?<!\d)\d{12}(?!\d)",
        RegexOptions.Compiled);

    private static readonly HashSet<string> SensitiveJsonKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "phone", "phoneNumber", "customerPhone", "email", "customerEmail",
        "licensePlate", "plate", "plateNumber", "identityNumber", "cccd",
        "fullName", "customerName", "buyerName", "address"
    };

    public string AnonymizeText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input ?? string.Empty;
        }

        var result = EmailRegex.Replace(input, "[REDACTED_EMAIL]");
        result = LicensePlateRegex.Replace(result, "[REDACTED_PLATE]");
        result = PhoneRegex.Replace(result, "[REDACTED_PHONE]");
        result = NationalIdRegex.Replace(result, "[REDACTED_ID]");
        return result;
    }

    public object AnonymizeObjectGraph(object? input)
    {
        if (input is null)
        {
            return new { };
        }

        try
        {
            var node = input as JsonNode ?? JsonSerializer.SerializeToNode(input);
            if (node is null)
            {
                return new { };
            }

            SanitizeJsonNode(node);
            return node;
        }
        catch
        {
            return AnonymizeText(input.ToString());
        }
    }

    private void SanitizeJsonNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(kvp => kvp.Key).ToList())
                {
                    var child = obj[key];
                    if (child is JsonValue val && val.TryGetValue<string>(out var strVal))
                    {
                        obj[key] = SensitiveJsonKeys.Contains(key)
                            ? "[REDACTED_PII]"
                            : AnonymizeText(strVal);
                    }
                    else
                    {
                        SanitizeJsonNode(child);
                    }
                }
                break;

            case JsonArray arr:
                for (var i = 0; i < arr.Count; i++)
                {
                    var item = arr[i];
                    if (item is JsonValue val && val.TryGetValue<string>(out var strVal))
                    {
                        arr[i] = AnonymizeText(strVal);
                    }
                    else
                    {
                        SanitizeJsonNode(item);
                    }
                }
                break;
        }
    }
}
