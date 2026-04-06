using System;
using System.Linq;
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
        public async Task<OvernightLockResponseDto> LockOvernightSlotsAsync(long branchId, DateOnly? businessDate = null)
        {
            var date = businessDate ?? _clock.LocalToday;
            var closingTime = date.ToDateTime(new TimeOnly(18, 0));
            var nextMorning = date.AddDays(1).ToDateTime(new TimeOnly(8, 0));
            var opHour = await _context.BranchOperatingHours
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.BranchId == branchId && h.DayOfWeek == (int)date.DayOfWeek);
            var slotStepMinutes = ResolveSlotStepMinutes(opHour?.SlotDurationMinutes);

            var unfinished = await LoadUnfinishedOvernightSourcesAsync(branchId, closingTime);

            var result = new OvernightLockResponseDto
            {
                BranchId = branchId,
                BusinessDate = date
            };

            foreach (var source in unfinished)
            {
                if (await HasExistingOvernightLockAsync(branchId, nextMorning, source))
                {
                    continue;
                }

                var lockMinutes = ResolveOvernightLockMinutes(source, closingTime, slotStepMinutes);
                var lockSlot = BuildOvernightLockSlot(source, nextMorning, lockMinutes);
                _context.WorkSlots.Add(lockSlot);
                await _context.SaveChangesAsync();

                result.LockedSlots.Add(new OvernightLockedSlotDto
                {
                    SourceWorkSlotId = source.Id,
                    LockedWorkSlotId = lockSlot.Id,
                    BayId = source.BayId,
                    LockedFrom = lockSlot.StartTime,
                    LockedTo = lockSlot.EndTime,
                    VehicleId = source.VehicleId
                });
            }

            result.LockedSlotCount = result.LockedSlots.Count;
            return result;
        }

        private async Task<int> SendDelayNotificationsAsync(long branchId, DateTime now, BranchBookingHealthResponseDto health)
        {
            if (!health.IsBookingLocked || !health.LockUntil.HasValue)
            {
                return 0;
            }

            var affected = await _context.Appointments
                .Where(a => a.BranchId == branchId
                    && (a.Status == nameof(AppointmentStatus.CONFIRMED) || a.Status == nameof(AppointmentStatus.PENDING))
                    && a.StartTime >= now.AddMinutes(90)
                    && a.StartTime <= health.LockUntil.Value)
                .ToListAsync();

            var suggestion = health.SuggestedBranches.FirstOrDefault();
            foreach (var appointment in affected)
            {
                _context.Notifications.Add(BuildDelayNotification(appointment, suggestion));
            }

            if (affected.Count > 0)
            {
                await _context.SaveChangesAsync();
            }

            return affected.Count;
        }

    }
}
