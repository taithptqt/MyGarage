using System;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Constants;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        public async Task<Appointment?> ConfirmAsync(long id, long? actorUserId = null)
        {
            var state = await LoadStatusContextAsync(id, actorUserId);
            if (state == null) return null;
            var (appointment, actorStaffId) = state.Value;
            _appointmentStatusPolicy.EnsureCanConfirm(appointment);

            appointment.Status = AppointmentStatuses.Confirmed;
            return await PersistStatusChangeAsync(id, appointment, actorStaffId);
        }

        public async Task<Appointment?> CancelAsync(long id, string? reason, long? actorUserId = null)
        {
            var state = await LoadStatusContextAsync(id, actorUserId);
            if (state == null) return null;
            var (appointment, actorStaffId) = state.Value;
            _appointmentStatusPolicy.EnsureCanCancel(appointment);

            var effectiveReason = _appointmentStatusPolicy.BuildCancelReason(appointment, reason, _clock.LocalNow);

            appointment.Status = AppointmentStatuses.Canceled;
            appointment.CancelReason = effectiveReason;
            return await PersistStatusChangeAsync(id, appointment, actorStaffId);
        }

        public async Task<Appointment?> CheckInAsync(long id, DateTime? arrivedAt = null, long? actorUserId = null)
        {
            var state = await LoadStatusContextAsync(id, actorUserId);
            if (state == null) return null;
            var (appointment, actorStaffId) = state.Value;
            _appointmentStatusPolicy.EnsureCanCheckIn(appointment);

            var arriveTime = arrivedAt ?? _clock.LocalNow;
            await ApplyArrivalStatusAsync(appointment, arriveTime);
            return await PersistStatusChangeAsync(id, appointment, actorStaffId);
        }

        public async Task<Appointment?> ArriveAsync(long id, DateTime? arrivedAt = null, long? actorUserId = null)
        {
            var state = await LoadStatusContextAsync(id, actorUserId);
            if (state == null) return null;
            var (appointment, actorStaffId) = state.Value;
            _appointmentStatusPolicy.EnsureCanArrive(appointment);

            var arriveTime = arrivedAt ?? _clock.LocalNow;
            await ApplyArrivalStatusAsync(appointment, arriveTime);
            return await PersistStatusChangeAsync(id, appointment, actorStaffId);
        }

        private async Task ApplyArrivalStatusAsync(Appointment appointment, DateTime arriveTime)
        {
            var flow = ResolveFlowByAppointment(appointment);
            if (flow == BookingFlowType.Flexible)
            {
                ApplyFlexibleArrival(appointment, arriveTime);
                return;
            }

            var lifecyclePolicy = ResolveLifecyclePolicy(appointment.BranchId, arriveTime);
            var lane = ResolveLifecycleLane(appointment);
            var cancelAfterMinutes = ResolveCancelAfterMinutes(lifecyclePolicy, lane, appointment.StartTime);

            if (arriveTime > appointment.StartTime.AddMinutes(cancelAfterMinutes))
            {
                appointment.Status = AppointmentStatuses.NoShow;
                appointment.CancelReason = AppointmentCancelReasons.AutoNoShowLateArrival;
                await IncrementNoShowCountAsync(appointment.CustomerId);
                return;
            }

            var arrivalOffsetMinutes = (arriveTime - appointment.StartTime).TotalMinutes;

            if (arrivalOffsetMinutes < -GraceArrivalMinutes)
            {
                await ApplyEarlyFixedArrivalAsync(appointment, arriveTime);
                return;
            }

            if (arrivalOffsetMinutes > GraceArrivalMinutes)
            {
                await ApplyLateFixedArrivalAsync(appointment, arriveTime);
                return;
            }

            appointment.Status = AppointmentStatuses.Arrived;
            appointment.CustomerNotes = AppendNoteMarker(appointment.CustomerNotes, "ON_TIME_ARRIVAL");
        }
    }
}
