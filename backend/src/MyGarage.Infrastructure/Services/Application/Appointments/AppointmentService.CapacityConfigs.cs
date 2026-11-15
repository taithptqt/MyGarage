using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private const decimal DefaultOnlineBookingThresholdPercent = 0.85m;
        private const decimal MinOnlineBookingThresholdPercent = 0.70m;
        private const decimal MaxOnlineBookingThresholdPercent = 0.95m;

        private async Task<List<CapacityConfig>> LoadCapacityConfigsForDayAsync(long branchId, DateOnly date)
        {
            var normalizedDayOfWeek = ToCapacityConfigDayOfWeek(date.DayOfWeek);
            return await _context.CapacityConfigs
                .AsNoTracking()
                .Where(c =>
                    c.IsActive &&
                    c.DayOfWeek == normalizedDayOfWeek &&
                    (c.BranchId == branchId || c.BranchId == null))
                .OrderByDescending(c => c.BranchId.HasValue)
                .ThenBy(c => c.StartTime)
                .ToListAsync();
        }

        private static byte ToCapacityConfigDayOfWeek(DayOfWeek dayOfWeek)
        {
            var raw = (int)dayOfWeek;
            return (byte)(raw == 0 ? 7 : raw);
        }

        private static int ResolveConfiguredCapacityLimit(
            DateTime startAt,
            DateTime endAt,
            IReadOnlyList<CapacityConfig> capacityConfigs,
            int fallbackLimit,
            long branchId,
            int stepMinutes = 15)
        {
            if (capacityConfigs.Count == 0 || fallbackLimit <= 0 || endAt <= startAt)
            {
                return fallbackLimit;
            }

            var effectiveStep = stepMinutes > 0 ? stepMinutes : 15;
            var minLimit = int.MaxValue;
            for (var cursor = startAt; cursor < endAt; cursor = cursor.AddMinutes(effectiveStep))
            {
                var sliceLimit = ResolveCapacityAt(cursor, capacityConfigs, fallbackLimit, branchId);
                minLimit = Math.Min(minLimit, sliceLimit);
            }

            return minLimit == int.MaxValue ? fallbackLimit : minLimit;
        }

        private static int ResolveCapacityAt(
            DateTime at,
            IReadOnlyList<CapacityConfig> capacityConfigs,
            int fallbackLimit,
            long branchId)
        {
            var match = ResolveCapacityConfigAt(at, capacityConfigs, branchId);
            if (match != null)
            {
                return Math.Max(0, match.MaxBaysAvailable);
            }

            return fallbackLimit;
        }

        private static int ResolveConfiguredOnlineBookingLimit(
            DateTime startAt,
            DateTime endAt,
            IReadOnlyList<CapacityConfig> capacityConfigs,
            int configuredCapacityLimit,
            long branchId,
            int stepMinutes = 15)
        {
            if (configuredCapacityLimit <= 0)
            {
                return 0;
            }

            if (endAt <= startAt)
            {
                return ComputeOnlineBookingLimit(
                    configuredCapacityLimit,
                    DefaultOnlineBookingThresholdPercent);
            }

            var effectiveStep = stepMinutes > 0 ? stepMinutes : 15;
            var minLimit = int.MaxValue;
            for (var cursor = startAt; cursor < endAt; cursor = cursor.AddMinutes(effectiveStep))
            {
                var sliceCapacity = ResolveCapacityAt(cursor, capacityConfigs, configuredCapacityLimit, branchId);
                var sliceThreshold = ResolveOnlineBookingThresholdAt(cursor, capacityConfigs, branchId);
                var sliceLimit = ComputeOnlineBookingLimit(sliceCapacity, sliceThreshold);
                minLimit = Math.Min(minLimit, sliceLimit);
            }

            if (minLimit == int.MaxValue)
            {
                minLimit = ComputeOnlineBookingLimit(
                    configuredCapacityLimit,
                    DefaultOnlineBookingThresholdPercent);
            }

            return Math.Max(0, Math.Min(configuredCapacityLimit, minLimit));
        }

        private static CapacityConfig? ResolveCapacityConfigAt(
            DateTime at,
            IReadOnlyList<CapacityConfig> capacityConfigs,
            long branchId)
        {
            var time = TimeOnly.FromDateTime(at);
            var branchMatch = capacityConfigs.FirstOrDefault(c =>
                c.BranchId == branchId &&
                c.StartTime <= time &&
                c.EndTime > time);
            if (branchMatch != null)
            {
                return branchMatch;
            }

            return capacityConfigs.FirstOrDefault(c =>
                c.BranchId == null &&
                c.StartTime <= time &&
                c.EndTime > time);
        }

        private static decimal ResolveOnlineBookingThresholdAt(
            DateTime at,
            IReadOnlyList<CapacityConfig> capacityConfigs,
            long branchId)
        {
            var match = ResolveCapacityConfigAt(at, capacityConfigs, branchId);
            var configured = match?.BookingThresholdPercent;
            return NormalizeOnlineBookingThreshold(configured);
        }

        private static decimal NormalizeOnlineBookingThreshold(decimal? value)
        {
            var effective = value ?? DefaultOnlineBookingThresholdPercent;
            if (effective < MinOnlineBookingThresholdPercent)
            {
                return MinOnlineBookingThresholdPercent;
            }

            if (effective > MaxOnlineBookingThresholdPercent)
            {
                return MaxOnlineBookingThresholdPercent;
            }

            return effective;
        }

        private static int ComputeOnlineBookingLimit(int maxBays, decimal thresholdPercent)
        {
            if (maxBays <= 0)
            {
                return 0;
            }

            var normalizedThreshold = NormalizeOnlineBookingThreshold(thresholdPercent);
            var computed = (int)Math.Floor(maxBays * (double)normalizedThreshold);
            return Math.Max(1, computed);
        }

        private static CapacityProfile ApplyConfiguredCapacityLimit(CapacityProfile current, int configuredLimit)
        {
            var cappedTotal = Math.Max(0, Math.Min(current.Total, configuredLimit));
            if (cappedTotal >= current.Total)
            {
                return current;
            }

            if (current.Total <= 0)
            {
                return new CapacityProfile(0, 0, 0);
            }

            var quickRatio = current.Quick / (double)current.Total;
            var quickCap = Math.Min(current.Quick, Math.Max(0, (int)Math.Floor(cappedTotal * quickRatio)));
            var repairCap = Math.Min(current.Repair, Math.Max(0, cappedTotal - quickCap));
            return new CapacityProfile(quickCap, repairCap, cappedTotal);
        }
    }
}
