using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace MyGarage.Services
{
    internal static class VehicleConditionLogCornerImageHelper
    {
        internal static bool HasRequiredCornerImages(string? defectsAndImages)
        {
            if (string.IsNullOrWhiteSpace(defectsAndImages))
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(defectsAndImages);
                var root = document.RootElement;

                if (TryValidateCornerImagesObject(root))
                {
                    return true;
                }

                var items = ExtractDefectItems(root);
                if (items.Count == 0)
                {
                    return false;
                }

                var hasFrontLeft = false;
                var hasFrontRight = false;
                var hasRearLeft = false;
                var hasRearRight = false;

                foreach (var item in items)
                {
                    if (!TryGetStringProperty(item, "part", out var partText))
                    {
                        continue;
                    }

                    if (!TryGetProperty(item, "images", out var imagesElement) || !HasAnyImage(imagesElement))
                    {
                        continue;
                    }

                    var normalizedPart = VehicleConditionLogCommandHelper.NormalizeSearchText(partText);
                    if (!hasFrontLeft && ContainsAny(normalizedPart, "goc truoc trai", "truoc trai", "front left"))
                    {
                        hasFrontLeft = true;
                    }
                    else if (!hasFrontRight && ContainsAny(normalizedPart, "goc truoc phai", "truoc phai", "front right"))
                    {
                        hasFrontRight = true;
                    }
                    else if (!hasRearLeft && ContainsAny(normalizedPart, "goc sau trai", "sau trai", "rear left", "back left"))
                    {
                        hasRearLeft = true;
                    }
                    else if (!hasRearRight && ContainsAny(normalizedPart, "goc sau phai", "sau phai", "rear right", "back right"))
                    {
                        hasRearRight = true;
                    }
                }

                return hasFrontLeft && hasFrontRight && hasRearLeft && hasRearRight;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryValidateCornerImagesObject(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!TryGetProperty(root, "cornerImages", out var cornerImages) || cornerImages.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return HasCornerImage(cornerImages, "frontLeft")
                && HasCornerImage(cornerImages, "frontRight")
                && HasCornerImage(cornerImages, "rearLeft")
                && HasCornerImage(cornerImages, "rearRight");
        }

        private static bool HasCornerImage(JsonElement cornerImages, string key)
        {
            return TryGetProperty(cornerImages, key, out var imageElement) && HasAnyImage(imageElement);
        }

        private static List<JsonElement> ExtractDefectItems(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Array)
            {
                return root.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).ToList();
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (TryGetProperty(root, "defectItems", out var defectItems) && defectItems.ValueKind == JsonValueKind.Array)
                {
                    return defectItems.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).ToList();
                }

                if (TryGetProperty(root, "items", out var items) && items.ValueKind == JsonValueKind.Array)
                {
                    return items.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).ToList();
                }
            }

            return new List<JsonElement>();
        }

        private static bool HasAnyImage(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                return !string.IsNullOrWhiteSpace(element.GetString());
            }

            if (element.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                {
                    return true;
                }

                if (item.ValueKind == JsonValueKind.Object &&
                    TryGetProperty(item, "url", out var urlElement) &&
                    urlElement.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(urlElement.GetString()))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetStringProperty(JsonElement element, string propertyName, out string value)
        {
            value = string.Empty;
            if (!TryGetProperty(element, propertyName, out var propertyElement))
            {
                return false;
            }

            if (propertyElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            value = propertyElement.GetString() ?? string.Empty;
            return true;
        }

        private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement propertyValue)
        {
            propertyValue = default;
            if (element.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    propertyValue = property.Value;
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsAny(string source, params string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            return tokens.Any(token => source.Contains(token, StringComparison.Ordinal));
        }
    }
}
