using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private async Task<int> ResolveScheduledDurationAsync(
            string? requestedServiceIds,
            int slotStepMinutes,
            bool isParallel = false)
        {
            var bufferMinutes = Math.Max(15, slotStepMinutes);
            if (string.IsNullOrWhiteSpace(requestedServiceIds))
            {
                var defaultMinutes = await ResolveDefaultDiagnosticServiceMinutesAsync();
                return RoundUpToStep(defaultMinutes + bufferMinutes, slotStepMinutes);
            }

            var serviceGroups = requestedServiceIds.Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (serviceGroups.Length == 0)
                serviceGroups = new[] { requestedServiceIds };

            var aggregateMinutes = 0;

            foreach (var group in serviceGroups)
            {
                var serviceIds = ParseServiceIds(group).ToList();
                if (!serviceIds.Any())
                {
                    if (isParallel)
                    {
                        aggregateMinutes = Math.Max(aggregateMinutes, 60);
                    }
                    else
                    {
                        aggregateMinutes += 60;
                    }
                    continue;
                }

                var groupMinutes = await _context.StandardServices
                    .AsNoTracking()
                    .Where(s => serviceIds.Contains(s.Id) && s.IsDeleted != true && s.IsActive == true)
                    .SumAsync(s => (int?)s.StandardMinutes) ?? 60;

                if (isParallel)
                {
                    aggregateMinutes = Math.Max(aggregateMinutes, groupMinutes);
                }
                else
                {
                    aggregateMinutes += groupMinutes;
                }
            }

            if (aggregateMinutes <= 0)
            {
                aggregateMinutes = 60;
            }

            return RoundUpToStep(aggregateMinutes + bufferMinutes, slotStepMinutes);
        }

        private Task<int> ResolveFixedDurationAsync(
            string? requestedServiceIds,
            int slotStepMinutes,
            DateTime? requestedEnd = null,
            DateTime? requestedStart = null,
            bool isParallel = false)
        {
            return ResolveScheduledDurationAsync(requestedServiceIds, slotStepMinutes, isParallel);
        }
    }
}
