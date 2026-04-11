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
        Task<IEnumerable<AppointmentDetailResponseDto>> GetAllDetailedAsync(string? status = null);
        Task<IEnumerable<AvailableSlotResponseDto>> GetAvailableSlotsAsync(
            long branchId,
            DateOnly date,
            string? requestedServiceIds = null,
            string? requestedPackageIds = null,
            string? bookingFlow = null,
            int vehicleCount = 1,
            bool isParallel = true,
            bool ignoreMinLeadTime = false);
        Task<SlotHoldResponseDto> CreateSlotHoldAsync(SlotHoldRequestDto dto);
        Task<bool> ReleaseSlotHoldAsync(string holdToken);
        Task<Appointment> BookAsync(long customerId, BookAppointmentRequestDto dto);
        Task<ReceptionAppointmentSaveResponseDto> SaveReceptionBookingAsync(long actorUserId, ReceptionAppointmentSaveRequestDto dto);
        Task<GroupSessionResponseDto> BookBatchAsync(long customerId, BookAppointmentBatchRequestDto dto);
        Task<GroupSessionResponseDto> BookBatchForCustomerAsync(long actorUserId, long targetCustomerId, BookAppointmentBatchRequestDto dto);
        Task<Appointment?> ConfirmAsync(long id, long? actorUserId = null);
        Task<Appointment?> CancelAsync(long id, string? reason, long? actorUserId = null);
        Task<Appointment?> CheckInAsync(long id, DateTime? arrivedAt = null, long? actorUserId = null);
        Task<Appointment?> ArriveAsync(long id, DateTime? arrivedAt = null, long? actorUserId = null);
        Task<WorkSlotDynamicExtensionResponseDto> ProcessDynamicExtensionAsync(long workSlotId, WorkSlotDynamicExtensionRequestDto dto, long? actorUserId = null);
        Task<int> AutoMarkNoShowsAsync(long branchId, DateTime? now = null);
        Task<BranchBookingHealthResponseDto> GetBranchBookingHealthAsync(long branchId, DateTime? at = null);
        Task<DispatchDashboardResponseDto> GetDispatchDashboardAsync(long branchId, DateTime? at = null);
        Task<DispatchCycleResponseDto> RunDispatchCycleAsync(long branchId, DateTime? at = null);
        Task<OvernightLockResponseDto> LockOvernightSlotsAsync(long branchId, DateOnly? businessDate = null);
        Task<int> ReleaseExpiredLockedSlotsAsync(DateTime? now = null);
        Task<NearestSlotSuggestionResponseDto> SuggestNearestSlotAsync(
            long branchId,
            DateTime requestedCheckInAt,
            string? requestedServiceIds = null,
            string? requestedPackageIds = null,
            string? bookingFlow = null,
            int vehicleCount = 1,
            bool isParallel = false,
            int searchDays = 3);
        Task<IEnumerable<AppointmentDetailResponseDto>> GetCustomerAppointmentsAsync(long customerId);
        Task<IEnumerable<AppointmentDetailResponseDto>> GetBranchAppointmentsAsync(long branchId, string? status = null);
        Task<AppointmentDetailResponseDto?> GetDetailedAsync(long id);
        Task<AppointmentEditCapabilityResponseDto?> GetEditCapabilityAsync(long id);
    }
}
