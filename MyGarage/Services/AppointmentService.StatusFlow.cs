using System;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Constants;
using MyGarage.Enums;
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

        public async Task<Appointment?> CheckInAsync(long id, long? actorUserId = null)
        {
            var state = await LoadStatusContextAsync(id, actorUserId);
            if (state == null) return null;
            var (appointment, actorStaffId) = state.Value;
            _appointmentStatusPolicy.EnsureCanCheckIn(appointment);

            appointment.Status = AppointmentStatuses.Arrived;
            return await PersistStatusChangeAsync(id, appointment, actorStaffId);
        }

        public async Task<Appointment?> ArriveAsync(long id, DateTime? arrivedAt = null, long? actorUserId = null)
        {
            var state = await LoadStatusContextAsync(id, actorUserId);
            if (state == null) return null;
            var (appointment, actorStaffId) = state.Value;
            _appointmentStatusPolicy.EnsureCanArrive(appointment);

            var arriveTime = arrivedAt ?? _clock.LocalNow;
            var flow = ResolveFlowByAppointment(appointment);

            if (flow == BookingFlowType.Flexible)
            {
                ApplyFlexibleArrival(appointment, arriveTime);
                return await PersistStatusChangeAsync(id, appointment, actorStaffId);
            }

            if (arriveTime > appointment.StartTime.AddMinutes(NoShowMinutes))
            {
                appointment.Status = AppointmentStatuses.NoShow;
                appointment.CancelReason = AppointmentCancelReasons.AutoNoShowLateArrival;
                return await PersistStatusChangeAsync(id, appointment, actorStaffId);
            }

            if (arriveTime > appointment.StartTime.AddMinutes(GraceArrivalMinutes))
            {
                await ApplyLateFixedArrivalAsync(appointment, arriveTime);
                return await PersistStatusChangeAsync(id, appointment, actorStaffId);
            }

            appointment.Status = AppointmentStatuses.Arrived;
            return await PersistStatusChangeAsync(id, appointment, actorStaffId);
        }
    }
}
