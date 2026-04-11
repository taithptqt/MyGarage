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
            string? effectiveRequestedPackageIds,
            BookingFlowType flow,
            int requiredDurationMinutes,
            int vehicleCount = 1,
            bool ignoreMinLeadTime = false)
        {
            var activeBays = await _context.Bays
                .AsNoTracking()
                .Where(b => b.BranchId == branchId && b.IsActive == true && b.IsDeleted != true)
                .ToListAsync();
            var capacity = ComputeCapacity(activeBays);
            var capacityConfigs = await LoadCapacityConfigsForDayAsync(branchId, date);

            var requestedCluster = ServiceCluster.Quick;
            if (flow == BookingFlowType.Fixed)
            {
                requestedCluster = await ResolveServiceClusterAsync(effectiveRequestedServiceIds, effectiveRequestedPackageIds, null);
            }

            var dayStart = date.ToDateTime(TimeOnly.MinValue);
            var dayEnd = dayStart.AddDays(1);
            var appointments = await LoadAppointmentsForDateAsync(branchId, date);
            if (flow == BookingFlowType.Fixed)
            {
                appointments.AddRange(await LoadProjectedRepairReservationsAsync(branchId, dayStart, dayEnd));
            }
            var overnightLocks = await LoadOvernightLocksForDateAsync(branchId, date);
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

                var candidate = flow == BookingFlowType.Flexible
                    ? EvaluateFlexibleSlotCandidate(
                        branchId,
                        current,
                        requiredDurationMinutes,
                        date.ToDateTime(openTime),
                        appointments,
                        syntheticHoldAppointments,
                        activeBays,
                        overnightLocks,
                        capacityConfigs,
                        capacity,
                        vehicleCount,
                        slotStepMinutes)
                    : EvaluateFixedSlotCandidate(
                        branchId,
                        current,
                        requiredDurationMinutes,
                        date.ToDateTime(openTime),
                        appointments,
                        syntheticHoldAppointments,
                        activeBays,
                        overnightLocks,
                        capacityConfigs,
                        capacity,
                        requestedCluster,
                        vehicleCount,
                        slotStepMinutes);

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
