using System;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AuthService
    {
        private static long? ExtractPreferredBranchId(string? note)
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

        private static void AssignPreferredBranchToCustomer(Customer customer, long? branchId)
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

        private static void AssignCityToCustomer(Customer customer, string? city)
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


    }
}
