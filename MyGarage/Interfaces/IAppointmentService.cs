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
        Task<IEnumerable<AvailableSlotResponseDto>> GetAvailableSlotsAsync(long branchId, DateOnly date, string? requestedServiceIds = null, string? bookingFlow = null);
        Task<Appointment> BookAsync(long customerId, BookAppointmentRequestDto dto);
        Task<Appointment?> ConfirmAsync(long id);
        Task<Appointment?> CancelAsync(long id, string? reason);
        Task<Appointment?> CheckInAsync(long id);
        Task<Appointment?> ArriveAsync(long id, DateTime? arrivedAt = null);
        Task<int> AutoMarkNoShowsAsync(long branchId, DateTime? now = null);
        Task<BranchBookingHealthResponseDto> GetBranchBookingHealthAsync(long branchId, DateTime? at = null);
        Task<DispatchDashboardResponseDto> GetDispatchDashboardAsync(long branchId, DateTime? at = null);
        Task<DispatchCycleResponseDto> RunDispatchCycleAsync(long branchId, DateTime? at = null);
        Task<OvernightLockResponseDto> LockOvernightSlotsAsync(long branchId, DateOnly? businessDate = null);
        Task<IEnumerable<AppointmentDetailResponseDto>> GetCustomerAppointmentsAsync(long customerId);
        Task<IEnumerable<AppointmentDetailResponseDto>> GetBranchAppointmentsAsync(long branchId, string? status = null);
        Task<AppointmentDetailResponseDto?> GetDetailedAsync(long id);
    }
}
