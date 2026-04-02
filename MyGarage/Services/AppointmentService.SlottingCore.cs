using System;
using System.Collections.Generic;
using System.Linq;
using MyGarage.Constants;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private static (int lockedQuick, int lockedRepair) CountLockedBays(IEnumerable<Bay> activeBays, IEnumerable<WorkSlot> locks, DateTime start, DateTime end)
        {
            var bayById = activeBays.ToDictionary(b => b.Id, b => b);
            var overlapping = locks.Where(l => l.StartTime < end && l.EndTime > start).ToList();
            var quick = 0;
            var repair = 0;

            foreach (var lockSlot in overlapping)
            {
                if (!bayById.TryGetValue(lockSlot.BayId, out var bay))
                {
                    continue;
                }

                if (IsQuickBay(bay.BayType))
                {
                    quick++;
                }
                else
                {
                    repair++;
                }
            }

            return (quick, repair);
        }

        private static BookingFlowType ResolveFlow(string? bookingFlow, string? requestedServiceIds)
        {
            if (!string.IsNullOrWhiteSpace(bookingFlow))
            {
                var key = bookingFlow.Trim().ToLowerInvariant();
                if (key.Contains("flex") || key.Contains("window") || key.Contains("check")) return BookingFlowType.Flexible;
                if (key.Contains("fixed") || key.Contains("slot")) return BookingFlowType.Fixed;
            }

            return string.IsNullOrWhiteSpace(requestedServiceIds) ? BookingFlowType.Flexible : BookingFlowType.Fixed;
        }

        private static BookingFlowType ResolveFlowByAppointment(Appointment appointment)
        {
            if (!string.IsNullOrWhiteSpace(appointment.CustomerNotes) && appointment.CustomerNotes.Contains(AppointmentNoteMarkers.FlowFlexible, StringComparison.OrdinalIgnoreCase))
                return BookingFlowType.Flexible;
            return ResolveFlow(null, appointment.RequestedServiceIds);
        }

        private void ApplyUpdateAudit(Appointment appointment, long? actorStaffId)
        {
            appointment.UpdatedAt = _clock.UtcNow;
            appointment.UpdatedBy = actorStaffId;
        }

        private static string ResolveSession(TimeSpan time)
        {
            if (time < new TimeSpan(12, 0, 0)) return "MORNING";
            if (time < new TimeSpan(17, 0, 0)) return "AFTERNOON";
            return "EVENING";
        }

        private static void MarkTopPicks(List<AvailableSlotResponseDto> slots)
        {
            foreach (var sessionGroup in slots.GroupBy(s => s.Session))
            {
                var rounded = sessionGroup.Where(s => s.IsRoundedHour).OrderByDescending(s => s.CompactionScore).ThenBy(s => s.StartTime).Take(5).ToList();
                var picks = new List<AvailableSlotResponseDto>(rounded);
                if (picks.Count < 3)
                {
                    picks.AddRange(sessionGroup.Where(s => !picks.Contains(s)).OrderByDescending(s => s.CompactionScore).ThenBy(s => s.StartTime).Take(3 - picks.Count));
                }

                foreach (var slot in picks) slot.IsTopPick = true;
            }
        }

        private static int CalculateCompactionScore(DateTime slotStart, DateTime openAt, List<Appointment> overlap)
        {
            var score = slotStart.Minute == 0 ? 30 : 0;
            if (slotStart == openAt) score += 20;
            var nearestEnd = overlap.Select(o => Math.Abs((o.EndTime - slotStart).TotalMinutes)).DefaultIfEmpty(999).Min();
            if (nearestEnd <= 15) score += 40;
            else if (nearestEnd <= 30) score += 20;
            return score;
        }
    }
}
