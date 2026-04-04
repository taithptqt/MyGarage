using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Responses;
using MyGarage.Constants;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private async Task<List<BranchSuggestionDto>> GetAlternativeBranchesAsync(long currentBranchId, DateTime at)
        {
            var current = await _context.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == currentBranchId && b.IsDeleted != true);
            var branches = await _context.Branches.AsNoTracking().Where(b => b.Id != currentBranchId && b.IsDeleted != true).ToListAsync();
            var output = new List<BranchSuggestionDto>();

            foreach (var branch in branches)
            {
                var bayCount = await _context.Bays.AsNoTracking()
                    .Where(b => b.BranchId == branch.Id && b.IsActive == true && b.IsDeleted != true)
                    .CountAsync();
                if (bayCount <= 0) bayCount = 20;

                var active = await _context.Appointments.AsNoTracking()
                    .Where(a => a.BranchId == branch.Id && a.StartTime < at.AddMinutes(15) && a.EndTime > at && !AppointmentStatusGroups.Closed.Contains(a.Status))
                    .CountAsync();
                var utilization = Math.Min(100d, active * 100d / bayCount);
                if (utilization >= 85d) continue;

                output.Add(new BranchSuggestionDto
                {
                    BranchId = branch.Id,
                    BranchName = branch.Name,
                    DistanceKm = HaversineDistanceKm(current?.Lat, current?.Lng, branch.Lat, branch.Lng),
                    EstimatedUtilizationPct = Math.Round(utilization, 1),
                    EarliestAvailableAt = at.AddMinutes(30)
                });
            }

            return output.OrderBy(b => b.DistanceKm).ThenBy(b => b.EstimatedUtilizationPct).Take(3).ToList();
        }

        private static double HaversineDistanceKm(decimal? lat1, decimal? lon1, decimal? lat2, decimal? lon2)
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

        private sealed record CapacityProfile(int Quick, int Repair, int Total);
        private sealed record SlotEvaluation(bool IsAvailable, int RemainingCapacity, int MaxCapacity, int QuickBooked, int HeavyBooked);

        private enum BookingFlowType
        {
            Fixed,
            Flexible
        }

        private enum ServiceCluster
        {
            Quick,
            Heavy
        }

    }
}



