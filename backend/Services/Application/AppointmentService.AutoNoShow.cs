using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MyGarage.Constants;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private const int DefaultPreReminderMinutes = 30;
        private const int DefaultLateReminderMinutes = 10;
        private const int DefaultGoldenWarningMinutes = 15;
        private const int DefaultQuickCancelMinutes = 15;
        private const int DefaultFlexibleCancelMinutes = 30;
        private const int MinLifecycleMinutes = 1;
        private const int MaxLifecycleMinutes = 240;

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

            var candidates = await _context.Appointments
                .Where(a => a.BranchId == branchId
                    && (a.Status == AppointmentStatuses.Pending
                        || a.Status == AppointmentStatuses.Confirmed
                        || a.Status == AppointmentStatuses.Late)
                    && a.StartTime <= at.AddMinutes(policy.PreReminderMinutes))
                .OrderBy(a => a.StartTime)
                .ToListAsync();

            if (candidates.Count == 0)
            {
                return 0;
            }

            var customerIds = candidates.Select(x => x.CustomerId).Distinct().ToList();
            var existingLinkActions = await _context.Notifications
                .AsNoTracking()
                .Where(n => customerIds.Contains(n.CustomerId)
                    && n.LinkAction != null
                    && EF.Functions.Like(n.LinkAction, "/appointments/%"))
                .Select(n => n.LinkAction!)
                .ToListAsync();
            var linkActionSet = new HashSet<string>(existingLinkActions, StringComparer.OrdinalIgnoreCase);

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
                    appointment.Status = AppointmentStatuses.NoShow;
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
                    appointment.Status = AppointmentStatuses.Late;
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
                await _context.SaveChangesAsync();
                foreach (var groupId in groupIds)
                {
                    await _bookingGroupService.SyncCountersAsync(groupId);
                }
            }

            return autoNoShowAppointments.Count;
        }

        private AppointmentLifecyclePolicy ResolveLifecyclePolicy(long branchId, DateTime referenceTime)
        {
            var section = _configuration.GetSection("AppointmentLifecycle");
            var branchSection = section
                .GetSection("BranchOverrides")
                .GetSection(branchId.ToString(CultureInfo.InvariantCulture));

            var preReminderMinutes = ReadLifecycleInt(section, "PreReminderMinutes", DefaultPreReminderMinutes);
            preReminderMinutes = ReadLifecycleInt(branchSection, "PreReminderMinutes", preReminderMinutes);

            var lateReminderMinutes = ReadLifecycleInt(section, "LateReminderMinutes", DefaultLateReminderMinutes);
            lateReminderMinutes = ReadLifecycleInt(branchSection, "LateReminderMinutes", lateReminderMinutes);

            var goldenWarningMinutes = ReadLifecycleInt(section, "GoldenWarningMinutes", DefaultGoldenWarningMinutes);
            goldenWarningMinutes = ReadLifecycleInt(branchSection, "GoldenWarningMinutes", goldenWarningMinutes);

            var quickCancelMinutes = ReadLifecycleInt(section, "QuickFlowCancelMinutes", DefaultQuickCancelMinutes);
            quickCancelMinutes = ReadLifecycleInt(branchSection, "QuickFlowCancelMinutes", quickCancelMinutes);

            var flexibleCancelMinutes = ReadLifecycleInt(section, "FlexibleFlowCancelMinutes", DefaultFlexibleCancelMinutes);
            flexibleCancelMinutes = ReadLifecycleInt(branchSection, "FlexibleFlowCancelMinutes", flexibleCancelMinutes);

            var isWeekend = referenceTime.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            if (isWeekend)
            {
                var weekendCancelMinutes = ReadLifecycleNullableInt(branchSection, "WeekendCancelMinutes")
                    ?? ReadLifecycleNullableInt(section, "WeekendCancelMinutes");
                if (weekendCancelMinutes.HasValue)
                {
                    quickCancelMinutes = weekendCancelMinutes.Value;
                    flexibleCancelMinutes = weekendCancelMinutes.Value;
                }

                var weekendQuickCancelMinutes = ReadLifecycleNullableInt(branchSection, "WeekendQuickFlowCancelMinutes")
                    ?? ReadLifecycleNullableInt(section, "WeekendQuickFlowCancelMinutes");
                if (weekendQuickCancelMinutes.HasValue)
                {
                    quickCancelMinutes = weekendQuickCancelMinutes.Value;
                }

                var weekendFlexibleCancelMinutes = ReadLifecycleNullableInt(branchSection, "WeekendFlexibleFlowCancelMinutes")
                    ?? ReadLifecycleNullableInt(section, "WeekendFlexibleFlowCancelMinutes");
                if (weekendFlexibleCancelMinutes.HasValue)
                {
                    flexibleCancelMinutes = weekendFlexibleCancelMinutes.Value;
                }
            }

            var minCancelMinutes = Math.Max(MinLifecycleMinutes, Math.Min(quickCancelMinutes, flexibleCancelMinutes));
            goldenWarningMinutes = Math.Clamp(goldenWarningMinutes, MinLifecycleMinutes, minCancelMinutes);
            lateReminderMinutes = Math.Clamp(lateReminderMinutes, MinLifecycleMinutes, goldenWarningMinutes);

            return new AppointmentLifecyclePolicy(
                PreReminderMinutes: preReminderMinutes,
                LateReminderMinutes: lateReminderMinutes,
                GoldenWarningMinutes: goldenWarningMinutes,
                QuickCancelMinutes: quickCancelMinutes,
                FlexibleCancelMinutes: flexibleCancelMinutes);
        }

        private static int ReadLifecycleInt(IConfigurationSection section, string key, int fallback)
        {
            if (!section.Exists())
            {
                return fallback;
            }

            var configured = section.GetValue<int?>(key);
            return configured.HasValue
                ? Math.Clamp(configured.Value, MinLifecycleMinutes, MaxLifecycleMinutes)
                : fallback;
        }

        private static int? ReadLifecycleNullableInt(IConfigurationSection section, string key)
        {
            if (!section.Exists())
            {
                return null;
            }

            var configured = section.GetValue<int?>(key);
            return configured.HasValue
                ? Math.Clamp(configured.Value, MinLifecycleMinutes, MaxLifecycleMinutes)
                : null;
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
            if (!string.Equals(appointment.Status, AppointmentStatuses.Pending, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(appointment.Status, AppointmentStatuses.Confirmed, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var minutesToStart = (appointment.StartTime - now).TotalMinutes;
            return minutesToStart >= 0 && minutesToStart <= preReminderMinutes;
        }

        private static bool ShouldSendLateReminder(Appointment appointment, DateTime now, int lateReminderAfterMinutes)
        {
            if (!string.Equals(appointment.Status, AppointmentStatuses.Pending, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(appointment.Status, AppointmentStatuses.Confirmed, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(appointment.Status, AppointmentStatuses.Late, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return (now - appointment.StartTime).TotalMinutes >= lateReminderAfterMinutes;
        }

        private static bool ShouldSwitchToLate(Appointment appointment, DateTime now, int warningAfterMinutes)
        {
            if (!string.Equals(appointment.Status, AppointmentStatuses.Pending, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(appointment.Status, AppointmentStatuses.Confirmed, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return (now - appointment.StartTime).TotalMinutes >= warningAfterMinutes;
        }

        private static bool ShouldAutoCancel(Appointment appointment, DateTime now, int cancelAfterMinutes)
        {
            if (!string.Equals(appointment.Status, AppointmentStatuses.Pending, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(appointment.Status, AppointmentStatuses.Confirmed, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(appointment.Status, AppointmentStatuses.Late, StringComparison.OrdinalIgnoreCase))
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

            _context.Notifications.Add(new Notification
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
            var bays = await _context.Bays
                .AsNoTracking()
                .Where(b => b.BranchId == branchId && b.IsActive == true && b.IsDeleted != true)
                .OrderBy(b => b.Code)
                .ToListAsync();

            var activeSlotBayIds = await _context.WorkSlots
                .AsNoTracking()
                .Where(s => s.BranchId == branchId
                    && s.StartTime <= now
                    && (s.ActualReleaseTime ?? s.EndTime) > now
                    && s.Status != nameof(WorkSlotStatus.CANCELED)
                    && s.Status != nameof(WorkSlotStatus.COMPLETED))
                .Select(s => s.BayId)
                .Distinct()
                .ToListAsync();
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
            var candidate = await _context.Appointments
                .AsNoTracking()
                .Include(a => a.Vehicle)
                .Where(a => a.BranchId == branchId
                    && (a.Status == AppointmentStatuses.Queued
                        || a.Status == AppointmentStatuses.Arrived
                        || a.Status == AppointmentStatuses.ArrivedLate)
                    && (a.CustomerNotes == null
                        || a.CustomerNotes.Contains(AppointmentNoteMarkers.FlowFlexible)
                        || a.CustomerNotes.Contains(AppointmentNoteMarkers.ClusterHeavy)))
                .OrderBy(a => a.StartTime)
                .FirstOrDefaultAsync();

            return candidate?.Vehicle?.LicensePlate;
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

            _context.AiSmartDispatchings.Add(new AiSmartDispatching
            {
                BranchId = noShowAppointment.BranchId,
                Date = DateOnly.FromDateTime(now),
                Recommendation = $"[AUTO_NO_SHOW_APPOINTMENT:{noShowAppointment.Id}] {bayLabel} \u0111\u00E3 tr\u1ED1ng do kh\u00E1ch kh\u00F4ng \u0111\u1EBFn. C\u00F3 th\u1EC3 \u0111\u01B0a {vehicleLabel} v\u00E0o l\u00E0m ngay.",
                EfficiencyScore = null,
                Status = "PENDING",
                CreatedAt = _clock.UtcNow
            });
        }
    }
}
