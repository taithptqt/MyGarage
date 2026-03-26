using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public interface IAppointmentService : IGenericService<Appointment>
    {
        Task<IEnumerable<AvailableSlotResponseDto>> GetAvailableSlotsAsync(long branchId, DateOnly date, string? requestedServiceIds);
        Task<Appointment> BookAsync(long customerId, BookAppointmentRequestDto dto, long? createdByStaffId = null);
        Task<Appointment> BookGuestAsync(BookAppointmentRequestDto dto);
        Task<Appointment?> ConfirmAsync(long id);
        Task<Appointment?> CancelAsync(long id, string? reason);
        Task<Appointment?> CheckInAsync(long id);
        Task<IEnumerable<AppointmentDetailResponseDto>> GetCustomerAppointmentsAsync(long customerId);
        Task<IEnumerable<AppointmentDetailResponseDto>> GetAllAppointmentsAsync(string? status = null);
        Task<IEnumerable<AppointmentDetailResponseDto>> GetBranchAppointmentsAsync(long branchId, string? status = null);
        Task<AppointmentDetailResponseDto?> GetDetailedAsync(long id);
    }
}
