using System;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class AppointmentLifecycleService : IAppointmentLifecycleService
    {
        private readonly IAppointmentStatusPersistenceService _statusPersistenceService;
        private readonly IAppointmentStatusPolicy _appointmentStatusPolicy;
        private readonly IWorkSlotExtensionService _workSlotExtensionService;
        private readonly IAppointmentCheckInService _checkInService;
        private readonly IAppointmentNoShowService _noShowService;
        private readonly IAppointmentDispatchService _dispatchService;

        public AppointmentLifecycleService(
            IAppointmentStatusPersistenceService statusPersistenceService,
            IAppointmentStatusPolicy appointmentStatusPolicy,
            IWorkSlotExtensionService workSlotExtensionService,
            IAppointmentCheckInService checkInService,
            IAppointmentNoShowService noShowService,
            IAppointmentDispatchService dispatchService)
        {
            _statusPersistenceService = statusPersistenceService;
            _appointmentStatusPolicy = appointmentStatusPolicy;
            _workSlotExtensionService = workSlotExtensionService;
            _checkInService = checkInService;
            _noShowService = noShowService;
            _dispatchService = dispatchService;
        }

        public async Task<Appointment?> ConfirmAsync(long id, long? actorUserId = null)
        {
            var state = await _statusPersistenceService.LoadStatusContextAsync(id, actorUserId);
            if (state == null) return null;
            var (appointment, actorStaffId) = state.Value;
            _appointmentStatusPolicy.EnsureCanConfirm(appointment);
            await _checkInService.EnsurePartsReadyForConfirmationAsync(appointment, actorStaffId);

            appointment.Status = nameof(AppointmentStatus.CONFIRMED);
            return await _statusPersistenceService.PersistStatusChangeAsync(id, appointment, actorStaffId);
        }

        public async Task<Appointment?> CancelAsync(long id, string? reason, long? actorUserId = null)
        {
            var state = await _statusPersistenceService.LoadStatusContextAsync(id, actorUserId);
            if (state == null) return null;
            var (appointment, actorStaffId) = state.Value;
            _appointmentStatusPolicy.EnsureCanCancel(appointment);

            var localNow = _statusPersistenceService.LocalNow;
            var effectiveReason = _appointmentStatusPolicy.BuildCancelReason(appointment, reason, localNow);
            return await _statusPersistenceService.ExecuteInTransactionAsync(async () =>
            {
                await _noShowService.ReleaseBookingReservationArtifactsAsync(
                    appointment.Id,
                    appointment.BranchId,
                    actorStaffId,
                    localNow);

                appointment.Status = nameof(AppointmentStatus.CANCELED);
                appointment.CancelReason = effectiveReason;
                return await _statusPersistenceService.PersistStatusChangeAsync(id, appointment, actorStaffId);
            });
        }

        public async Task<Appointment?> CheckInAsync(long id, DateTime? arrivedAt = null, long? actorUserId = null)
        {
            var state = await _statusPersistenceService.LoadStatusContextAsync(id, actorUserId);
            if (state == null) return null;
            var (appointment, actorStaffId) = state.Value;
            _appointmentStatusPolicy.EnsureCanCheckIn(appointment);

            return await CompleteArrivalAsync(id, appointment, actorStaffId, arrivedAt);
        }

        public async Task<Appointment?> ArriveAsync(long id, DateTime? arrivedAt = null, long? actorUserId = null)
        {
            var state = await _statusPersistenceService.LoadStatusContextAsync(id, actorUserId);
            if (state == null) return null;
            var (appointment, actorStaffId) = state.Value;
            _appointmentStatusPolicy.EnsureCanArrive(appointment);

            return await CompleteArrivalAsync(id, appointment, actorStaffId, arrivedAt);
        }

        public Task<WorkSlotDynamicExtensionResponseDto> ProcessDynamicExtensionAsync(
            long workSlotId,
            WorkSlotDynamicExtensionRequestDto dto,
            long? actorUserId = null)
        {
            return _workSlotExtensionService.ProcessDynamicExtensionAsync(workSlotId, dto, actorUserId);
        }

        public Task<int> AutoMarkNoShowsAsync(long branchId, DateTime? now = null)
        {
            return _noShowService.AutoMarkNoShowsAsync(branchId, now);
        }

        public Task<DispatchDashboardResponseDto> GetDispatchDashboardAsync(long branchId, DateTime? at = null)
        {
            return _dispatchService.GetDispatchDashboardAsync(branchId, at);
        }

        public Task<DispatchCycleResponseDto> RunDispatchCycleAsync(long branchId, DateTime? at = null)
        {
            return _dispatchService.RunDispatchCycleAsync(branchId, at);
        }

        public Task<OvernightLockResponseDto> LockOvernightSlotsAsync(long branchId, DateOnly? businessDate = null)
        {
            return _dispatchService.LockOvernightSlotsAsync(branchId, businessDate);
        }

        public Task<int> ReleaseExpiredLockedSlotsAsync(DateTime? now = null)
        {
            return _dispatchService.ReleaseExpiredLockedSlotsAsync(now);
        }

        private Task<Appointment?> CompleteArrivalAsync(long id, Appointment appointment, long? actorStaffId, DateTime? arrivedAt)
        {
            var arriveTime = arrivedAt ?? _statusPersistenceService.LocalNow;
            return _statusPersistenceService.ExecuteInTransactionAsync(async () =>
            {
                await _checkInService.ApplyArrivalStatusAsync(appointment, arriveTime);
                var updated = await _statusPersistenceService.PersistStatusChangeAsync(id, appointment, actorStaffId);
                if (updated != null && string.Equals(updated.Status, nameof(AppointmentStatus.NO_SHOW), StringComparison.OrdinalIgnoreCase))
                {
                    await _noShowService.ReleaseBookingReservationArtifactsAsync(updated.Id, updated.BranchId, actorStaffId, arriveTime);
                }
                if (updated != null)
                {
                    await _checkInService.EnsureServiceOrderExistsForArrivedAppointmentAsync(updated, arriveTime, actorStaffId);
                }

                return updated;
            });
        }
    }
}
