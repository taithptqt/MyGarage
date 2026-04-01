using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Constants;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private IEnumerable<AvailableSlotResponseDto> BuildFlexibleSlots(
            DateOnly date,
            TimeOnly openTime,
            TimeOnly closeTime,
            int slotStepMinutes)
        {
            var windows = new List<AvailableSlotResponseDto>();
            var windowCursor = date.ToDateTime(openTime);
            var windowEndLimit = date.ToDateTime(closeTime);
            while (windowCursor.AddHours(1) <= windowEndLimit)
            {
                windows.Add(new AvailableSlotResponseDto
                {
                    StartTime = windowCursor,
                    EndTime = windowCursor.AddHours(1),
                    IsAvailable = true,
                    IsRoundedHour = windowCursor.Minute == 0,
                    IsTopPick = windowCursor.Minute == 0,
                    Session = ResolveSession(windowCursor.TimeOfDay),
                    CompactionScore = windowCursor.Minute == 0 ? 100 : 80,
                    BookingFlow = AppointmentBookingFlows.Flexible
                });
                windowCursor = windowCursor.AddMinutes(slotStepMinutes);
            }

            return windows;
        }

        private async Task<List<AvailableSlotResponseDto>> BuildFixedSlotsAsync(
            long branchId,
            DateOnly date,
            TimeOnly openTime,
            TimeOnly closeTime,
            int slotStepMinutes,
            string? effectiveRequestedServiceIds)
        {
            var activeBays = await _context.Bays
                .AsNoTracking()
                .Where(b => b.BranchId == branchId && b.IsActive == true && b.IsDeleted != true)
                .ToListAsync();
            var capacity = ComputeCapacity(activeBays);

            var requestedCluster = await ResolveServiceClusterAsync(effectiveRequestedServiceIds, null);
            var requiredDuration = await ResolveFixedDurationAsync(effectiveRequestedServiceIds, slotStepMinutes);

            var appointments = await LoadAppointmentsForDateAsync(branchId, date);
            var overnightLocks = await LoadOvernightLocksForDateAsync(branchId, date);

            var slots = new List<AvailableSlotResponseDto>();
            var current = date.ToDateTime(openTime);
            var lastStart = date.ToDateTime(closeTime).AddMinutes(-requiredDuration);
            var minLeadMinutes = Math.Max(15, slotStepMinutes);
            while (current <= lastStart)
            {
                if (ShouldSkipByLeadTime(date, current, minLeadMinutes))
                {
                    current = current.AddMinutes(slotStepMinutes);
                    continue;
                }

                var candidate = EvaluateFixedSlotCandidate(
                    current,
                    requiredDuration,
                    date.ToDateTime(openTime),
                    appointments,
                    activeBays,
                    overnightLocks,
                    capacity,
                    requestedCluster);

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
