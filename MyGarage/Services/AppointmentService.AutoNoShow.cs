using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Constants;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        public async Task<int> AutoMarkNoShowsAsync(long branchId, DateTime? now = null)
        {
            var at = now ?? _clock.LocalNow;
            var candidates = await _context.Appointments
                .Where(a => a.BranchId == branchId
                    && (a.Status == AppointmentStatuses.Confirmed || a.Status == AppointmentStatuses.Pending)
                    && a.StartTime <= at.AddMinutes(-NoShowMinutes))
                .ToListAsync();

            foreach (var a in candidates)
            {
                a.Status = AppointmentStatuses.NoShow;
                a.CancelReason = AppointmentCancelReasons.AutoNoShowTimeout;
                a.UpdatedAt = _clock.UtcNow;
            }

            if (candidates.Count > 0)
            {
                await _context.SaveChangesAsync();
                foreach (var groupId in candidates.Select(x => x.BookingGroupId).Where(x => x.HasValue).Select(x => x!.Value).Distinct())
                {
                    await _bookingGroupService.SyncCountersAsync(groupId);
                }
            }

            return candidates.Count;
        }
    }
}
