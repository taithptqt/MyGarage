using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Appointments;
using MyGarage.Constants;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using static MyGarage.Services.AppointmentSchedulingHelpers;

namespace MyGarage.Services
{
    public class AppointmentNoShowService : IAppointmentNoShowService
    {
        private const int DefaultPreReminderMinutes = 30;
        private const int DefaultLateReminderMinutes = 10;
        private const int DefaultGoldenWarningMinutes = 15;
        private const int DefaultQuickCancelMinutes = 15;
        private const int DefaultFlexibleCancelMinutes = 30;
        private const int MinLifecycleMinutes = 1;
        private const int MaxLifecycleMinutes = 240;

        private readonly IAppointmentStore _store;
        private readonly IGroupSessionService _groupSessionService;
        private readonly ISystemClock _clock;

        public AppointmentNoShowService(
            IAppointmentStore store,
            IGroupSessionService groupSessionService,
            ISystemClock clock)
        {
            _store = store;
            _groupSessionService = groupSessionService;
            _clock = clock;
        }

        private sealed record AppointmentLifecyclePolicy(
            int PreReminderMinutes,
            int LateReminderMinutes,
            int GoldenWarningMinutes,
            int QuickCancelMinutes,
            int FlexibleCancelMinutes);

        private enum AppointmentLifecycleLane
        {
            Quick,
            Flexible
        }

        public async Task<int> AutoMarkNoShowsAsync(long branchId, DateTime? now = null)
        {
            var at = now ?? _clock.LocalNow;
            var policy = ResolveLifecyclePolicy(branchId, at);

            var candidates = await _store.GetAutoNoShowCandidatesAsync(branchId, at.AddMinutes(policy.PreReminderMinutes));

            if (candidates.Count == 0)
            {
                return 0;
            }

            var customerIds = candidates.Select(x => x.CustomerId).Distinct().ToList();
            var linkActionSet = await _store.GetAppointmentNotificationLinkActionsAsync(customerIds);

            var preferredBayCodes = await ResolvePreferredIdleBayCodesAsync(branchId, at);
            var queuedFlexibleVehiclePlate = await ResolveFlexibleQueueVehiclePlateAsync(branchId);

            var groupIds = new HashSet<long>();
            var autoNoShowAppointments = new List<Appointment>();
            var customerNoShowDeltas = new Dictionary<long, int>();
            var hasStatusChanges = false;
            var hasAuxiliaryChanges = false;

            foreach (var appointment in candidates)
            {
                var lane = ResolveLifecycleLane(appointment);
                var cancelAfterMinutes = ResolveCancelAfterMinutes(policy, lane, appointment.StartTime);
                var warningAfterMinutes = Math.Min(policy.GoldenWarningMinutes, cancelAfterMinutes);
                var lateReminderAfterMinutes = Math.Min(policy.LateReminderMinutes, warningAfterMinutes);

                if (ShouldSendPreReminder(appointment, at, policy.PreReminderMinutes))
                {
                    hasAuxiliaryChanges |= TryQueueLifecycleNotification(
                        appointment,
                        linkActionSet,
                        BuildPreReminderLinkAction(appointment.Id),
                        "Nh\u1EAFc l\u1ECBch h\u1EB9n s\u1EAFp \u0111\u1EBFn",
                        $"L\u1ECBch h\u1EB9n c\u1EE7a b\u1EA1n s\u1EBD b\u1EAFt \u0111\u1EA7u l\u00FAc {appointment.StartTime:HH:mm dd/MM}. Vui l\u00F2ng \u0111\u1EBFn s\u1EDBm \u0111\u1EC3 \u0111\u1EA3m b\u1EA3o ti\u1EBFn \u0111\u1ED9.");
                }

                if (ShouldSendLateReminder(appointment, at, lateReminderAfterMinutes))
                {
                    hasAuxiliaryChanges |= TryQueueLifecycleNotification(
                        appointment,
                        linkActionSet,
                        BuildLateReminderLinkAction(appointment.Id),
                        "L\u1ECBch h\u1EB9n \u0111ang \u0111\u01B0\u1EE3c gi\u1EEF ch\u1ED7",
                        "Ch\u00FAng t\u00F4i v\u1EABn \u0111ang gi\u1EEF ch\u1ED7 cho b\u1EA1n, vui l\u00F2ng \u0111\u1EBFn s\u1EDBm \u0111\u1EC3 \u0111\u1EA3m b\u1EA3o ti\u1EBFn \u0111\u1ED9.");
                }

                if (ShouldAutoCancel(appointment, at, cancelAfterMinutes))
                {
                    appointment.Status = nameof(AppointmentStatus.NO_SHOW);
                    appointment.CancelReason = ResolveAutoCancelReason(lane);
                    appointment.UpdatedAt = _clock.UtcNow;

                    hasStatusChanges = true;
                    autoNoShowAppointments.Add(appointment);
                    AddNoShowDelta(customerNoShowDeltas, appointment.CustomerId);

                    if (appointment.GroupSessionId.HasValue)
                    {
                        groupIds.Add(appointment.GroupSessionId.Value);
                    }

                    hasAuxiliaryChanges |= TryQueueLifecycleNotification(
                        appointment,
                        linkActionSet,
                        BuildAutoCancelLinkAction(appointment.Id),
                        "Th\u00F4ng b\u00E1o qu\u00E1 gi\u1EDD h\u1EB9n",
                        $"R\u1EA5t ti\u1EBFc, do b\u1EA1n \u0111\u1EBFn mu\u1ED9n qu\u00E1 {cancelAfterMinutes} ph\u00FAt n\u00EAn h\u1EC7 th\u1ED1ng \u0111\u00E3 ghi nh\u1EADn l\u1ECBch h\u1EB9n l\u00E0 KH\u00D4NG \u0110\u1EBEN (NO_SHOW) \u0111\u1EC3 nh\u01B0\u1EDDng ch\u1ED7 cho kh\u00E1ch kh\u00E1c. Vui l\u00F2ng \u0111\u1EB7t l\u1EA1i l\u1ECBch m\u1EDBi ho\u1EB7c li\u00EAn h\u1EC7 hotline.");

                    hasAuxiliaryChanges = true;
                    QueueBackfillSuggestion(
                        appointment,
                        at,
                        preferredBayCodes.TryGetValue(lane, out var bayCode) ? bayCode : null,
                        queuedFlexibleVehiclePlate);
                    continue;
                }

                if (ShouldSwitchToLate(appointment, at, warningAfterMinutes))
                {
                    appointment.Status = nameof(AppointmentStatus.LATE);
                    appointment.CancelReason = null;
                    appointment.CustomerNotes = AppendNoteMarker(appointment.CustomerNotes, "LATE_WAITING_CONFIRMATION");
                    appointment.UpdatedAt = _clock.UtcNow;

                    hasStatusChanges = true;

                    if (appointment.GroupSessionId.HasValue)
                    {
                        groupIds.Add(appointment.GroupSessionId.Value);
                    }
                }
            }

            if (hasStatusChanges || hasAuxiliaryChanges)
            {
                await IncrementNoShowCountsAsync(customerNoShowDeltas);
                await _store.SaveChangesInTransactionAsync();
                foreach (var groupId in groupIds)
                {
                    await _groupSessionService.SyncCountersAsync(groupId);
                }

                foreach (var autoNoShow in autoNoShowAppointments)
                {
                    await ReleaseBookingReservationArtifactsAsync(
                        autoNoShow.Id,
                        autoNoShow.BranchId,
                        actorStaffId: null,
                        releasedAt: at);
                }
            }

            return autoNoShowAppointments.Count;
        }

        public int ResolveCancelAfterMinutes(Appointment appointment, DateTime referenceTime)
        {
            var policy = ResolveLifecyclePolicy(appointment.BranchId, referenceTime);
            var lane = ResolveLifecycleLane(appointment);
            return ResolveCancelAfterMinutes(policy, lane, appointment.StartTime);
        }

        public async Task IncrementNoShowCountAsync(long customerId, int delta = 1)
        {
            if (customerId <= 0 || delta <= 0)
            {
                return;
            }

            var customer = await _store.GetCustomerByIdAsync(customerId);
            if (customer == null)
            {
                return;
            }

            customer.NoShowCount = (customer.NoShowCount ?? 0) + delta;
        }

        public async Task ReleaseBookingReservationArtifactsAsync(
            long appointmentId,
            long branchId,
            long? actorStaffId,
            DateTime releasedAt)
        {
            var workSlots = await _store.GetActiveWorkSlotsByAppointmentAsync(appointmentId);

            foreach (var slot in workSlots)
            {
                slot.Status = nameof(WorkSlotStatus.CANCELED);
                slot.ActualReleaseTime ??= releasedAt;
                if (slot.EndTime < releasedAt)
                {
                    slot.EndTime = releasedAt;
                }
            }

            var slotIds = workSlots.Select(s => s.Id).Distinct().ToList();
            if (slotIds.Count == 0)
            {
                if (workSlots.Count > 0)
                {
                    await _store.SaveChangesInTransactionAsync();
                }

                return;
            }

            var reservations = await _store.GetActivePartReservationsForSlotsAsync(slotIds);

            foreach (var group in reservations.GroupBy(r => r.ProductId))
            {
                var releaseQty = group.Sum(x => x.ReservedQuantity);
                await ReleaseInventoryQuantityAsync(branchId, group.Key, releaseQty);
            }

            var now = _clock.UtcNow;
            foreach (var reservation in reservations)
            {
                reservation.Status = "CANCELED";
                reservation.UpdateAt = now;
                reservation.UpdateBy = actorStaffId;
            }

            await _store.SaveChangesInTransactionAsync();
        }

        public async Task ReleaseInventoryQuantityAsync(long branchId, long productId, decimal quantityToRelease)
        {
            var remaining = quantityToRelease;
            var inventoryRows = await _store.GetBranchInventoriesForProductAsync(branchId, productId, onlyWithReserved: true);

            foreach (var row in inventoryRows)
            {
                if (remaining <= InventoryQuantityTolerance)
                {
                    break;
                }

                var reserved = row.ReservedQuantity ?? 0m;
                if (reserved <= InventoryQuantityTolerance)
                {
                    continue;
                }

                var delta = Math.Min(reserved, remaining);
                row.ReservedQuantity = reserved - delta;
                remaining -= delta;
            }
        }

        private AppointmentLifecyclePolicy ResolveLifecyclePolicy(long branchId, DateTime referenceTime)
        {
            var isWeekend = referenceTime.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var config = _store.GetLifecycleConfig(branchId, isWeekend);

            var minCancelMinutes = Math.Max(MinLifecycleMinutes, Math.Min(config.QuickCancelMinutes, config.FlexibleCancelMinutes));
            var goldenWarningMinutes = Math.Clamp(config.GoldenWarningMinutes, MinLifecycleMinutes, minCancelMinutes);
            var lateReminderMinutes = Math.Clamp(config.LateReminderMinutes, MinLifecycleMinutes, goldenWarningMinutes);

            return new AppointmentLifecyclePolicy(
                PreReminderMinutes: config.PreReminderMinutes,
                LateReminderMinutes: lateReminderMinutes,
                GoldenWarningMinutes: goldenWarningMinutes,
                QuickCancelMinutes: config.QuickCancelMinutes,
                FlexibleCancelMinutes: config.FlexibleCancelMinutes);
        }

        private static AppointmentLifecycleLane ResolveLifecycleLane(Appointment appointment)
        {
            var notes = appointment.CustomerNotes ?? string.Empty;

            if (notes.Contains(AppointmentNoteMarkers.ClusterQuick, StringComparison.OrdinalIgnoreCase))
            {
                return AppointmentLifecycleLane.Quick;
            }

            if (notes.Contains(AppointmentNoteMarkers.ClusterHeavy, StringComparison.OrdinalIgnoreCase))
            {
                return AppointmentLifecycleLane.Flexible;
            }

            if (notes.Contains(AppointmentNoteMarkers.FlowFixed, StringComparison.OrdinalIgnoreCase))
            {
                return AppointmentLifecycleLane.Quick;
            }

            if (notes.Contains(AppointmentNoteMarkers.FlowFlexible, StringComparison.OrdinalIgnoreCase))
            {
                return AppointmentLifecycleLane.Flexible;
            }

            return AppointmentLifecycleLane.Flexible;
        }

        private static int ResolveCancelAfterMinutes(
            AppointmentLifecyclePolicy policy,
            AppointmentLifecycleLane lane,
            DateTime _)
        {
            return lane == AppointmentLifecycleLane.Quick
                ? policy.QuickCancelMinutes
                : policy.FlexibleCancelMinutes;
        }

        private static string ResolveAutoCancelReason(AppointmentLifecycleLane _)
        {
            return AppointmentCancelReasons.AutoNoShowTimeout;
        }

        private static bool ShouldSendPreReminder(Appointment appointment, DateTime now, int preReminderMinutes)
        {
            if (!string.Equals(appointment.Status, nameof(AppointmentStatus.PENDING), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(appointment.Status, nameof(AppointmentStatus.CONFIRMED), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var minutesToStart = (appointment.StartTime - now).TotalMinutes;
            return minutesToStart >= 0 && minutesToStart <= preReminderMinutes;
        }

        private static bool ShouldSendLateReminder(Appointment appointment, DateTime now, int lateReminderAfterMinutes)
        {
            if (!string.Equals(appointment.Status, nameof(AppointmentStatus.PENDING), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(appointment.Status, nameof(AppointmentStatus.CONFIRMED), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(appointment.Status, nameof(AppointmentStatus.LATE), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return (now - appointment.StartTime).TotalMinutes >= lateReminderAfterMinutes;
        }

        private static bool ShouldSwitchToLate(Appointment appointment, DateTime now, int warningAfterMinutes)
        {
            if (!string.Equals(appointment.Status, nameof(AppointmentStatus.PENDING), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(appointment.Status, nameof(AppointmentStatus.CONFIRMED), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return (now - appointment.StartTime).TotalMinutes >= warningAfterMinutes;
        }

        private static bool ShouldAutoCancel(Appointment appointment, DateTime now, int cancelAfterMinutes)
        {
            if (!string.Equals(appointment.Status, nameof(AppointmentStatus.PENDING), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(appointment.Status, nameof(AppointmentStatus.CONFIRMED), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(appointment.Status, nameof(AppointmentStatus.LATE), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return (now - appointment.StartTime).TotalMinutes >= cancelAfterMinutes;
        }

        private bool TryQueueLifecycleNotification(
            Appointment appointment,
            HashSet<string> linkActionSet,
            string linkAction,
            string title,
            string content)
        {
            if (linkActionSet.Contains(linkAction))
            {
                return false;
            }

            _store.AddNotification(new Notification
            {
                CustomerId = appointment.CustomerId,
                Title = title,
                Content = content,
                IsRead = false,
                SentAt = _clock.UtcNow,
                LinkAction = linkAction
            });
            linkActionSet.Add(linkAction);
            return true;
        }

        private static string BuildPreReminderLinkAction(long appointmentId) => $"/appointments/{appointmentId}/auto-reminders/t-30";

        private static string BuildLateReminderLinkAction(long appointmentId) => $"/appointments/{appointmentId}/auto-reminders/t+10";

        private static string BuildAutoCancelLinkAction(long appointmentId) => $"/appointments/{appointmentId}/auto-no-show";

        private async Task<Dictionary<AppointmentLifecycleLane, string?>> ResolvePreferredIdleBayCodesAsync(long branchId, DateTime now)
        {
            var bays = await _store.GetActiveBaysAsync(branchId, ordered: true);

            var activeSlotBayIds = await _store.GetOccupiedBayIdsAtAsync(branchId, now);
            var occupiedBayIds = activeSlotBayIds.ToHashSet();

            string? quickBay = SelectIdleBayCode(
                bays,
                occupiedBayIds,
                firstPriority: IsQuickBay,
                secondPriority: IsRepairBay);

            string? flexibleBay = SelectIdleBayCode(
                bays,
                occupiedBayIds,
                firstPriority: IsRepairBay,
                secondPriority: IsQuickBay);

            return new Dictionary<AppointmentLifecycleLane, string?>
            {
                [AppointmentLifecycleLane.Quick] = quickBay,
                [AppointmentLifecycleLane.Flexible] = flexibleBay
            };
        }

        private static string? SelectIdleBayCode(
            IReadOnlyList<Bay> bays,
            IReadOnlySet<long> occupiedBayIds,
            Func<Bay, bool> firstPriority,
            Func<Bay, bool> secondPriority)
        {
            var preferred = bays
                .Where(b => firstPriority(b) && !occupiedBayIds.Contains(b.Id))
                .Select(b => b.Code)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(preferred))
            {
                return preferred;
            }

            return bays
                .Where(b => secondPriority(b) && !occupiedBayIds.Contains(b.Id))
                .Select(b => b.Code)
                .FirstOrDefault();
        }

        private async Task<string?> ResolveFlexibleQueueVehiclePlateAsync(long branchId)
        {
            return await _store.GetFirstFlexibleQueueVehiclePlateAsync(branchId);
        }

        private void QueueBackfillSuggestion(
            Appointment noShowAppointment,
            DateTime now,
            string? bayCode,
            string? flexibleQueueVehiclePlate)
        {
            var bayLabel = string.IsNullOrWhiteSpace(bayCode)
                ? "M\u1ED9t khoang ph\u00F9 h\u1EE3p"
                : $"Khoang {bayCode}";
            var vehicleLabel = string.IsNullOrWhiteSpace(flexibleQueueVehiclePlate)
                ? "xe t\u1EEB lu\u1ED3ng linh ho\u1EA1t"
                : $"xe {flexibleQueueVehiclePlate}";

            _store.AddAiSmartDispatching(new AiSmartDispatching
            {
                BranchId = noShowAppointment.BranchId,
                Date = DateOnly.FromDateTime(now),
                Recommendation = $"[AUTO_NO_SHOW_APPOINTMENT:{noShowAppointment.Id}] {bayLabel} \u0111\u00E3 tr\u1ED1ng do kh\u00E1ch kh\u00F4ng \u0111\u1EBFn. C\u00F3 th\u1EC3 \u0111\u01B0a {vehicleLabel} v\u00E0o l\u00E0m ngay.",
                EfficiencyScore = null,
                Status = nameof(AiDispatchStatus.GENERATED),
                CreatedAt = _clock.UtcNow
            });
        }

        private async Task IncrementNoShowCountsAsync(IReadOnlyDictionary<long, int> customerDeltas)
        {
            if (customerDeltas.Count == 0)
            {
                return;
            }

            var customerIds = customerDeltas.Keys.Where(x => x > 0).Distinct().ToList();
            if (customerIds.Count == 0)
            {
                return;
            }

            var customers = await _store.GetCustomersByIdsAsync(customerIds);

            foreach (var customer in customers)
            {
                if (!customerDeltas.TryGetValue(customer.Id, out var delta) || delta <= 0)
                {
                    continue;
                }

                customer.NoShowCount = (customer.NoShowCount ?? 0) + delta;
            }
        }

        private static void AddNoShowDelta(Dictionary<long, int> customerDeltas, long customerId)
        {
            if (customerId <= 0)
            {
                return;
            }

            if (customerDeltas.TryGetValue(customerId, out var current))
            {
                customerDeltas[customerId] = current + 1;
            }
            else
            {
                customerDeltas[customerId] = 1;
            }
        }
    }
}
