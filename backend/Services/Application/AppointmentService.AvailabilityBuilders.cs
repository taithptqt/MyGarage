using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Constants;
using MyGarage.DTOs.Responses;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private async Task<List<AvailableSlotResponseDto>> BuildScheduledSlotsAsync(
            long branchId,
            DateOnly date,
            TimeOnly openTime,
            TimeOnly closeTime,
            int slotStepMinutes,
            string? effectiveRequestedServiceIds,
            int requiredDurationMinutes,
            int vehicleCount = 1,
            bool ignoreMinLeadTime = false)
        {
            var activeBays = await _context.Bays
                .AsNoTracking()
                .Where(b => b.BranchId == branchId && b.IsActive == true && b.IsDeleted != true)
                .ToListAsync();
            var capacity = ComputeCapacity(activeBays);

            var requestedCluster = await ResolveServiceClusterAsync(effectiveRequestedServiceIds, null);

            var appointments = await LoadAppointmentsForDateAsync(branchId, date);
            var overnightLocks = await LoadOvernightLocksForDateAsync(branchId, date);
            var dayStart = date.ToDateTime(TimeOnly.MinValue);
            var dayEnd = dayStart.AddDays(1);
            var fixedHolds = _slotHoldService.GetActiveHolds(branchId, dayStart, dayEnd, AppointmentBookingFlows.Fixed);
            var flexibleHolds = _slotHoldService.GetActiveHolds(branchId, dayStart, dayEnd, AppointmentBookingFlows.Flexible);
            var mergedHolds = fixedHolds.Concat(flexibleHolds).ToList();
            var syntheticHoldAppointments = BuildSyntheticAppointmentsFromHolds(mergedHolds);

            var slots = new List<AvailableSlotResponseDto>();
            var current = date.ToDateTime(openTime);
            var lastStart = date.ToDateTime(closeTime).AddMinutes(-requiredDurationMinutes);
            var minLeadMinutes = ignoreMinLeadTime ? 0 : Math.Max(15, slotStepMinutes);
            while (current <= lastStart)
            {
                if (ShouldSkipByLeadTime(date, current, minLeadMinutes))
                {
                    current = current.AddMinutes(slotStepMinutes);
                    continue;
                }

                var candidate = EvaluateFixedSlotCandidate(
                    current,
                    requiredDurationMinutes,
                    date.ToDateTime(openTime),
                    appointments,
                    syntheticHoldAppointments,
                    activeBays,
                    overnightLocks,
                    capacity,
                    requestedCluster,
                    vehicleCount);

                if (candidate != null)
                {
                    slots.Add(candidate);
                }

                current = current.AddMinutes(slotStepMinutes);
            }

            MarkTopPicks(slots);
            return slots;
        }
    }
}
