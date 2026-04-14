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
            bool isParallel = false,
            string? requestedPackageIds = null)
        {
            var normalizedStep = ResolveSlotStepMinutes(slotStepMinutes);
            var serviceMinutes = await ResolveServiceWorkMinutesAsync(requestedServiceIds, isParallel, requestedPackageIds);
            var setupMinutes = Math.Max(0, StandardCheckInSetupMinutes);
            var bufferMinutes = Math.Max(ExecutionBufferMinutes, normalizedStep);
            var totalMinutes = RoundUpToStep(serviceMinutes + setupMinutes + bufferMinutes, normalizedStep);
            return (serviceMinutes, setupMinutes, bufferMinutes, totalMinutes);
        }

        private async Task<int> ResolveScheduledDurationAsync(
            string? requestedServiceIds,
            int slotStepMinutes,
            bool isParallel = false,
            string? requestedPackageIds = null)
        {
            var breakdown = await ResolveDurationBreakdownAsync(requestedServiceIds, slotStepMinutes, isParallel, requestedPackageIds);
            return breakdown.TotalMinutes;
        }

        private async Task<int> ResolveServiceWorkMinutesAsync(
            string? requestedServiceIds,
            bool isParallel,
            string? requestedPackageIds = null)
        {
            if (string.IsNullOrWhiteSpace(requestedServiceIds) && string.IsNullOrWhiteSpace(requestedPackageIds))
            {
                var defaultMinutes = await ResolveDefaultDiagnosticServiceMinutesAsync();
                return Math.Max(30, defaultMinutes);
            }

            var serviceGroups = SplitRequestedGroups(requestedServiceIds);
            var packageGroups = SplitRequestedGroups(requestedPackageIds);
            var groupCount = Math.Max(serviceGroups.Count, packageGroups.Count);
            if (groupCount == 0)
            {
                groupCount = 1;
            }

            var aggregateMinutes = 0;
            var hasExplicitServiceIds = false;

            for (var index = 0; index < groupCount; index++)
            {
                var serviceGroup = index < serviceGroups.Count ? serviceGroups[index] : string.Empty;
                var packageGroup = index < packageGroups.Count ? packageGroups[index] : string.Empty;
                var serviceIds = ParseServiceIds(serviceGroup).ToList();
                var packageIds = ParsePackageIds(packageGroup).ToList();
                if (!serviceIds.Any() && !packageIds.Any())
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

                hasExplicitServiceIds = true;

                var serviceRows = await _context.StandardServices
                    .AsNoTracking()
                    .Where(s => serviceIds.Contains(s.Id) && s.IsDeleted != true && s.IsActive == true)
                    .Select(s => new
                    {
                        s.Code,
                        s.Name,
                        s.StandardMinutes
                    })
                    .ToListAsync();

                var groupMinutes = serviceRows
                    .Where(s => !IsMiniCheckService(s.Code, s.Name))
                    .Sum(s => Math.Max(0, s.StandardMinutes));

                if (serviceRows.Count == 0)
                {
                    groupMinutes = packageIds.Count > 0 ? 0 : 60;
                }

                groupMinutes += await ResolvePackageWorkMinutesAsync(packageIds);
                if (groupMinutes == 0 && packageIds.Count > 0)
                {
                    groupMinutes = 60;
                }

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
                aggregateMinutes = hasExplicitServiceIds ? 0 : 60;
            }

            return aggregateMinutes;
        }

        private Task<int> ResolveFixedDurationAsync(
            string? requestedServiceIds,
            int slotStepMinutes,
            DateTime? requestedEnd = null,
            DateTime? requestedStart = null,
            bool isParallel = false,
            string? requestedPackageIds = null)
        {
            return ResolveScheduledDurationAsync(requestedServiceIds, slotStepMinutes, isParallel, requestedPackageIds);
        }
    }
}
