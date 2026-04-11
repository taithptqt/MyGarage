using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Constants;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private const int SlotHoldMinutes = 15;

        public async Task<SlotHoldResponseDto> CreateSlotHoldAsync(SlotHoldRequestDto dto)
        {
            if (dto.BranchId <= 0)
                throw new InvalidOperationException("Chi nh\u00E1nh kh\u00F4ng h\u1EE3p l\u1EC7.");

            var opHour = await _context.BranchOperatingHours
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.BranchId == dto.BranchId && h.DayOfWeek == (int)dto.StartTime.DayOfWeek);
            var slotStepMinutes = ResolveSlotStepMinutes(opHour?.SlotDurationMinutes);

            var flow = await ResolveFlowAsync(dto.BookingFlow, dto.RequestedServiceIds, dto.RequestedPackageIds);
            var plannedDuration = await ResolveScheduledDurationAsync(dto.RequestedServiceIds, slotStepMinutes, false, dto.RequestedPackageIds);

            var holdEndTime = dto.EndTime;

            if (flow == BookingFlowType.Flexible && holdEndTime <= dto.StartTime)
            {
                var flexibleWindow = ResolveFlexibleWindowMinutes(slotStepMinutes);
                holdEndTime = dto.StartTime.AddMinutes(flexibleWindow);
            }

            if (flow == BookingFlowType.Fixed && holdEndTime <= dto.StartTime)
            {
                holdEndTime = dto.StartTime.AddMinutes(plannedDuration);
            }

            if (holdEndTime <= dto.StartTime)
                throw new InvalidOperationException("Khung gi\u1EDD gi\u1EEF ch\u1ED7 kh\u00F4ng h\u1EE3p l\u1EC7.");

            var hold = _slotHoldService.CreateHold(
                dto.BranchId,
                dto.StartTime,
                holdEndTime,
                flow == BookingFlowType.Flexible ? AppointmentBookingFlows.Flexible : AppointmentBookingFlows.Fixed,
                dto.RequestedServiceIds,
                dto.RequestedPackageIds,
                Math.Max(1, dto.VehicleCount));

            return new SlotHoldResponseDto
            {
                HoldToken = hold.Token,
                ExpiresAt = hold.ExpiresAt,
                HoldMinutes = SlotHoldMinutes
            };
        }

        public Task<bool> ReleaseSlotHoldAsync(string holdToken)
        {
            var released = _slotHoldService.ReleaseHold(holdToken);
            return Task.FromResult(released);
        }

        private List<Appointment> BuildSyntheticAppointmentsFromHolds(IReadOnlyList<SlotHoldRecord> holds)
        {
            if (holds.Count == 0) return new List<Appointment>();

            var mapped = new List<Appointment>();
            foreach (var hold in holds)
            {
                var repeat = Math.Max(1, hold.VehicleCount);
                for (var i = 0; i < repeat; i++)
                {
                    mapped.Add(new Appointment
                    {
                        BranchId = hold.BranchId,
                        StartTime = hold.StartTime,
                        EndTime = hold.EndTime,
                        RequestedServiceIds = hold.RequestedServiceIds,
                        RequestedPackageIds = hold.RequestedPackageIds,
                        CustomerNotes = null,
                        Status = nameof(AppointmentStatus.PENDING)
                    });
                }
            }

            return mapped;
        }

        private void ValidateSlotHoldOrThrow(
            BookAppointmentRequestDto dto,
            BookingFlowType flow,
            DateTime requestedStartAt,
            DateTime requestedEndAt)
        {
            if (string.IsNullOrWhiteSpace(dto.SlotHoldToken))
            {
                return;
            }

            if (!_slotHoldService.TryGetActiveHold(dto.SlotHoldToken, out var hold) || hold == null)
            {
                throw new InvalidOperationException("Gi\u1EEF ch\u1ED7 \u0111\u00E3 h\u1EBFt h\u1EA1n. Vui l\u00F2ng ch\u1ECDn l\u1EA1i khung gi\u1EDD.");
            }

            var expectedFlow = flow == BookingFlowType.Flexible ? AppointmentBookingFlows.Flexible : AppointmentBookingFlows.Fixed;
            if (hold.BranchId != dto.BranchId ||
                !string.Equals(hold.BookingFlow, expectedFlow, StringComparison.OrdinalIgnoreCase) ||
                hold.StartTime != requestedStartAt)
            {
                throw new InvalidOperationException("Th\u00F4ng tin gi\u1EEF ch\u1ED7 kh\u00F4ng kh\u1EDBp v\u1EDBi l\u1ECBch \u0111ang \u0111\u1EB7t.");
            }

            // Dat lich nhom song song: moi xe co the co thoi luong dich vu khac nhau.
            // Hold thuong duoc tao theo khung gio chung (dai hon), nen chi can dam bao
            // lich gui len khong vuot qua khung gio da giu.
            if (flow == BookingFlowType.Fixed && requestedEndAt > hold.EndTime)
            {
                throw new InvalidOperationException("Khung gi\u1EDD gi\u1EEF ch\u1ED7 kh\u00F4ng c\u00F2n ph\u00F9 h\u1EE3p v\u1EDBi l\u1ECBch \u0111ang \u0111\u1EB7t. Vui l\u00F2ng ch\u1ECDn l\u1EA1i khung gi\u1EDD.");
            }
        }
    }
}
