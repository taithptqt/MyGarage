using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IAppointmentReceptionService
    {
        Task EnsureInsuranceCompanyExistsAsync(long? insuranceCompanyId);

        Task<long?> ResolveVehicleModelIdForBookingAsync(long customerId, BookAppointmentRequestDto dto);

        Task ValidateSelectedVehicleOwnershipAsync(long customerId, long? vehicleId);

        void UpdateCustomerContact(Customer customer, BookAppointmentRequestDto dto);

        Task<long?> ResolveVehicleForBookingAsync(long customerId, long? actorStaffId, BookAppointmentRequestDto dto);

        decimal? NormalizeInsuranceDeductibleAmount(decimal? value);

        string? NormalizeInsuranceToken(string? value);

        Task PersistAppointmentInsuranceAssetsAsync(Appointment appointment, BookAppointmentRequestDto dto);

        Task ValidateReceptionSaveRequestAsync(ReceptionAppointmentSaveRequestDto dto);

        Task<long> ResolveReceptionCustomerIdAsync(ReceptionAppointmentSaveRequestDto dto, long? actorStaffId);

        Task<BookAppointmentRequestDto> BuildReceptionBookRequestAsync(
            ReceptionAppointmentSaveRequestDto dto,
            ReceptionAppointmentVehiclePlanItemDto plan,
            long customerId,
            long? actorStaffId);

        Task CompleteReceptionReplacementAsync(
            long actorUserId,
            long replaceAppointmentId,
            IReadOnlyList<Appointment> createdAppointments);

        Task<ReceptionAppointmentSaveResponseDto> BuildReceptionSaveResponseAsync(
            long customerId,
            long? groupSessionId,
            bool updateMode,
            IReadOnlyList<Appointment> createdAppointments);
    }
}
