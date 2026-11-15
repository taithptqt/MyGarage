using System;
using System.Collections.Generic;
using System.Linq;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    internal static class AppointmentAvailabilityHelpers
    {
        internal static List<Appointment> BuildSyntheticAppointmentsFromHolds(IReadOnlyList<SlotHoldRecord> holds)
        {
            if (holds.Count == 0)
            {
                return new List<Appointment>();
            }

            var mapped = new List<Appointment>();
            foreach (var hold in holds)
            {
                var repeat = Math.Max(1, hold.VehicleCount);
                for (var i = 0; i < repeat; i++)
                {
                    mapped.Add(new Appointment
                    {
                        BranchId = hold.BranchId,
                        StartTime = hold.StartTime,
                        EndTime = hold.EndTime,
                        RequestedServiceIds = hold.RequestedServiceIds,
                        RequestedPackageIds = hold.RequestedPackageIds,
                        CustomerNotes = null,
                        Status = nameof(AppointmentStatus.PENDING)
                    });
                }
            }

            return mapped;
        }

        internal static string BuildFlexibleDynamicNotice(
            int serviceMinutes,
            int setupMinutes,
            int bufferMinutes,
            int estimatedMinutes)
        {
            return $"Tổng thời gian dự kiến {estimatedMinutes} phút " +
                   $"(sửa chữa {serviceMinutes}p + tiếp nhận/chụp ảnh {setupMinutes}p + buffer {bufferMinutes}p). " +
                   "Thời gian hoàn tất thực tế sẽ được cập nhật sau khi kỹ thuật viên hoàn tất bước kiểm tra ban đầu.";
        }

        internal static bool HasGroupedRequestedServices(string? requestedServiceIds, string? requestedPackageIds = null)
        {
            return (!string.IsNullOrWhiteSpace(requestedServiceIds)
                    && requestedServiceIds.Contains('|', StringComparison.Ordinal)) ||
                   (!string.IsNullOrWhiteSpace(requestedPackageIds)
                    && requestedPackageIds.Contains('|', StringComparison.Ordinal));
        }

        internal static string ResolveSession(TimeSpan time)
        {
            if (time < new TimeSpan(12, 0, 0)) return "MORNING";
            if (time < new TimeSpan(17, 0, 0)) return "AFTERNOON";
            return "EVENING";
        }

        internal static void MarkTopPicks(List<AvailableSlotResponseDto> slots)
        {
            foreach (var sessionGroup in slots.GroupBy(s => s.Session))
            {
                var rounded = sessionGroup
                    .Where(s => s.IsRoundedHour)
                    .OrderByDescending(s => s.CompactionScore)
                    .ThenBy(s => s.StartTime)
                    .Take(5)
                    .ToList();

                var picks = new List<AvailableSlotResponseDto>(rounded);
                if (picks.Count < 3)
                {
                    picks.AddRange(sessionGroup
                        .Where(s => !picks.Contains(s))
                        .OrderByDescending(s => s.CompactionScore)
                        .ThenBy(s => s.StartTime)
                        .Take(3 - picks.Count));
                }

                foreach (var slot in picks)
                {
                    slot.IsTopPick = true;
                }
            }
        }

        internal static int CalculateCompactionScore(DateTime slotStart, DateTime openAt, List<Appointment> overlap)
        {
            var score = slotStart.Minute == 0 ? 30 : 0;
            if (slotStart == openAt) score += 20;
            var nearestEnd = overlap.Select(o => Math.Abs((o.EndTime - slotStart).TotalMinutes)).DefaultIfEmpty(999).Min();
            if (nearestEnd <= 15) score += 40;
            else if (nearestEnd <= 30) score += 20;
            return score;
        }

        internal static double HaversineDistanceKm(decimal? lat1, decimal? lon1, decimal? lat2, decimal? lon2)
        {
            if (!lat1.HasValue || !lon1.HasValue || !lat2.HasValue || !lon2.HasValue) return 9999;
            const double r = 6371d;
            var dLat = ToRadians((double)(lat2.Value - lat1.Value));
            var dLon = ToRadians((double)(lon2.Value - lon1.Value));
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                    + Math.Cos(ToRadians((double)lat1.Value)) * Math.Cos(ToRadians((double)lat2.Value))
                    * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return Math.Round(r * c, 1);
        }

        private static double ToRadians(double v) => v * Math.PI / 180d;
    }
}
