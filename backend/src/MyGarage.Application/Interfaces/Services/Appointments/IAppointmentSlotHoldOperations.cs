using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Services;

namespace MyGarage.Interfaces
{
    public interface IAppointmentSlotHoldOperations
    {
        Task<SlotHoldResponseDto> CreateSlotHoldAsync(SlotHoldRequestDto dto);
        Task<bool> ReleaseSlotHoldAsync(string holdToken);

        void ValidateSlotHoldOrThrow(
            BookAppointmentRequestDto dto,
            BookingFlowType flow,
            DateTime requestedStartAt,
            DateTime requestedEndAt);

        void ConsumeSlotHold(string? holdToken);

        List<Appointment> BuildSyntheticAppointmentsFromHolds(IReadOnlyList<SlotHoldRecord> holds);
    }
}
