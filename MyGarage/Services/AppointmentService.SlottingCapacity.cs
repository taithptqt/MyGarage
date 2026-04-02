using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private async Task<int> ResolveFixedDurationAsync(
            string? requestedServiceIds,
            int slotStepMinutes,
            DateTime? requestedEnd = null,
            DateTime? requestedStart = null,
            bool isParallel = false)
        {
            if (requestedStart.HasValue && requestedEnd.HasValue && requestedEnd > requestedStart)
            {
                return RoundUpToStep((int)Math.Ceiling((requestedEnd.Value - requestedStart.Value).TotalMinutes), slotStepMinutes);
            }

            if (string.IsNullOrWhiteSpace(requestedServiceIds)) return RoundUpToStep(60 + slotStepMinutes, slotStepMinutes);

            // Ho tro Batch Booking bang cach tach nhom bang ky tu '|'
            var serviceGroups = requestedServiceIds.Split('|', StringSplitOptions.RemoveEmptyEntries);
            int maxMinutes = 0;

            foreach (var group in serviceGroups)
            {
                var serviceIds = ParseServiceIds(group).ToList();
                if (!serviceIds.Any()) 
                {
                    maxMinutes = Math.Max(maxMinutes, 60);
                    continue;
                }

                var groupMinutes = await _context.StandardServices
                    .AsNoTracking()
                    .Where(s => serviceIds.Contains(s.Id))
                    .SumAsync(s => (int?)s.StandardMinutes) ?? 60;

                if (isParallel)
                {
                    maxMinutes = Math.Max(maxMinutes, groupMinutes);
                }
                else
                {
                    maxMinutes += groupMinutes;
                }
            }

            return RoundUpToStep(maxMinutes + slotStepMinutes, slotStepMinutes);
        }
    }
}
