using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        // Availability & Capacity delegations
        public Task<IEnumerable<AvailableSlotResponseDto>> GetAvailableSlotsAsync(
            long branchId,
            DateOnly date,
            string? requestedServiceIds = null,
            string? requestedPackageIds = null,
            string? bookingFlow = null,
            int vehicleCount = 1,
            bool isParallel = true,
            bool ignoreMinLeadTime = false)
            => _availabilityService.GetAvailableSlotsAsync(
                branchId,
                date,
                requestedServiceIds,
                requestedPackageIds,
                bookingFlow,
                vehicleCount,
                isParallel,
                ignoreMinLeadTime);

        public Task<SlotHoldResponseDto> CreateSlotHoldAsync(SlotHoldRequestDto dto)
            => _availabilityService.CreateSlotHoldAsync(dto);

        public Task<bool> ReleaseSlotHoldAsync(string holdToken)
            => _availabilityService.ReleaseSlotHoldAsync(holdToken);

        public Task<NearestSlotSuggestionResponseDto> SuggestNearestSlotAsync(
            long branchId,
            DateTime requestedCheckInAt,
            string? requestedServiceIds = null,
            string? requestedPackageIds = null,
            string? bookingFlow = null,
            int vehicleCount = 1,
            bool isParallel = false,
            int searchDays = 3)
            => _availabilityService.SuggestNearestSlotAsync(
                branchId,
                requestedCheckInAt,
                requestedServiceIds,
                requestedPackageIds,
                bookingFlow,
                vehicleCount,
                isParallel,
                searchDays);

        public Task<BranchBookingHealthResponseDto> GetBranchBookingHealthAsync(long branchId, DateTime? at = null)
            => _availabilityService.GetBranchBookingHealthAsync(branchId, at);

        // Lifecycle, Status Flow, Overnight & Dispatch delegations
        public Task<Appointment?> ConfirmAsync(long id, long? actorUserId = null)
            => _lifecycleService.ConfirmAsync(id, actorUserId);

        public Task<Appointment?> CancelAsync(long id, string? reason, long? actorUserId = null)
            => _lifecycleService.CancelAsync(id, reason, actorUserId);

        public Task<Appointment?> CheckInAsync(long id, DateTime? arrivedAt = null, long? actorUserId = null)
            => _lifecycleService.CheckInAsync(id, arrivedAt, actorUserId);

        public Task<Appointment?> ArriveAsync(long id, DateTime? arrivedAt = null, long? actorUserId = null)
            => _lifecycleService.ArriveAsync(id, arrivedAt, actorUserId);

        public Task<WorkSlotDynamicExtensionResponseDto> ProcessDynamicExtensionAsync(
            long workSlotId,
            WorkSlotDynamicExtensionRequestDto dto,
            long? actorUserId = null)
            => _lifecycleService.ProcessDynamicExtensionAsync(workSlotId, dto, actorUserId);

        public Task<int> AutoMarkNoShowsAsync(long branchId, DateTime? now = null)
            => _lifecycleService.AutoMarkNoShowsAsync(branchId, now);

        public Task<DispatchDashboardResponseDto> GetDispatchDashboardAsync(long branchId, DateTime? at = null)
            => _lifecycleService.GetDispatchDashboardAsync(branchId, at);

        public Task<DispatchCycleResponseDto> RunDispatchCycleAsync(long branchId, DateTime? at = null)
            => _lifecycleService.RunDispatchCycleAsync(branchId, at);

        public Task<OvernightLockResponseDto> LockOvernightSlotsAsync(long branchId, DateOnly? businessDate = null)
            => _lifecycleService.LockOvernightSlotsAsync(branchId, businessDate);

        public Task<int> ReleaseExpiredLockedSlotsAsync(DateTime? now = null)
            => _lifecycleService.ReleaseExpiredLockedSlotsAsync(now);

        // Booking & Reception delegations
        public Task<Appointment> BookAsync(long customerId, BookAppointmentRequestDto dto)
            => _bookingService.BookAsync(customerId, dto);

        public Task<ReceptionAppointmentSaveResponseDto> SaveReceptionBookingAsync(long actorUserId, ReceptionAppointmentSaveRequestDto dto)
            => _bookingService.SaveReceptionBookingAsync(actorUserId, dto);

        public Task<GroupSessionResponseDto> BookBatchAsync(long customerId, BookAppointmentBatchRequestDto dto)
            => _bookingService.BookBatchAsync(customerId, dto);

        public Task<GroupSessionResponseDto> BookBatchForCustomerAsync(long actorUserId, long targetCustomerId, BookAppointmentBatchRequestDto dto)
            => _bookingService.BookBatchForCustomerAsync(actorUserId, targetCustomerId, dto);

        // Queries & Edit Capability delegations
        public Task<IEnumerable<AppointmentDetailResponseDto>> GetAllDetailedAsync(string? status = null)
            => _queryService.GetAllDetailedAsync(status);

        public Task<(IEnumerable<AppointmentDetailResponseDto> Items, int Total)> GetDetailedPageAsync(
            long? branchId,
            long? customerId,
            string? status,
            int page,
            int pageSize)
            => _queryService.GetDetailedPageAsync(branchId, customerId, status, page, pageSize);

        public Task<IEnumerable<AppointmentDetailResponseDto>> GetCustomerAppointmentsAsync(long customerId)
            => _queryService.GetCustomerAppointmentsAsync(customerId);

        public Task<IEnumerable<AppointmentDetailResponseDto>> GetBranchAppointmentsAsync(long branchId, string? status = null)
            => _queryService.GetBranchAppointmentsAsync(branchId, status);

        public Task<AppointmentDetailResponseDto?> GetDetailedAsync(long id)
            => _queryService.GetDetailedAsync(id);

        public Task<AppointmentEditCapabilityResponseDto?> GetEditCapabilityAsync(long id)
            => _queryService.GetEditCapabilityAsync(id);
    }
}
