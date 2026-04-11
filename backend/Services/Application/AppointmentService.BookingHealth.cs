using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Constants;
using MyGarage.DTOs.Responses;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        public async Task<BranchBookingHealthResponseDto> GetBranchBookingHealthAsync(long branchId, DateTime? at = null)
        {
            var atTime = at ?? _clock.LocalNow;
            var bays = await _context.Bays.AsNoTracking().Where(b => b.BranchId == branchId && b.IsActive == true && b.IsDeleted != true).ToListAsync();
            var capacity = ComputeCapacity(bays);
            var delayThreshold = Math.Max(120, capacity.Total * 15);
            var parkingThreshold = Math.Max(12, (int)Math.Ceiling(capacity.Total * 1.5));

            var delayedSlots = await _context.WorkSlots.AsNoTracking()
                .Where(s => s.BranchId == branchId
                    && s.EndTime < atTime
                    && s.SlotType != nameof(WorkSlotType.BUFFER)
                    && s.Status != nameof(WorkSlotStatus.COMPLETED)
                    && s.Status != nameof(WorkSlotStatus.CANCELLED)
                    && s.Status != nameof(WorkSlotStatus.NOSHOW))
                .ToListAsync();

            var totalDelay = delayedSlots.Sum(s => (int)Math.Max(0, Math.Ceiling(((s.ActualReleaseTime ?? atTime) - s.EndTime).TotalMinutes)));
            var queueCount = await _context.Appointments.AsNoTracking()
                .Where(a => a.BranchId == branchId &&
                    (a.Status == nameof(AppointmentStatus.PENDING) ||
                     a.Status == nameof(AppointmentStatus.CONFIRMED) ||
                     a.Status == nameof(AppointmentStatus.LATE) ||
                     a.Status == nameof(AppointmentStatus.ARRIVED) ||
                     a.Status == nameof(AppointmentStatus.ARRIVED_LATE)))
                .CountAsync();

            var locked = totalDelay >= delayThreshold || (totalDelay >= delayThreshold / 2 && queueCount >= parkingThreshold);
            return new BranchBookingHealthResponseDto
            {
                BranchId = branchId,
                EvaluatedAt = atTime,
                IsBookingLocked = locked,
                LockUntil = locked ? atTime.AddHours(BookingLockHours) : null,
                TotalDelayMinutes = totalDelay,
                DelayThresholdMinutes = delayThreshold,
                ParkingQueueCount = queueCount,
                ParkingThreshold = parkingThreshold,
                Message = locked ? "Chi nhanh dang qua tai, tam khoa nhan lich 4 gio." : "Chi nhanh dang trong nguong an toan.",
                SuggestedBranches = locked ? await GetAlternativeBranchesAsync(branchId, atTime) : new List<BranchSuggestionDto>()
            };
        }
    }
}
