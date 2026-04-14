using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MyGarage.Services
{
    public partial class AuthService
    {
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
