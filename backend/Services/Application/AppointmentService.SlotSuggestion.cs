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
        public async Task<NearestSlotSuggestionResponseDto> SuggestNearestSlotAsync(
            long branchId,
            DateTime requestedCheckInAt,
            string? requestedServiceIds = null,
            string? requestedPackageIds = null,
            string? bookingFlow = null,
            int vehicleCount = 1,
            bool isParallel = false,
            int searchDays = 3)
        {
            const int suggestionLimit = 5;

            if (branchId <= 0)
            {
                throw new InvalidOperationException("Chi nh\u00E1nh kh\u00F4ng h\u1EE3p l\u1EC7.");
            }

            var baselineCheckInAt = requestedCheckInAt < _clock.LocalNow
                ? _clock.LocalNow
                : requestedCheckInAt;

            var effectiveRequestedServiceIds = requestedServiceIds;
            var effectiveRequestedPackageIds = requestedPackageIds;
            if (string.IsNullOrWhiteSpace(effectiveRequestedServiceIds))
            {
                effectiveRequestedServiceIds = await ResolveRequestedServiceIdsForBookingAsync(null, effectiveRequestedPackageIds);
            }

            if (string.IsNullOrWhiteSpace(effectiveRequestedServiceIds) && string.IsNullOrWhiteSpace(effectiveRequestedPackageIds))
            {
                return new NearestSlotSuggestionResponseDto
                {
                    BranchId = branchId,
                    RequestedCheckInAt = baselineCheckInAt,
                    RequestedServiceIds = requestedServiceIds,
                    RequestedPackageIds = requestedPackageIds,
                    Message = "Ch\u01B0a x\u00E1c \u0111\u1ECBnh \u0111\u01B0\u1EE3c d\u1ECBch v\u1EE5 \u0111\u1EC3 g\u1EE3i \u00FD khung gi\u1EDD."
                };
            }
            var resolvedFlow = await ResolveFlowAsync(bookingFlow, effectiveRequestedServiceIds, effectiveRequestedPackageIds);
            var effectiveBookingFlow = resolvedFlow == BookingFlowType.Flexible
                ? AppointmentBookingFlows.Flexible
                : AppointmentBookingFlows.Fixed;

            var opHour = await _context.BranchOperatingHours
                .AsNoTracking()
                .FirstOrDefaultAsync(h =>
                    h.BranchId == branchId &&
                    h.DayOfWeek == (int)baselineCheckInAt.DayOfWeek);
            var slotStepMinutes = ResolveSlotStepMinutes(opHour?.SlotDurationMinutes);
            var breakdown = await ResolveDurationBreakdownAsync(
                effectiveRequestedServiceIds,
                slotStepMinutes,
                isParallel,
                effectiveRequestedPackageIds);

            var candidateSlots = new List<AvailableSlotResponseDto>();
            var effectiveSearchDays = Math.Clamp(searchDays, 1, 7);
            var startDate = DateOnly.FromDateTime(baselineCheckInAt);
            var effectiveVehicleCount = Math.Max(1, vehicleCount);

            for (var i = 0; i < effectiveSearchDays; i++)
            {
                var date = startDate.AddDays(i);
                var slots = (await GetAvailableSlotsAsync(
                        branchId,
                        date,
                        effectiveRequestedServiceIds,
                        effectiveRequestedPackageIds,
                        effectiveBookingFlow,
                        effectiveVehicleCount,
                        isParallel,
                        ignoreMinLeadTime: true))
                    .ToList();

                if (slots.Count == 0)
                {
                    continue;
                }

                foreach (var slot in slots)
                {
                    if (slot.StartTime < baselineCheckInAt)
                    {
                        continue;
                    }

                    slot.ServiceMinutes = breakdown.ServiceMinutes;
                    slot.SetupMinutes = breakdown.SetupMinutes;
                    slot.BufferMinutes = breakdown.BufferMinutes;
                    slot.EstimatedDurationMinutes = breakdown.TotalMinutes;
                    candidateSlots.Add(slot);
                }

                if (candidateSlots.Count >= suggestionLimit)
                {
                    break;
                }
            }

            var ordered = candidateSlots
                .OrderBy(s => s.StartTime)
                .ToList();
            var best = ordered.FirstOrDefault();

            if (best == null)
            {
                return new NearestSlotSuggestionResponseDto
                {
                    BranchId = branchId,
                    RequestedCheckInAt = baselineCheckInAt,
                    RequestedServiceIds = requestedServiceIds,
                    RequestedPackageIds = requestedPackageIds,
                    ServiceMinutes = breakdown.ServiceMinutes,
                    SetupMinutes = breakdown.SetupMinutes,
                    BufferMinutes = breakdown.BufferMinutes,
                    EstimatedDurationMinutes = breakdown.TotalMinutes,
                    Message = "Kh\u00F4ng t\u00ECm th\u1EA5y khung gi\u1EDD tr\u1ED1ng ph\u00F9 h\u1EE3p trong kho\u1EA3ng t\u00ECm ki\u1EBFm hi\u1EC7n t\u1EA1i."
                };
            }

            var deltaMinutes = (int)Math.Ceiling((best.StartTime - baselineCheckInAt).TotalMinutes);
            var isExactOrEarlier = deltaMinutes <= 0;

            var message = isExactOrEarlier
                ? "Khung gi\u1EDD xe v\u00E0o x\u01B0\u1EDFng \u0111ang kh\u1EA3 d\u1EE5ng ngay."
                : $"Khung gi\u1EDD ph\u00F9 h\u1EE3p g\u1EA7n nh\u1EA5t l\u1EC7ch {deltaMinutes} ph\u00FAt so v\u1EDBi gi\u1EDD nh\u1EADp.";

            return new NearestSlotSuggestionResponseDto
            {
                BranchId = branchId,
                RequestedCheckInAt = baselineCheckInAt,
                RequestedServiceIds = requestedServiceIds,
                RequestedPackageIds = requestedPackageIds,
                ServiceMinutes = breakdown.ServiceMinutes,
                SetupMinutes = breakdown.SetupMinutes,
                BufferMinutes = breakdown.BufferMinutes,
                EstimatedDurationMinutes = breakdown.TotalMinutes,
                SuggestedStartTime = best.StartTime,
                SuggestedEndTime = best.EndTime,
                SuggestionDeltaMinutes = Math.Max(0, deltaMinutes),
                IsExactOrEarlier = isExactOrEarlier,
                Message = message,
                Alternatives = ordered.Take(suggestionLimit).ToList()
            };
        }
    }
}
