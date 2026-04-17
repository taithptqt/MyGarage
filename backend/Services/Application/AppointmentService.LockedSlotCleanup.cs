using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Enums;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        public async Task<int> ReleaseExpiredLockedSlotsAsync(DateTime? now = null)
        {
            var releasedAt = now ?? _clock.LocalNow;
            var expiredSlots = await _context.WorkSlots
                .Where(s =>
                    s.Status == nameof(WorkSlotStatus.LOCKED) &&
                    s.LockedUntil.HasValue &&
                    s.LockedUntil.Value <= releasedAt)
                .ToListAsync();

            if (expiredSlots.Count == 0)
            {
                return 0;
            }

            var expiredById = expiredSlots.ToDictionary(s => s.Id, s => s);
            foreach (var slot in expiredSlots)
            {
                slot.Status = nameof(WorkSlotStatus.CANCELED);
                slot.ActualReleaseTime ??= releasedAt;
                if (slot.EndTime < releasedAt)
                {
                    slot.EndTime = releasedAt;
                }
            }

            var slotIds = expiredSlots.Select(s => s.Id).Distinct().ToList();
            var reservations = await _context.PartInventoryReservations
                .Where(r =>
                    slotIds.Contains(r.WorkSlotId) &&
                    r.Status == "RESERVED")
                .ToListAsync();

            var groupedReleases = reservations
                .Where(r => expiredById.ContainsKey(r.WorkSlotId))
                .GroupBy(r => new
                {
                    expiredById[r.WorkSlotId].BranchId,
                    r.ProductId
                })
                .Select(g => new
                {
                    g.Key.BranchId,
                    g.Key.ProductId,
                    Quantity = g.Sum(x => x.ReservedQuantity)
                })
                .ToList();

            foreach (var release in groupedReleases)
            {
                await ReleaseInventoryQuantityAsync(release.BranchId, release.ProductId, release.Quantity);
            }

            var utcNow = _clock.UtcNow;
            foreach (var reservation in reservations)
            {
                reservation.Status = "CANCELED";
                reservation.UpdateAt = utcNow;
                reservation.UpdateBy = null;
            }

            await _context.SaveChangesAsync();
            return expiredSlots.Count;
        }
    }
}

