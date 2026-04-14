using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Enums;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private const int DispatchDelayThresholdMinutes = 15;

        private async Task<int> SendProactiveDelayNotificationsAsync(long branchId, DateTime now)
        {
            var slots = await _context.WorkSlots
                .AsNoTracking()
                .Where(s => s.BranchId == branchId
                    && s.BayId.HasValue
                    && s.StartTime <= now.AddHours(6)
                    && (s.ActualReleaseTime == null || s.ActualReleaseTime > now)
                    && s.SlotType != nameof(WorkSlotType.BUFFER)
                    && s.Status != nameof(WorkSlotStatus.CANCELLED)
                    && s.Status != nameof(WorkSlotStatus.NOSHOW)
                    && s.Status != nameof(WorkSlotStatus.COMPLETED))
                .OrderBy(s => s.BayId)
                .ThenBy(s => s.StartTime)
                .ToListAsync();

            if (slots.Count == 0)
            {
                return 0;
            }

            var bays = await _context.Bays
                .AsNoTracking()
                .Where(b => b.BranchId == branchId && b.IsActive == true && b.IsDeleted != true)
                .ToListAsync();

            var sentCount = 0;
            foreach (var bayGroup in slots.GroupBy(s => s.BayId!.Value))
            {
                var current = bayGroup
                    .Where(s => s.StartTime <= now && ResolveEffectiveSlotEnd(s) > now)
                    .OrderByDescending(s => s.StartTime)
                    .FirstOrDefault();
                if (current == null)
                {
                    continue;
                }

                var next = bayGroup
                    .Where(s => s.Id != current.Id && s.StartTime >= now)
                    .OrderBy(s => s.StartTime)
                    .FirstOrDefault();
                if (next == null || !next.CustomerId.HasValue)
                {
                    continue;
                }

                var overlapMinutes = (int)Math.Ceiling((ResolveEffectiveSlotEnd(current) - next.StartTime).TotalMinutes);
                if (overlapMinutes <= DispatchDelayThresholdMinutes)
                {
                    continue;
                }

                var hasAlternativeBay = HasAlternativeBayForSlot(next, bays, slots);
                if (hasAlternativeBay)
                {
                    continue;
                }

                var linkAction = $"/work-slots/{next.Id}/delay-warning";
                var alreadySent = await _context.Notifications
                    .AnyAsync(n => n.CustomerId == next.CustomerId.Value
                        && n.LinkAction == linkAction
                        && n.SentAt.HasValue
                        && n.SentAt.Value >= now.Date);

                if (alreadySent)
                {
                    continue;
                }

                _context.Notifications.Add(new Models.Notification
                {
                    CustomerId = next.CustomerId.Value,
                    Title = "L\u1ECBch h\u1EB9n c\u00F3 th\u1EC3 b\u1ECB ch\u1EADm",
                    Content = $"Th\u1EADt xin l\u1ED7i, ca s\u1EEDa ch\u1EEFa tr\u01B0\u1EDBc xe c\u1EE7a b\u1EA1n ph\u00E1t sinh h\u1EA1ng m\u1EE5c ph\u1EE9c t\u1EA1p. L\u1ECBch h\u1EB9n c\u1EE7a b\u1EA1n c\u00F3 th\u1EC3 ch\u1EADm {overlapMinutes} ph\u00FAt. Ch\u00FAng t\u00F4i \u0111ang n\u1ED7 l\u1EF1c s\u1EAFp x\u1EBFp khoang tr\u1ED1ng s\u1EDBm nh\u1EA5t cho b\u1EA1n.",
                    IsRead = false,
                    SentAt = _clock.UtcNow,
                    LinkAction = linkAction
                });

                sentCount++;
            }

            if (sentCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            return sentCount;
        }

        private static bool HasAlternativeBayForSlot(
            Models.WorkSlot slot,
            System.Collections.Generic.IReadOnlyList<Models.Bay> bays,
            System.Collections.Generic.IReadOnlyList<Models.WorkSlot> slots)
        {
            if (!slot.BayId.HasValue)
            {
                return false;
            }

            var slotNeedsQuick = IsQuickSlotType(slot.SlotType, slot.Notes);
            foreach (var bay in bays.Where(b => b.Id != slot.BayId.Value))
            {
                if (!slotNeedsQuick && IsQuickBay(bay))
                {
                    continue;
                }

                var busy = slots.Any(s =>
                    s.Id != slot.Id &&
                    s.BayId == bay.Id &&
                    s.StartTime < slot.EndTime &&
                    ResolveEffectiveSlotEnd(s) > slot.StartTime &&
                    !IsClosedWorkSlotStatus(s.Status));

                if (!busy)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
