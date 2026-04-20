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
                throw new InvalidOperationException("Chi nhánh không hợp lệ.");
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
                    Message = "Chưa xác định được dịch vụ để gợi ý khung giờ."
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
                    Message = "Không tìm thấy khung giờ trống phù hợp trong khoảng tìm kiếm hiện tại."
                };
            }

            var deltaMinutes = (int)Math.Ceiling((best.StartTime - baselineCheckInAt).TotalMinutes);
            var isExactOrEarlier = deltaMinutes <= 0;

            var message = isExactOrEarlier
                ? "Khung giờ xe vào xưởng đang khả dụng ngay."
                : $"Khung giờ phù hợp gần nhất lệch {deltaMinutes} phút so với giờ nhập.";

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
