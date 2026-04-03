using System;
using System.Globalization;
using System.Linq;

namespace MyGarage.Services
{
    public partial class VoucherRedemptionService
    {
        private static bool IsInValidTimeRange(TimeOnly? validFrom, TimeOnly? validTo, TimeSpan now)
        {
            if (!validFrom.HasValue && !validTo.HasValue)
            {
                return true;
            }

            var nowTime = TimeOnly.FromTimeSpan(now);

            if (validFrom.HasValue && validTo.HasValue)
            {
                if (validFrom.Value <= validTo.Value)
                {
                    return nowTime >= validFrom.Value && nowTime <= validTo.Value;
                }

                // Range crosses midnight
                return nowTime >= validFrom.Value || nowTime <= validTo.Value;
            }

            if (validFrom.HasValue)
            {
                return nowTime >= validFrom.Value;
            }

            return nowTime <= validTo!.Value;
        }

        private static bool IsDayAllowed(string? validDaysOfWeek, DayOfWeek currentDay)
        {
            if (string.IsNullOrWhiteSpace(validDaysOfWeek))
            {
                return true;
            }

            var tokens = validDaysOfWeek
                .Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (tokens.Count == 0)
            {
                return true;
            }

            var dayNumber = ((int)currentDay).ToString(CultureInfo.InvariantCulture);
            if (tokens.Any(t => string.Equals(t, dayNumber, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var dayCode = currentDay.ToString().Substring(0, 3).ToUpperInvariant();
            return tokens.Any(t =>
            {
                var normalized = t.ToUpperInvariant();
                return normalized == dayCode || normalized == currentDay.ToString().ToUpperInvariant();
            });
        }

    }
}
