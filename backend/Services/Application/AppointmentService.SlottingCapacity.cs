using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private async Task<(int ServiceMinutes, int SetupMinutes, int BufferMinutes, int TotalMinutes)> ResolveDurationBreakdownAsync(
            string? requestedServiceIds,
            int slotStepMinutes,
            bool isParallel = false)
        {
            var normalizedStep = ResolveSlotStepMinutes(slotStepMinutes);
            var serviceMinutes = await ResolveServiceWorkMinutesAsync(requestedServiceIds, isParallel);
            var setupMinutes = Math.Max(0, StandardCheckInSetupMinutes);
            var bufferMinutes = Math.Max(ExecutionBufferMinutes, normalizedStep);
            var totalMinutes = RoundUpToStep(serviceMinutes + setupMinutes + bufferMinutes, normalizedStep);
            return (serviceMinutes, setupMinutes, bufferMinutes, totalMinutes);
        }

        private async Task<int> ResolveScheduledDurationAsync(
            string? requestedServiceIds,
            int slotStepMinutes,
            bool isParallel = false)
        {
            var breakdown = await ResolveDurationBreakdownAsync(requestedServiceIds, slotStepMinutes, isParallel);
            return breakdown.TotalMinutes;
        }

        private async Task<int> ResolveServiceWorkMinutesAsync(
            string? requestedServiceIds,
            bool isParallel)
        {
            if (string.IsNullOrWhiteSpace(requestedServiceIds))
            {
                var defaultMinutes = await ResolveDefaultDiagnosticServiceMinutesAsync();
                return Math.Max(30, defaultMinutes);
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

            return aggregateMinutes;
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
