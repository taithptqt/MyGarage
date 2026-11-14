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
        private const int SlotHoldMinutes = 15;

        public async Task<SlotHoldResponseDto> CreateSlotHoldAsync(SlotHoldRequestDto dto)
        {
            if (dto.BranchId <= 0)
                throw new InvalidOperationException("Chi nhánh không hợp lệ.");

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
                throw new InvalidOperationException("Khung giờ giữ chỗ không hợp lệ.");

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
                throw new InvalidOperationException("Giữ chỗ đã hết hạn. Vui lòng chọn lại khung giờ.");
            }

            var expectedFlow = flow == BookingFlowType.Flexible ? AppointmentBookingFlows.Flexible : AppointmentBookingFlows.Fixed;
            if (hold.BranchId != dto.BranchId ||
                !string.Equals(hold.BookingFlow, expectedFlow, StringComparison.OrdinalIgnoreCase) ||
                hold.StartTime != requestedStartAt)
            {
                throw new InvalidOperationException("Thông tin giữ chỗ không khớp với lịch đang đặt.");
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
