using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Constants;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private async Task<List<WorkSlot>> LoadUnfinishedOvernightSourcesAsync(long branchId, DateTime closingTime)
        {
            return await _context.WorkSlots
                .Where(s => s.BranchId == branchId
                    && s.BayId.HasValue
                    && s.StartTime <= closingTime
                    && s.SlotType != nameof(WorkSlotType.BUFFER)
                    && s.Status != nameof(WorkSlotStatus.COMPLETED)
                    && s.Status != nameof(WorkSlotStatus.CANCELLED)
                    && s.Status != nameof(WorkSlotStatus.NOSHOW)
                    && s.Status != nameof(WorkSlotStatus.OVERNIGHT_HOLD)
                    && (s.ActualReleaseTime == null || s.ActualReleaseTime > closingTime))
                .ToListAsync();
        }

        private async Task<bool> HasExistingOvernightLockAsync(long branchId, DateTime nextMorning, WorkSlot source)
        {
            if (!source.BayId.HasValue)
            {
                return false;
            }

            var marker = BuildOvernightSourceMarker(source.Id);
            return await _context.WorkSlots.AnyAsync(s =>
                s.BranchId == branchId
                && s.BayId == source.BayId.Value
                && s.SlotType == nameof(WorkSlotType.REPAIR)
                && s.Status == nameof(WorkSlotStatus.OVERNIGHT_HOLD)
                && s.StartTime >= nextMorning
                && s.StartTime < nextMorning.AddDays(1)
                && s.Notes != null
                && s.Notes.Contains(marker));
        }

        private static string BuildOvernightSourceMarker(long sourceSlotId) => $"[OVERNIGHT_FROM:{sourceSlotId}]";

        private static int ResolveOvernightLockMinutes(WorkSlot source, DateTime closingTime, int slotStepMinutes)
        {
            var baseRemaining = source.ActualReleaseTime.HasValue && source.ActualReleaseTime.Value > closingTime
                ? (int)Math.Ceiling((source.ActualReleaseTime.Value - closingTime).TotalMinutes)
                : 90;
            return RoundUpToStep(Math.Clamp(baseRemaining, 60, 240), slotStepMinutes);
        }

        private static WorkSlot BuildOvernightLockSlot(WorkSlot source, DateTime nextMorning, int lockMinutes)
        {
            return new WorkSlot
            {
                ParentSlotId = source.Id,
                BranchId = source.BranchId,
                BayId = source.BayId,
                TechnicianId = source.TechnicianId,
                StartTime = nextMorning,
                EndTime = nextMorning.AddMinutes(lockMinutes),
                SlotType = nameof(WorkSlotType.REPAIR),
                CustomerId = source.CustomerId,
                VehicleId = source.VehicleId,
                AppointmentId = source.AppointmentId,
                ServiceOrderId = source.ServiceOrderId,
                IsAdditional = source.IsAdditional,
                SequenceOrder = source.SequenceOrder > 0 ? source.SequenceOrder : 3,
                Status = nameof(WorkSlotStatus.OVERNIGHT_HOLD),
                Notes = BuildOvernightSourceMarker(source.Id)
            };
        }

        private Notification BuildDelayNotification(Appointment appointment, BranchSuggestionDto? suggestion)
        {
            var suffix = suggestion == null
                ? "Chi nhanh dang qua tai, vui long den som de duoc ho tro."
                : $"Ban co the tham khao {suggestion.BranchName} (~{suggestion.DistanceKm} km) dang trong cho.";

            return new Notification
            {
                CustomerId = appointment.CustomerId,
                Title = "Lich hen co nguy co tre",
                Content = $"Lich hen {appointment.StartTime:HH:mm dd/MM} co the bi tre do xuong qua tai. {suffix}",
                IsRead = false,
                SentAt = _clock.UtcNow,
                LinkAction = $"/appointments/{appointment.Id}"
            };
        }
    }
}
