using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public Task<SlotHoldResponseDto> CreateSlotHoldAsync(SlotHoldRequestDto dto)
        {
            if (dto.BranchId <= 0)
                throw new InvalidOperationException("Chi nhánh không hợp lệ.");
            if (dto.EndTime <= dto.StartTime)
                throw new InvalidOperationException("Khung giờ giữ chỗ không hợp lệ.");

            var flow = ResolveFlow(dto.BookingFlow, dto.RequestedServiceIds);
            var hold = _slotHoldService.CreateHold(
                dto.BranchId,
                dto.StartTime,
                dto.EndTime,
                flow == BookingFlowType.Flexible ? AppointmentBookingFlows.Flexible : AppointmentBookingFlows.Fixed,
                dto.RequestedServiceIds,
                Math.Max(1, dto.VehicleCount));

            return Task.FromResult(new SlotHoldResponseDto
            {
                HoldToken = hold.Token,
                ExpiresAt = hold.ExpiresAt,
                HoldMinutes = SlotHoldMinutes
            });
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
                throw new InvalidOperationException("Giữ chỗ đã hết hạn. Vui lòng chọn lại khung giờ.");
            }

            var expectedFlow = flow == BookingFlowType.Flexible ? AppointmentBookingFlows.Flexible : AppointmentBookingFlows.Fixed;
            if (hold.BranchId != dto.BranchId ||
                !string.Equals(hold.BookingFlow, expectedFlow, StringComparison.OrdinalIgnoreCase) ||
                hold.StartTime != requestedStartAt ||
                hold.EndTime != requestedEndAt)
            {
                throw new InvalidOperationException("Thông tin giữ chỗ không khớp với lịch đang đặt.");
            }
        }
    }
}
