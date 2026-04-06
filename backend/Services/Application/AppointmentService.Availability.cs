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
        public async Task<IEnumerable<AvailableSlotResponseDto>> GetAvailableSlotsAsync(
            long branchId,
            DateOnly date,
            string? requestedServiceIds = null,
            string? bookingFlow = null,
            int vehicleCount = 1,
            bool isParallel = true,
            bool ignoreMinLeadTime = false)
        {
            if (date < _clock.LocalToday)
            {
                return Enumerable.Empty<AvailableSlotResponseDto>();
            }

            var flow = await ResolveFlowAsync(bookingFlow, requestedServiceIds);
            var dayOfWeek = (int)date.DayOfWeek;

            var opHour = await _context.BranchOperatingHours
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.BranchId == branchId && h.DayOfWeek == dayOfWeek);

            var openTime = opHour?.OpenTime ?? new TimeOnly(8, 0);
            var closeTime = opHour?.CloseTime ?? new TimeOnly(20, 0);
            var slotStepMinutes = ResolveSlotStepMinutes(opHour?.SlotDurationMinutes);
            if (opHour?.IsDayOff == true)
            {
                return Enumerable.Empty<AvailableSlotResponseDto>();
            }

            var exception = await _context.BranchExceptions
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.BranchId == branchId && e.ExceptionDate == date);

            if (exception?.IsClosed == true) return Enumerable.Empty<AvailableSlotResponseDto>();
            if (exception?.StartTime.HasValue == true) openTime = exception.StartTime.Value;
            if (exception?.EndTime.HasValue == true) closeTime = exception.EndTime.Value;

            var effectiveRequestedServiceIds = requestedServiceIds;
            if (string.IsNullOrWhiteSpace(effectiveRequestedServiceIds))
            {
                effectiveRequestedServiceIds = await ResolveRequestedServiceIdsForBookingAsync(null);
            }

            if (string.IsNullOrWhiteSpace(effectiveRequestedServiceIds))
            {
                return Enumerable.Empty<AvailableSlotResponseDto>();
            }

            var effectiveVehicleCount = Math.Max(1, vehicleCount);
            var useParallel = isParallel;

            var durationBreakdown = await ResolveDurationBreakdownAsync(
                effectiveRequestedServiceIds,
                slotStepMinutes,
                useParallel);
            var requiredDuration = durationBreakdown.TotalMinutes;

            var slots = await BuildScheduledSlotsAsync(
                branchId,
                date,
                openTime,
                closeTime,
                slotStepMinutes,
                effectiveRequestedServiceIds,
                requiredDuration,
                effectiveVehicleCount,
                ignoreMinLeadTime);

            if (!slots.Any() &&
                useParallel &&
                HasGroupedRequestedServices(effectiveRequestedServiceIds))
            {
                durationBreakdown = await ResolveDurationBreakdownAsync(
                    effectiveRequestedServiceIds,
                    slotStepMinutes,
                    isParallel: false);
                requiredDuration = durationBreakdown.TotalMinutes;

                slots = await BuildScheduledSlotsAsync(
                    branchId,
                    date,
                    openTime,
                    closeTime,
                    slotStepMinutes,
                    effectiveRequestedServiceIds,
                    requiredDuration,
                    effectiveVehicleCount,
                    ignoreMinLeadTime);
                        }

            var dynamicNotice = flow == BookingFlowType.Flexible
                ? BuildFlexibleDynamicNotice(
                    durationBreakdown.ServiceMinutes,
                    durationBreakdown.SetupMinutes,
                    durationBreakdown.BufferMinutes,
                    requiredDuration)
                : null;

            foreach (var slot in slots)
            {
                slot.BookingFlow = AppointmentBookingFlows.Fixed;
                slot.EstimatedDurationMinutes = requiredDuration;
                slot.ServiceMinutes = durationBreakdown.ServiceMinutes;
                slot.SetupMinutes = durationBreakdown.SetupMinutes;
                slot.BufferMinutes = durationBreakdown.BufferMinutes;
                slot.DynamicNotice = dynamicNotice;
            }

            return slots.OrderBy(s => s.StartTime);
        }

        private static bool HasGroupedRequestedServices(string? requestedServiceIds)
        {
            return !string.IsNullOrWhiteSpace(requestedServiceIds)
                   && requestedServiceIds.Contains('|', StringComparison.Ordinal);
        }

    }
}
