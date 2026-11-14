using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MyGarage.Common
{
    public static class EngineTypeCompatibilityRules
    {
        public static IReadOnlyCollection<string> ParseServiceEngineFamilies(string? rawValue)
        {
            var normalized = NormalizeTokenSource(rawValue);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return Array.Empty<string>();
            }

            if (ContainsAny(normalized, "ALL", "ANY", "UNIVERSAL", "GENERAL"))
            {
                return Array.Empty<string>();
            }

            var families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (ContainsAny(normalized, "PHEV"))
            {
                families.Add("PHEV");
            }

            if (ContainsAny(normalized, "HEV", "HYBRID"))
            {
                families.Add("HYBRID");
            }

            if (ContainsAny(normalized, "EV", "ELECTRIC", "DIEN"))
            {
                families.Add("EV");
            }

            if (ContainsAny(normalized, "DIESEL", "DAU"))
            {
                families.Add("DIESEL");
            }

            if (ContainsAny(normalized, "GASOLINE", "PETROL", "XANG"))
            {
                families.Add("GASOLINE");
            }

            if (ContainsAny(normalized, "CNG", "LPG", "GAS"))
            {
                families.Add("GAS");
            }

            if (ContainsAny(normalized, "ICE", "COMBUSTION", "DOT TRONG", "DONG CO DOT TRONG"))
            {
                families.Add("ICE");
            }

            return families;
        }

        public static IReadOnlyCollection<string> ParseVehicleEngineFamilies(string? rawFuelType)
        {
            var normalized = NormalizeTokenSource(rawFuelType);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return Array.Empty<string>();
            }

            var families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (ContainsAny(normalized, "PHEV"))
            {
                families.Add("PHEV");
                families.Add("HYBRID");
                families.Add("ICE");
            }

            if (ContainsAny(normalized, "HEV", "HYBRID"))
            {
                families.Add("HYBRID");
                families.Add("ICE");
            }

            if (ContainsAny(normalized, "EV", "ELECTRIC", "DIEN"))
            {
                families.Add("EV");
            }

            if (ContainsAny(normalized, "DIESEL", "DAU"))
            {
                families.Add("DIESEL");
                families.Add("ICE");
            }

            if (ContainsAny(normalized, "GASOLINE", "PETROL", "XANG"))
            {
                families.Add("GASOLINE");
                families.Add("ICE");
            }

            if (ContainsAny(normalized, "CNG", "LPG", "GAS"))
            {
                families.Add("GAS");
                families.Add("ICE");
            }

            if (ContainsAny(normalized, "ICE", "COMBUSTION", "DOT TRONG", "DONG CO DOT TRONG"))
            {
                families.Add("ICE");
            }

            return families;
        }

        public static bool IsServiceCompatibleWithVehicleFuel(string? serviceEngineType, string? vehicleFuelType)
        {
            var vehicleFamilies = ParseVehicleEngineFamilies(vehicleFuelType);
            if (vehicleFamilies.Count == 0)
            {
                return true;
            }

            var serviceFamilies = ParseServiceEngineFamilies(serviceEngineType);
            if (serviceFamilies.Count == 0)
            {
                return true;
            }

            return serviceFamilies.Any(vehicleFamilies.Contains);
        }

        private static bool ContainsAny(string source, params string[] keywords)
        {
            return keywords.Any(keyword => source.Contains(keyword, StringComparison.Ordinal));
        }

        private static string NormalizeTokenSource(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);
            foreach (var character in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(character);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (character == '\u0111' || character == '\u0110')
                {
                    builder.Append('D');
                    continue;
                }

                builder.Append(char.IsLetterOrDigit(character) ? char.ToUpperInvariant(character) : ' ');
            }

            return string.Join(" ", builder.ToString()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
    }
}
