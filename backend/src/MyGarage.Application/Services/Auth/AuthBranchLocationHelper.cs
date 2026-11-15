using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Auth;
using MyGarage.Models;

namespace MyGarage.Services
{
    internal static class AuthBranchLocationHelper
    {
        internal static async Task<long?> ResolveNearestBranchIdAsync(
            IAuthSessionValidationStore sessionStore,
            decimal? lat,
            decimal? lng,
            string? city = null)
        {
            var activeBranches = await sessionStore.GetActiveBranchesAsync();

            if (activeBranches.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(city) && TryResolveRegionFromProvince(city, out var region))
            {
                var regionKeywords = region switch
                {
                    "north" => new[] { "ha noi", "hanoi", "bac" },
                    "central" => new[] { "da nang", "danang", "trung" },
                    "south" => new[] { "hcm", "ho chi minh", "sai gon", "nam" },
                    _ => Array.Empty<string>()
                };

                var branchByRegion = activeBranches.FirstOrDefault(b =>
                    !string.IsNullOrWhiteSpace(b.Name) &&
                    regionKeywords.Any(k => NormalizeVietnamText(b.Name!).Contains(k, StringComparison.Ordinal)));

                if (branchByRegion is not null)
                {
                    return branchByRegion.Id;
                }
            }

            if (lat.HasValue && lng.HasValue)
            {
                var geoBranches = activeBranches
                    .Where(b => b.Lat.HasValue && b.Lng.HasValue)
                    .ToList();

                if (geoBranches.Count > 0)
                {
                    var nearest = geoBranches
                        .Select(b => new
                        {
                            b.Id,
                            Distance = CalculateDistanceKm(
                                (double)lat.Value,
                                (double)lng.Value,
                                (double)b.Lat!.Value,
                                (double)b.Lng!.Value)
                        })
                        .OrderBy(x => x.Distance)
                        .First();

                    return nearest.Id;
                }
            }

            return activeBranches.OrderBy(b => b.Id).Select(b => (long?)b.Id).FirstOrDefault();
        }

        internal static long? ExtractPreferredBranchId(string? note)
        {
            if (string.IsNullOrWhiteSpace(note)) return null;
            const string key = "[PreferredBranchId:";
            var start = note.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return null;
            start += key.Length;
            var end = note.IndexOf("]", start, StringComparison.OrdinalIgnoreCase);
            if (end <= start) return null;

            var raw = note.Substring(start, end - start);
            return long.TryParse(raw, out var value) ? value : null;
        }

        internal static void AssignPreferredBranchToCustomer(Customer customer, long? branchId)
        {
            if (!branchId.HasValue) return;

            var existingBranchId = ExtractPreferredBranchId(customer.Note);
            if (existingBranchId == branchId.Value) return;

            var tag = $"[PreferredBranchId:{branchId.Value}]";
            if (string.IsNullOrWhiteSpace(customer.Note))
            {
                customer.Note = tag;
            }
            else if (existingBranchId.HasValue)
            {
                var oldTag = $"[PreferredBranchId:{existingBranchId.Value}]";
                customer.Note = customer.Note.Replace(oldTag, tag, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                customer.Note = $"{customer.Note} | {tag}";
            }
        }

        internal static void AssignCityToCustomer(Customer customer, string? city)
        {
            var cleanedCity = city?.Trim();
            if (string.IsNullOrWhiteSpace(cleanedCity))
            {
                return;
            }

            var cityPrefix = "[City:";
            var cityStart = customer.Note?.IndexOf(cityPrefix, StringComparison.OrdinalIgnoreCase) ?? -1;
            var cityTag = $"[City:{cleanedCity}]";

            if (cityStart >= 0)
            {
                var end = customer.Note!.IndexOf("]", cityStart, StringComparison.OrdinalIgnoreCase);
                if (end > cityStart)
                {
                    var oldTag = customer.Note.Substring(cityStart, end - cityStart + 1);
                    customer.Note = customer.Note.Replace(oldTag, cityTag, StringComparison.OrdinalIgnoreCase);
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(customer.Note))
            {
                customer.Note = cityTag;
            }
            else
            {
                customer.Note = $"{customer.Note} | {cityTag}";
            }
        }

        private static bool TryResolveRegionFromProvince(string province, out string region)
        {
            var normalized = NormalizeVietnamText(province);
            normalized = normalized
                .Replace("tinh ", string.Empty, StringComparison.Ordinal)
                .Replace("thanh pho ", string.Empty, StringComparison.Ordinal)
                .Replace("tp ", string.Empty, StringComparison.Ordinal);

            var north = new HashSet<string>
            {
                "ha noi", "hai phong", "quang ninh", "bac ninh", "bac giang", "lang son", "cao bang",
                "bac kan", "thai nguyen", "tuyen quang", "ha giang", "lao cai", "yen bai", "dien bien",
                "lai chau", "son la", "hoa binh", "phu tho", "vinh phuc", "thai binh", "nam dinh",
                "ha nam", "ninh binh", "hung yen", "hai duong"
            };

            var central = new HashSet<string>
            {
                "thanh hoa", "nghe an", "ha tinh", "quang binh", "quang tri", "thua thien hue",
                "hue", "da nang", "quang nam", "quang ngai", "binh dinh", "phu yen", "khanh hoa",
                "ninh thuan", "binh thuan", "kon tum", "gia lai", "dak lak", "dak nong", "lam dong"
            };

            var south = new HashSet<string>
            {
                "ho chi minh", "tp hcm", "hcm", "sai gon", "ba ria vung tau", "vung tau",
                "binh duong", "binh phuoc", "dong nai", "tay ninh", "long an", "tien giang",
                "ben tre", "tra vinh", "vinh long", "dong thap", "an giang", "kien giang",
                "can tho", "hau giang", "soc trang", "bac lieu", "ca mau"
            };

            if (north.Contains(normalized))
            {
                region = "north";
                return true;
            }

            if (central.Contains(normalized))
            {
                region = "central";
                return true;
            }

            if (south.Contains(normalized))
            {
                region = "south";
                return true;
            }

            region = string.Empty;
            return false;
        }

        private static string NormalizeVietnamText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var buffer = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category == UnicodeCategory.NonSpacingMark) continue;
                buffer.Append(ch == '\u0111' ? 'd' : ch);
            }

            var noAccent = buffer.ToString().Normalize(NormalizationForm.FormC);
            var cleaned = new StringBuilder(noAccent.Length);
            var previousWasSpace = false;
            foreach (var ch in noAccent)
            {
                var current = char.IsLetterOrDigit(ch) ? ch : ' ';
                if (current == ' ')
                {
                    if (previousWasSpace) continue;
                    previousWasSpace = true;
                }
                else
                {
                    previousWasSpace = false;
                }
                cleaned.Append(current);
            }

            return cleaned.ToString().Trim();
        }

        private static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadiusKm = 6371d;
            static double ToRadians(double degree) => degree * Math.PI / 180d;

            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a =
                Math.Pow(Math.Sin(dLat / 2d), 2d) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Pow(Math.Sin(dLon / 2d), 2d);
            var c = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
            return earthRadiusKm * c;
        }
    }
}
