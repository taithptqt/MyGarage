using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Constants;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class BookingGroupService
    {
        public async Task<BookingGroup> CreateAsync(long customerId, long branchId, string? customerNote, long? actorStaffId)
        {
            var entity = new BookingGroup
            {
                GroupCode = GenerateBookingGroupCode(),
                CustomerId = customerId,
                BranchId = branchId,
                Status = BookingGroupStatuses.Pending,
                TotalAppointments = 0,
                ConfirmedAppointments = 0,
                CanceledAppointments = 0,
                CustomerNote = string.IsNullOrWhiteSpace(customerNote) ? null : customerNote.Trim(),
                CreatedAt = _clock.UtcNow,
                UpdatedAt = _clock.UtcNow,
                CreatedBy = actorStaffId,
                UpdatedBy = actorStaffId
            };

            _context.BookingGroups.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task SyncCountersAsync(long? bookingGroupId, long? actorStaffId = null)
        {
            if (!bookingGroupId.HasValue)
            {
                return;
            }

            var group = await _context.BookingGroups
                .FirstOrDefaultAsync(x => x.Id == bookingGroupId.Value);
            if (group == null)
            {
                return;
            }

            var statuses = await _context.Appointments
                .AsNoTracking()
                .Where(x => x.BookingGroupId == bookingGroupId.Value)
                .Select(x => x.Status)
                .ToListAsync();

            var outcome = _bookingGroupStatusPolicy.Evaluate(statuses, group.Status);
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
