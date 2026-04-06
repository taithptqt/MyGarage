using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Constants;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class GroupSessionService
    {
        public async Task<GroupSession> CreateAsync(long customerId, long branchId, string? customerNote, long? actorStaffId)
        {
            var entity = new GroupSession
            {
                GroupCode = GenerateGroupSessionCode(),
                CustomerId = customerId,
                BranchId = branchId,
                Status = GroupSessionStatuses.Pending,
                TotalAppointments = 0,
                ConfirmedAppointments = 0,
                CanceledAppointments = 0,
                CustomerNote = string.IsNullOrWhiteSpace(customerNote) ? null : customerNote.Trim(),
                CreatedAt = _clock.UtcNow,
                UpdatedAt = _clock.UtcNow,
                CreatedBy = actorStaffId,
                UpdatedBy = actorStaffId
            };

            _context.GroupSessions.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task SyncCountersAsync(long? groupSessionId, long? actorStaffId = null)
        {
            if (!groupSessionId.HasValue)
            {
                return;
            }

            var group = await _context.GroupSessions
                .FirstOrDefaultAsync(x => x.Id == groupSessionId.Value);
            if (group == null)
            {
                return;
            }

            var statuses = await _context.Appointments
                .AsNoTracking()
                .Where(x => x.GroupSessionId == groupSessionId.Value)
                .Select(x => x.Status)
                .ToListAsync();

            var outcome = _groupSessionStatusPolicy.Evaluate(statuses, group.Status);
            group.Status = outcome.Status;
            group.TotalAppointments = outcome.TotalAppointments;
            group.ConfirmedAppointments = outcome.ConfirmedAppointments;
            group.CanceledAppointments = outcome.CanceledAppointments;

            var nowUtc = _clock.UtcNow;
            if (outcome.ShouldSetConfirmedAt)
            {
                group.ConfirmedAt = nowUtc;
            }
            if (outcome.ShouldSetCanceledAt)
            {
                group.CanceledAt = nowUtc;
            }
            if (outcome.ShouldClearConfirmedAt)
            {
                group.ConfirmedAt = null;
            }
            if (outcome.ShouldClearCanceledAt)
            {
                group.CanceledAt = null;
            }

            if (actorStaffId.HasValue)
            {
                group.UpdatedBy = actorStaffId.Value;
            }
            group.UpdatedAt = nowUtc;
            await _context.SaveChangesAsync();
        }
    }
}
