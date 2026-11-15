using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MyGarage.Application.Interfaces.Stores.Appointments;
using MyGarage.Constants;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Appointments;

public sealed partial class EfAppointmentStore
{
    private const int DefaultPreReminderMinutes = 30;
    private const int DefaultLateReminderMinutes = 10;
    private const int DefaultGoldenWarningMinutes = 15;
    private const int DefaultQuickCancelMinutes = 15;
    private const int DefaultFlexibleCancelMinutes = 30;
    private const int MinLifecycleMinutes = 1;
    private const int MaxLifecycleMinutes = 240;

    public async Task<List<Appointment>> GetAutoNoShowCandidatesAsync(long branchId, DateTime maxStartTime)
    {
        return await _context.Appointments
            .Where(a => a.BranchId == branchId
                && (a.Status == nameof(AppointmentStatus.PENDING)
                    || a.Status == nameof(AppointmentStatus.CONFIRMED)
                    || a.Status == nameof(AppointmentStatus.LATE))
                && a.StartTime <= maxStartTime)
            .OrderBy(a => a.StartTime)
            .ToListAsync();
    }

    public async Task<string?> GetFirstFlexibleQueueVehiclePlateAsync(long branchId)
    {
        var candidate = await _context.Appointments
            .AsNoTracking()
            .Include(a => a.Vehicle)
            .Where(a => a.BranchId == branchId
                && (a.Status == nameof(AppointmentStatus.PENDING)
                    || a.Status == nameof(AppointmentStatus.ARRIVED)
                    || a.Status == nameof(AppointmentStatus.ARRIVED_LATE))
                && (a.CustomerNotes == null
                    || a.CustomerNotes.Contains(AppointmentNoteMarkers.FlowFlexible)
                    || a.CustomerNotes.Contains(AppointmentNoteMarkers.ClusterHeavy)))
            .OrderBy(a => a.StartTime)
            .FirstOrDefaultAsync();

        return candidate?.Vehicle?.LicensePlate;
    }

    public async Task<HashSet<string>> GetAppointmentNotificationLinkActionsAsync(IReadOnlyCollection<long> customerIds)
    {
        if (customerIds.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var actions = await _context.Notifications
            .AsNoTracking()
            .Where(n => customerIds.Contains(n.CustomerId)
                && n.LinkAction != null
                && EF.Functions.Like(n.LinkAction, "/appointments/%"))
            .Select(n => n.LinkAction!)
            .ToListAsync();

        return new HashSet<string>(actions, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> HasNotificationSentSinceAsync(long customerId, string linkAction, DateTime sinceDate)
    {
        return await _context.Notifications
            .AnyAsync(n => n.CustomerId == customerId
                && n.LinkAction == linkAction
                && n.SentAt.HasValue
                && n.SentAt.Value >= sinceDate);
    }

    public async Task<long?> FindPendingMaintenanceReminderIdAsync(long customerId, long vehicleId, DateTime now)
    {
        var normalizedStatuses = new[] { "PENDING", "SENT", "CUSTOMER_RESPONDED" };
        return await _context.MaintenanceReminders
            .AsNoTracking()
            .Where(x =>
                x.CustomerId == customerId &&
                x.VehicleId == vehicleId &&
                normalizedStatuses.Contains(x.Status) &&
                x.ResultAppointmentId == null)
            .OrderByDescending(x => x.ScheduledAt <= now)
            .ThenBy(x => Math.Abs(EF.Functions.DateDiffMinute(x.ScheduledAt, now)))
            .ThenByDescending(x => x.Id)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync();
    }

    public AppointmentLifecycleConfigSnapshot GetLifecycleConfig(long branchId, bool isWeekend)
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

        return new AppointmentLifecycleConfigSnapshot(
            preReminderMinutes,
            lateReminderMinutes,
            goldenWarningMinutes,
            quickCancelMinutes,
            flexibleCancelMinutes);
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

    public void AddNotification(Notification notification) => _context.Notifications.Add(notification);
    public void AddAiSmartDispatching(AiSmartDispatching dispatching) => _context.AiSmartDispatchings.Add(dispatching);
}
