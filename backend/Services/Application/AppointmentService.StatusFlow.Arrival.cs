using System;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Constants;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private void ApplyFlexibleArrival(Appointment appointment, DateTime arriveTime)
        {
            if (arriveTime > appointment.EndTime.AddMinutes(NoShowMinutes))
            {
                appointment.Status = AppointmentStatuses.Pending;
                appointment.CustomerNotes = AppendNoteMarker(appointment.CustomerNotes, "WINDOW_MISSED_REPRIORITIZED");
                return;
            }

            appointment.Status = AppointmentStatuses.Arrived;
        }

        private async Task ApplyEarlyFixedArrivalAsync(Appointment appointment, DateTime arriveTime)
        {
            var candidates = (await GetAvailableSlotsAsync(
                    appointment.BranchId,
                    DateOnly.FromDateTime(appointment.StartTime),
                    appointment.RequestedServiceIds,
                    AppointmentBookingFlows.Fixed))
                .Where(s => s.StartTime >= arriveTime && s.StartTime < appointment.StartTime)
                .OrderByDescending(s => s.IsTopPick)
                .ThenByDescending(s => s.CompactionScore)
                .ThenBy(s => s.StartTime)
                .ToList();

            if (candidates.Count == 0)
            {
                appointment.Status = AppointmentStatuses.Arrived;
                appointment.CustomerNotes = AppendNoteMarker(appointment.CustomerNotes, "EARLY_WAIT_BOOKED_SLOT");
                return;
            }

            var preferred = candidates[0];
            appointment.StartTime = preferred.StartTime;
            appointment.EndTime = preferred.EndTime;
            appointment.Status = AppointmentStatuses.Arrived;
            appointment.CustomerNotes = AppendNoteMarker(appointment.CustomerNotes, "EARLY_BROUGHT_FORWARD");
        }

        private async Task ApplyLateFixedArrivalAsync(Appointment appointment, DateTime arriveTime)
        {
            var lateMinutes = (arriveTime - appointment.StartTime).TotalMinutes;
            if (lateMinutes > NoShowMinutes)
            {
                ApplyLateSwitchToFlexibleQueue(appointment);
                return;
            }

            var candidates = (await GetAvailableSlotsAsync(
                    appointment.BranchId,
                    DateOnly.FromDateTime(appointment.StartTime),
                    appointment.RequestedServiceIds,
                    AppointmentBookingFlows.Fixed))
                .Where(s => s.StartTime >= arriveTime.AddMinutes(15))
                .OrderByDescending(s => s.IsTopPick)
                .ThenByDescending(s => s.CompactionScore)
                .ThenBy(s => s.StartTime)
                .ToList();

            if (candidates.Count == 0)
            {
                ApplyLateSwitchToFlexibleQueue(appointment);
                return;
            }

            var next = candidates[0];
            appointment.StartTime = next.StartTime;
            appointment.EndTime = next.EndTime;
            appointment.Status = AppointmentStatuses.ArrivedLate;
            appointment.CustomerNotes = AppendNoteMarker(appointment.CustomerNotes, "LATE_RESCHEDULED");
        }

        private void ApplyLateSwitchToFlexibleQueue(Appointment appointment)
        {
            appointment.Status = AppointmentStatuses.Pending;
            appointment.CustomerNotes = AppendNoteMarker(appointment.CustomerNotes, "LATE_SWITCHED_TO_FLEX_QUEUE");
        }
    }
}
