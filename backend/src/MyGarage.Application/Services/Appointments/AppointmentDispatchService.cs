using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Appointments;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using static MyGarage.Services.AppointmentSchedulingHelpers;

namespace MyGarage.Services
{
    public class AppointmentDispatchService : IAppointmentDispatchService
    {
        private const int DispatchDelayThresholdMinutes = 15;

        private static readonly string[] DispatchQueueStatuses =
        {
            nameof(AppointmentStatus.PENDING),
            nameof(AppointmentStatus.ARRIVED),
            nameof(AppointmentStatus.ARRIVED_LATE)
        };

        private readonly IAppointmentStore _store;
        private readonly IAppointmentAvailabilityQuery _availabilityQuery;
        private readonly IAppointmentNoShowService _noShowService;
        private readonly ISystemClock _clock;

        public AppointmentDispatchService(
            IAppointmentStore store,
            IAppointmentAvailabilityQuery availabilityQuery,
            IAppointmentNoShowService noShowService,
            ISystemClock clock)
        {
            _store = store;
            _availabilityQuery = availabilityQuery;
            _noShowService = noShowService;
            _clock = clock;
        }

        public async Task<DispatchDashboardResponseDto> GetDispatchDashboardAsync(long branchId, DateTime? at = null)
        {
            var snapshot = at ?? _clock.LocalNow;
            var bays = await _store.GetActiveBaysAsync(branchId, ordered: true);
            var slots = await _store.GetActiveDispatchWorkSlotsAsync(branchId, snapshot);

            var bayDtos = new List<DispatchBayStatusDto>();
            var busy = 0;
            var overflow = 0;
            foreach (var bay in bays)
            {
                var slot = slots.FirstOrDefault(s => s.BayId == bay.Id);
                if (slot != null) busy++;
                var isOverflow = slot != null && IsRepairBay(bay) && IsQuickSlotType(slot.SlotType, slot.Notes);
                if (isOverflow) overflow++;

                bayDtos.Add(new DispatchBayStatusDto
                {
                    BayId = bay.Id,
                    SequenceOrder = bay.SequenceOrder,
                    BayCode = bay.Code,
                    BayName = bay.Name,
                    BayType = bay.BayType,
                    ServiceArea = bay.ServiceArea ?? string.Empty,
                    Status = slot == null ? "IDLE" : "BUSY",
                    IsOverflowFromQuick = isOverflow,
                    SlotType = slot?.SlotType,
                    CurrentCustomerId = slot?.CustomerId,
                    CurrentVehicleId = slot?.VehicleId,
                    PlannedReleaseAt = slot?.EndTime,
                    ActualReleaseAt = slot?.ActualReleaseTime
                });
            }

            var queue = await _store.CountActiveBranchAppointmentsAsync(branchId, DispatchQueueStatuses);
            return new DispatchDashboardResponseDto
            {
                BranchId = branchId,
                SnapshotAt = snapshot,
                TotalBays = bays.Count,
                BusyBays = busy,
                IdleBays = bays.Count - busy,
                OverflowBays = overflow,
                WaitingQueueCount = queue,
                Bays = bayDtos
            };
        }

        public async Task<DispatchCycleResponseDto> RunDispatchCycleAsync(long branchId, DateTime? at = null)
        {
            var executedAt = at ?? _clock.LocalNow;
            var noShows = await _noShowService.AutoMarkNoShowsAsync(branchId, executedAt);
            var health = await _availabilityQuery.GetBranchBookingHealthAsync(branchId, executedAt);
            var delayNotifications = await SendProactiveDelayNotificationsAsync(branchId, executedAt);

            if (health.IsBookingLocked)
            {
                delayNotifications += await SendDelayNotificationsAsync(branchId, executedAt, health);
            }

            return new DispatchCycleResponseDto
            {
                BranchId = branchId,
                ExecutedAt = executedAt,
                AutoNoShowCount = noShows,
                DelayNotificationCount = delayNotifications,
                BookingHealth = health
            };
        }

        public async Task<OvernightLockResponseDto> LockOvernightSlotsAsync(long branchId, DateOnly? businessDate = null)
        {
            var date = businessDate ?? _clock.LocalToday;
            var closingTime = date.ToDateTime(new TimeOnly(18, 0));
            var nextMorning = date.AddDays(1).ToDateTime(new TimeOnly(8, 0));
            var opHour = await _store.GetBranchOperatingHourAsync(branchId, (int)date.DayOfWeek);
            var slotStepMinutes = ResolveSlotStepMinutes(opHour?.SlotDurationMinutes);

            var unfinished = await _store.GetUnfinishedOvernightSourcesAsync(branchId, closingTime);

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
                _store.AddWorkSlot(lockSlot);
                await _store.SaveChangesInTransactionAsync();

                result.LockedSlots.Add(new OvernightLockedSlotDto
                {
                    SourceWorkSlotId = source.Id,
                    LockedWorkSlotId = lockSlot.Id,
                    BayId = source.BayId!.Value,
                    LockedFrom = lockSlot.StartTime,
                    LockedTo = lockSlot.EndTime,
                    VehicleId = source.VehicleId
                });
            }

            result.LockedSlotCount = result.LockedSlots.Count;
            return result;
        }

        public async Task<int> ReleaseExpiredLockedSlotsAsync(DateTime? now = null)
        {
            var releasedAt = now ?? _clock.LocalNow;
            var expiredSlots = await _store.GetExpiredLockedWorkSlotsAsync(releasedAt);

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
            var reservations = await _store.GetActivePartReservationsForSlotsAsync(slotIds);

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
                await _noShowService.ReleaseInventoryQuantityAsync(release.BranchId, release.ProductId, release.Quantity);
            }

            var utcNow = _clock.UtcNow;
            foreach (var reservation in reservations)
            {
                reservation.Status = "CANCELED";
                reservation.UpdateAt = utcNow;
                reservation.UpdateBy = null;
            }

            await _store.SaveChangesInTransactionAsync();
            return expiredSlots.Count;
        }

        private async Task<int> SendDelayNotificationsAsync(long branchId, DateTime now, BranchBookingHealthResponseDto health)
        {
            if (!health.IsBookingLocked || !health.LockUntil.HasValue)
            {
                return 0;
            }

            var affected = (await _store.GetOpenAppointmentsOverlappingAsync(branchId, now.AddMinutes(90), health.LockUntil.Value))
                .Where(a => (a.Status == nameof(AppointmentStatus.CONFIRMED) || a.Status == nameof(AppointmentStatus.PENDING))
                    && a.StartTime >= now.AddMinutes(90)
                    && a.StartTime <= health.LockUntil.Value)
                .ToList();

            var suggestion = health.SuggestedBranches.FirstOrDefault();
            foreach (var appointment in affected)
            {
                _store.AddNotification(BuildDelayNotification(appointment, suggestion));
            }

            if (affected.Count > 0)
            {
                await _store.SaveChangesInTransactionAsync();
            }

            return affected.Count;
        }

        private async Task<int> SendProactiveDelayNotificationsAsync(long branchId, DateTime now)
        {
            var slots = await _store.GetActiveProactiveDelayWorkSlotsAsync(branchId, now, now.AddHours(6));

            if (slots.Count == 0)
            {
                return 0;
            }

            var bays = await _store.GetActiveBaysAsync(branchId);

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
                var alreadySent = await _store.HasNotificationSentSinceAsync(next.CustomerId.Value, linkAction, now.Date);

                if (alreadySent)
                {
                    continue;
                }

                _store.AddNotification(new Notification
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
                await _store.SaveChangesInTransactionAsync();
            }

            return sentCount;
        }

        private static bool HasAlternativeBayForSlot(
            WorkSlot slot,
            IReadOnlyList<Bay> bays,
            IReadOnlyList<WorkSlot> slots)
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

        private async Task<bool> HasExistingOvernightLockAsync(long branchId, DateTime nextMorning, WorkSlot source)
        {
            if (!source.BayId.HasValue)
            {
                return false;
            }

            var marker = BuildOvernightSourceMarker(source.Id);
            return await _store.HasOvernightLockAsync(
                branchId,
                source.BayId.Value,
                nextMorning,
                nextMorning.AddDays(1),
                marker);
        }

        private static int ResolveOvernightLockMinutes(WorkSlot source, DateTime closingTime, int slotStepMinutes)
        {
            var baseRemaining = source.ActualReleaseTime.HasValue && source.ActualReleaseTime.Value > closingTime
                ? (int)Math.Ceiling((source.ActualReleaseTime.Value - closingTime).TotalMinutes)
                : 90;
            return RoundUpToStep(Math.Clamp(baseRemaining, 60, 240), slotStepMinutes);
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
