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
        public async Task<IEnumerable<AvailableSlotResponseDto>> GetAvailableSlotsAsync(long branchId, DateOnly date, string? requestedServiceIds = null, string? bookingFlow = null)
        {
            if (date < _clock.LocalToday)
            {
                return Enumerable.Empty<AvailableSlotResponseDto>();
            }

            var flow = ResolveFlow(bookingFlow, requestedServiceIds);
            var requestedServiceIdList = ParseServiceIds(requestedServiceIds).Distinct().ToList();
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

            if (flow == BookingFlowType.Flexible)
            {
                return BuildFlexibleSlots(date, openTime, closeTime, slotStepMinutes);
            }

            if (flow == BookingFlowType.Fixed && requestedServiceIdList.Count == 0)
            {
                return Enumerable.Empty<AvailableSlotResponseDto>();
            }

            var effectiveRequestedServiceIds = requestedServiceIds;
            if (flow == BookingFlowType.Fixed && string.IsNullOrWhiteSpace(effectiveRequestedServiceIds))
            {
                effectiveRequestedServiceIds = await ResolveRequestedServiceIdsForBookingAsync(null);
            }

            var slots = await BuildFixedSlotsAsync(
                branchId,
                date,
                openTime,
                closeTime,
                slotStepMinutes,
                effectiveRequestedServiceIds);

            return slots.OrderBy(s => s.StartTime);
        }


    }
}
