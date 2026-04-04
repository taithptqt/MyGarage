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
                appointment.Status = nameof(AppointmentStatus.PENDING);
                appointment.CustomerNotes = AppendNoteMarker(appointment.CustomerNotes, "WINDOW_MISSED_REPRIORITIZED");
                return;
            }

            appointment.Status = nameof(AppointmentStatus.ARRIVED);
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
            appointment.Status = nameof(AppointmentStatus.ARRIVED_LATE);
            appointment.CustomerNotes = AppendNoteMarker(appointment.CustomerNotes, "LATE_RESCHEDULED");
        }

        private void ApplyLateSwitchToFlexibleQueue(Appointment appointment)
        {
            appointment.Status = nameof(AppointmentStatus.PENDING);
            appointment.CustomerNotes = AppendNoteMarker(appointment.CustomerNotes, "LATE_SWITCHED_TO_FLEX_QUEUE");
        }
    }
}
