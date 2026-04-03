using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Constants;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private const int SlotHoldMinutes = 5;

        public async Task<SlotHoldResponseDto> CreateSlotHoldAsync(SlotHoldRequestDto dto)
        {
            if (dto.BranchId <= 0)
                throw new InvalidOperationException("Chi nh\u00E1nh kh\u00F4ng h\u1EE3p l\u1EC7.");

            var flow = await ResolveFlowAsync(dto.BookingFlow, dto.RequestedServiceIds);
            var holdEndTime = dto.EndTime;

            if (flow == BookingFlowType.Flexible && holdEndTime <= dto.StartTime)
            {
                var opHour = await _context.BranchOperatingHours
                    .AsNoTracking()
                    .FirstOrDefaultAsync(h => h.BranchId == dto.BranchId && h.DayOfWeek == (int)dto.StartTime.DayOfWeek);
                var slotStepMinutes = ResolveSlotStepMinutes(opHour?.SlotDurationMinutes);
                var flexibleDuration = await ResolveScheduledDurationAsync(dto.RequestedServiceIds, slotStepMinutes, isParallel: false);
                holdEndTime = dto.StartTime.AddMinutes(flexibleDuration);
            }

            if (holdEndTime <= dto.StartTime)
                throw new InvalidOperationException("Khung gi\u1EDD gi\u1EEF ch\u1ED7 kh\u00F4ng h\u1EE3p l\u1EC7.");

            var hold = _slotHoldService.CreateHold(
                dto.BranchId,
                dto.StartTime,
                holdEndTime,
                flow == BookingFlowType.Flexible ? AppointmentBookingFlows.Flexible : AppointmentBookingFlows.Fixed,
                dto.RequestedServiceIds,
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
                        CustomerNotes = null,
                        Status = AppointmentStatuses.Pending
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

            // Với đặt lịch nhóm song song, mỗi xe có thể có thời lượng dịch vụ khác nhau.
            // Hold thường được tạo theo khung giờ chung (dài hơn), nên chỉ cần đảm bảo
            // lịch gửi lên không vượt quá khung giờ đã giữ.
            if (flow == BookingFlowType.Fixed && requestedEndAt > hold.EndTime)
            {
                throw new InvalidOperationException("Khung giờ giữ chỗ không còn phù hợp với lịch đang đặt. Vui lòng chọn lại khung giờ.");
            }
        }
    }
}
