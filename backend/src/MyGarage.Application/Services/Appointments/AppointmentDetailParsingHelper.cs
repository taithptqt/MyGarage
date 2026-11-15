using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using MyGarage.Constants;
using MyGarage.DTOs.Responses;

namespace MyGarage.Services
{
    internal static class AppointmentDetailParsingHelper
    {
        private static readonly Regex ProductFallbackLabelRegex = new(
            @"(?:Sản phẩm|San pham|Product)\s*#(?<id>\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex PartShortageItemRegex = new(
            @"^(?<label>.+?)\s*\(cần\s*(?<required>[-+]?\d+(?:[.,]\d+)?),\s*còn\s*(?<available>[-+]?\d+(?:[.,]\d+)?)\s*\)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static List<AppointmentPartShortageServiceResponseDto> BuildFallbackPartsFollowUpServices(
            IReadOnlyCollection<string> serviceNames,
            IReadOnlyCollection<string> rawDetails,
            IReadOnlyDictionary<long, string> productLabelById)
        {
            if (serviceNames == null || rawDetails == null)
            {
                return new List<AppointmentPartShortageServiceResponseDto>();
            }

            var normalizedServiceNames = serviceNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct()
                .ToList();

            if (normalizedServiceNames.Count != 1)
            {
                return new List<AppointmentPartShortageServiceResponseDto>();
            }

            var missingParts = ParseFallbackPartShortageItems(rawDetails, productLabelById);
            if (missingParts.Count == 0)
            {
                return new List<AppointmentPartShortageServiceResponseDto>();
            }

            return new List<AppointmentPartShortageServiceResponseDto>
            {
                new()
                {
                    ServiceId = 0,
                    ServiceName = normalizedServiceNames[0],
                    MissingParts = missingParts,
                }
            };
        }

        internal static string? ExtractTargetBayArea(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
            {
                return null;
            }

            var start = notes.IndexOf(WorkSlotNoteMarkers.TargetBayAreaPrefix, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return null;
            }

            start += WorkSlotNoteMarkers.TargetBayAreaPrefix.Length;
            var end = notes.IndexOf(']', start);
            if (end <= start)
            {
                return null;
            }

            return notes.Substring(start, end - start).Trim().ToUpperInvariant();
        }

        internal static string? ExtractPartsShortageDetails(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
            {
                return null;
            }

            var start = notes.IndexOf(WorkSlotNoteMarkers.PartsShortageDetailsPrefix, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return null;
            }

            start += WorkSlotNoteMarkers.PartsShortageDetailsPrefix.Length;
            var end = notes.IndexOf(']', start);
            if (end <= start)
            {
                return null;
            }

            return notes.Substring(start, end - start).Trim();
        }

        internal static IEnumerable<long> ExtractFallbackProductIds(IEnumerable<string> details)
        {
            if (details == null)
            {
                yield break;
            }

            foreach (var detail in details)
            {
                if (string.IsNullOrWhiteSpace(detail))
                {
                    continue;
                }

                foreach (Match match in ProductFallbackLabelRegex.Matches(detail))
                {
                    if (long.TryParse(match.Groups["id"].Value, out var productId) && productId > 0)
                    {
                        yield return productId;
                    }
                }
            }
        }

        internal static string ReplaceFallbackProductLabels(string detail, IReadOnlyDictionary<long, string> productLabelById)
        {
            if (string.IsNullOrWhiteSpace(detail))
            {
                return string.Empty;
            }

            return ProductFallbackLabelRegex.Replace(detail, match =>
            {
                if (!long.TryParse(match.Groups["id"].Value, out var productId))
                {
                    return match.Value;
                }

                return productLabelById.TryGetValue(productId, out var label) && !string.IsNullOrWhiteSpace(label)
                    ? label
                    : match.Value;
            });
        }

        internal static string BuildDispatchAlertMessage(IReadOnlyCollection<string> targetAreas)
        {
            if (targetAreas == null || targetAreas.Count == 0)
            {
                return "Lịch này đang chờ điều phối khoang thủ công. Lễ tân báo quản đốc để gán khoang trước giờ xe vào xưởng.";
            }

            var normalized = targetAreas
                .Where(area => !string.IsNullOrWhiteSpace(area))
                .Select(area => area.Trim().ToUpperInvariant())
                .Distinct()
                .ToList();

            if (normalized.Count == 0)
            {
                return "Lịch này đang chờ điều phối khoang thủ công. Lễ tân báo quản đốc để gán khoang trước giờ xe vào xưởng.";
            }

            var joinedAreas = string.Join(", ", normalized);
            return $"Lịch này đang chờ điều phối khoang thủ công (mục tiêu: {joinedAreas}). Lễ tân báo quản đốc để gán khoang trước giờ xe vào xưởng.";
        }

        internal static string BuildPartsFollowUpMessage(IReadOnlyCollection<string> details)
        {
            const string defaultMessage = "Lịch hẹn đang thiếu phụ tùng bắt buộc. Lễ tân cần gọi khách để xác nhận thời gian xử lý.";
            if (details == null || details.Count == 0)
            {
                return defaultMessage;
            }

            var normalized = details
                .Where(detail => !string.IsNullOrWhiteSpace(detail))
                .Select(detail => detail.Trim())
                .Distinct()
                .ToList();

            if (normalized.Count == 0)
            {
                return defaultMessage;
            }

            return $"Lịch hẹn đang thiếu phụ tùng bắt buộc: {string.Join("; ", normalized)}. Lễ tân cần gọi khách để xác nhận thời gian xử lý.";
        }

        internal static string ResolvePartLabel(long productId, string? name, string? sku)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name.Trim();
            }

            if (!string.IsNullOrWhiteSpace(sku))
            {
                return sku.Trim();
            }

            return $"Sản phẩm #{productId}";
        }

        internal static IEnumerable<long> ParseServiceIds(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return Enumerable.Empty<long>();
            return csv
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => long.TryParse(s.Trim(), out var id) ? id : 0)
                .Where(id => id > 0);
        }

        private static List<AppointmentPartShortageItemResponseDto> ParseFallbackPartShortageItems(
            IReadOnlyCollection<string> rawDetails,
            IReadOnlyDictionary<long, string> productLabelById)
        {
            if (rawDetails == null || rawDetails.Count == 0)
            {
                return new List<AppointmentPartShortageItemResponseDto>();
            }

            var parsedItems = new List<AppointmentPartShortageItemResponseDto>();

            foreach (var detail in rawDetails)
            {
                var normalizedDetail = ReplaceFallbackProductLabels(detail, productLabelById);
                if (string.IsNullOrWhiteSpace(normalizedDetail))
                {
                    continue;
                }

                var segments = normalizedDetail
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var segment in segments)
                {
                    if (string.IsNullOrWhiteSpace(segment))
                    {
                        continue;
                    }

                    var match = PartShortageItemRegex.Match(segment);
                    if (!match.Success)
                    {
                        parsedItems.Add(new AppointmentPartShortageItemResponseDto
                        {
                            ProductId = 0,
                            ProductName = segment.Trim(),
                            RequiredQuantity = 0m,
                            AvailableQuantity = 0m,
                            Unit = null,
                        });
                        continue;
                    }

                    var label = match.Groups["label"].Value.Trim();
                    parsedItems.Add(new AppointmentPartShortageItemResponseDto
                    {
                        ProductId = ExtractFallbackProductId(label),
                        ProductName = label,
                        RequiredQuantity = ParseQuantity(match.Groups["required"].Value),
                        AvailableQuantity = ParseQuantity(match.Groups["available"].Value),
                        Unit = null,
                    });
                }
            }

            return parsedItems
                .Where(item => !string.IsNullOrWhiteSpace(item.ProductName))
                .GroupBy(item => item.ProductName, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var firstItem = group.First();
                    return new AppointmentPartShortageItemResponseDto
                    {
                        ProductId = firstItem.ProductId,
                        ProductName = firstItem.ProductName,
                        RequiredQuantity = group.Sum(item => item.RequiredQuantity),
                        AvailableQuantity = group.Min(item => item.AvailableQuantity),
                        Unit = group.Select(item => item.Unit).FirstOrDefault(unit => !string.IsNullOrWhiteSpace(unit)),
                    };
                })
                .OrderBy(item => item.ProductName)
                .ToList();
        }

        private static long ExtractFallbackProductId(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return 0;
            }

            var match = ProductFallbackLabelRegex.Match(label);
            return match.Success && long.TryParse(match.Groups["id"].Value, out var productId)
                ? productId
                : 0;
        }

        private static decimal ParseQuantity(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return 0m;
            }

            var trimmed = rawValue.Trim();
            if (decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.GetCultureInfo("vi-VN"), out var viValue))
            {
                return viValue;
            }

            var normalized = trimmed.Replace(',', '.');
            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantValue)
                ? invariantValue
                : 0m;
        }
    }
}
