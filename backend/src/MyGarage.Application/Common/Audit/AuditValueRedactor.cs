using System.Text.Json;
using System.Text.Json.Nodes;
using MyGarage.Domain.Rules;

namespace MyGarage.Application.Common.Audit;

public static class AuditValueRedactor
{
    private const int MaxNestedDepth = 32;

    // Only explicitly approved typed fields are returned from historical audit data.
    // All other values can contain free-text secrets even when their key looks harmless.
    public static string? RedactForRead(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var safe = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!seen.Add(property.Name)) return null;
                if ((property.Name.Equals("IsDeleted", StringComparison.OrdinalIgnoreCase) ||
                     property.Name.Equals("IsActive", StringComparison.OrdinalIgnoreCase)) &&
                    property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    safe.Add(property.Name, property.Value.GetBoolean());
            }
            return safe.Count == 0 ? null : JsonSerializer.Serialize(safe);
        }
        catch (JsonException) { return null; }
    }

    // Audit values are expected to be JSON objects. Unknown legacy formats are
    // withheld because their contents cannot be inspected by field name.
    public static string? Redact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            var node = JsonNode.Parse(value);
            if (node is not JsonObject) return null;
            if (!RedactNode(node, 0)) return null;
            return node.ToJsonString();
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private static bool RedactNode(JsonNode? node, int depth)
    {
        if (depth > MaxNestedDepth) return false;
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToArray())
            {
                if (SharedOperationsDomainRules.IsSensitiveAuditField(property.Key))
                    obj[property.Key] = "[REDACTED]";
                else if (IsEmbeddedJson(property.Value, out var text))
                {
                    if (!TryRedactEmbedded(text!, depth + 1, out var sanitized)) return false;
                    obj[property.Key] = sanitized;
                }
                else
                {
                    if (!RedactNode(property.Value, depth + 1)) return false;
                }
            }
        }
        else if (node is JsonArray array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                if (IsEmbeddedJson(array[i], out var text))
                {
                    if (!TryRedactEmbedded(text!, depth + 1, out var sanitized)) return false;
                    array[i] = sanitized;
                }
                else
                {
                    if (!RedactNode(array[i], depth + 1)) return false;
                }
            }
        }
        return true;
    }

    private static bool IsEmbeddedJson(JsonNode? value, out string? text)
    {
        text = null;
        if (value is not JsonValue scalar || !scalar.TryGetValue<string>(out text) || text is null)
            return false;
        var trimmed = text.AsSpan().TrimStart();
        return !trimmed.IsEmpty && (trimmed[0] == '{' || trimmed[0] == '[');
    }

    private static bool TryRedactEmbedded(string text, int depth, out string? sanitized)
    {
        sanitized = null;
        if (depth > MaxNestedDepth) return false;
        var embedded = JsonNode.Parse(text);
        if (embedded is not (JsonObject or JsonArray) || !RedactNode(embedded, depth)) return false;
        sanitized = embedded.ToJsonString();
        return true;
    }
}
