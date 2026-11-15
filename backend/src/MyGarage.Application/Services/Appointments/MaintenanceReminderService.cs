using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class MaintenanceReminderService : IMaintenanceReminderService
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "PENDING",
        "SENT",
        "CUSTOMER_RESPONDED",
        "BOOKED",
        "DISMISSED"
    };

    private static readonly HashSet<string> AllowedChannels = new(StringComparer.OrdinalIgnoreCase)
    {
        "SMS",
        "EMAIL",
        "PUSH"
    };

    private readonly IMaintenanceReminderStore _store;
    private readonly ISystemClock _clock;

    public MaintenanceReminderService(IMaintenanceReminderStore store, ISystemClock clock)
    {
        _store = store;
        _clock = clock;
    }

    public async Task<object> ScheduleAsync(
        MaintenanceReminderScheduleRequestDto request,
        long? actorId,
        CancellationToken cancellationToken = default)
    {
        if (request.CustomerId <= 0 || request.VehicleId <= 0)
        {
            throw new ArgumentException("CustomerId and VehicleId are required.");
        }

        var customerExists = await _store.CustomerExistsAsync(request.CustomerId, cancellationToken);
        if (!customerExists)
        {
            throw new InvalidOperationException("Customer does not exist.");
        }

        var vehicleExists = await _store.VehicleExistsAsync(request.VehicleId, cancellationToken);
        if (!vehicleExists)
        {
            throw new InvalidOperationException("Vehicle does not exist.");
        }

        var scheduledAt = request.ScheduledAt ?? _clock.LocalNow;
        var reminderType = NormalizeReminderType(request.ReminderType);
        var channel = NormalizeChannel(request.Channel);

        var duplicateExists = await _store.HasPendingReminderOnDateAsync(
            request.CustomerId,
            request.VehicleId,
            reminderType,
            scheduledAt.Date,
            cancellationToken);
        if (duplicateExists)
        {
            throw new InvalidOperationException("A pending reminder with the same type already exists for this customer/vehicle/date.");
        }

        var entity = new MaintenanceReminder
        {
            CustomerId = request.CustomerId,
            VehicleId = request.VehicleId,
            ServiceOrderId = request.ServiceOrderId,
            ReminderType = reminderType,
            ScheduledAt = scheduledAt,
            ReminderMileage = request.ReminderMileage,
            EstimatedServices = NormalizeEstimatedServices(request.EstimatedServices),
            Channel = channel,
            Status = "PENDING",
            CreatedAt = _clock.LocalNow,
            CreatedBy = actorId
        };

        await _store.AddReminderInTransactionAsync(entity, cancellationToken);

        return BuildReminderPayload(entity);
    }

    public async Task<object> ScheduleFromServiceOrderAsync(
        long serviceOrderId,
        int? remindAfterDays,
        int? remindAfterMileage,
        long? actorId,
        CancellationToken cancellationToken = default)
    {
        if (serviceOrderId <= 0)
        {
            throw new ArgumentException("Invalid serviceOrderId.");
        }

        var serviceOrder = await _store.GetServiceOrderWithVehicleAndDetailsAsync(serviceOrderId, cancellationToken);
        if (serviceOrder == null)
        {
            throw new InvalidOperationException("Service order not found.");
        }

        var baseDate = serviceOrder.ActualHandover ?? serviceOrder.UpdatedAt ?? serviceOrder.CreatedAt ?? _clock.LocalNow;
        var scheduleDays = Math.Clamp(remindAfterDays ?? 180, 1, 3650);
        var scheduledAt = baseDate.Date.AddDays(scheduleDays).AddHours(8);

        var estimateMileageDelta = Math.Max(0, remindAfterMileage ?? 5000);
        var targetMileage = serviceOrder.Vehicle?.Odo.HasValue == true
            ? serviceOrder.Vehicle.Odo.Value + estimateMileageDelta
            : (int?)null;

        var topServices = serviceOrder.ServiceOrderDetails
            .Where(x => !string.IsNullOrWhiteSpace(x.ItemName))
            .Select(x => x.ItemName.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();
        var estimatedServices = topServices.Length == 0 ? null : JsonSerializer.Serialize(topServices);

        var ownerCustomerIds = await _store.GetCurrentOwnerCustomerIdsAsync(serviceOrder.VehicleId, cancellationToken);

        if (ownerCustomerIds.Count == 0)
        {
            ownerCustomerIds.Add(serviceOrder.CustomerId);
        }

        var now = _clock.LocalNow;
        var created = new List<MaintenanceReminder>();
        foreach (var customerId in ownerCustomerIds)
        {
            var duplicateExists = await _store.HasPendingReminderForServiceOrderAsync(
                customerId,
                serviceOrder.VehicleId,
                serviceOrderId,
                "NextPeriodicService",
                cancellationToken);
            if (duplicateExists)
            {
                continue;
            }

            var reminder = new MaintenanceReminder
            {
                CustomerId = customerId,
                VehicleId = serviceOrder.VehicleId,
                ServiceOrderId = serviceOrderId,
                ReminderType = "NextPeriodicService",
                ScheduledAt = scheduledAt,
                ReminderMileage = targetMileage,
                EstimatedServices = estimatedServices,
                Channel = "PUSH",
                Status = "PENDING",
                CreatedAt = now,
                CreatedBy = actorId
            };

            created.Add(reminder);
        }

        await _store.AddRemindersInTransactionAsync(created, cancellationToken);

        return new
        {
            serviceOrderId,
            scheduledAt,
            createdCount = created.Count,
            reminders = created.Select(BuildReminderPayload).ToList()
        };
    }

    public async Task<IReadOnlyList<object>> GetDueAsync(
        DateTime fromInclusive,
        DateTime toExclusive,
        CancellationToken cancellationToken = default)
    {
        var dueRows = await _store.GetDueRemindersAsync(fromInclusive, toExclusive, cancellationToken);

        return dueRows
            .Select(x => (object)new
            {
                x.Id,
                x.CustomerId,
                x.VehicleId,
                x.ServiceOrderId,
                x.ReminderType,
                x.ScheduledAt,
                x.ReminderMileage,
                x.EstimatedServices,
                x.Channel,
                x.Status
            })
            .ToList();
    }

    public async Task<object> ProcessDueAsync(int batchSize = 200, CancellationToken cancellationToken = default)
    {
        if (batchSize < 1)
        {
            batchSize = 1;
        }
        if (batchSize > 1000)
        {
            batchSize = 1000;
        }

        var now = _clock.LocalNow;
        var dueRows = await _store.GetPendingDueRemindersForProcessingAsync(now, batchSize, cancellationToken);

        if (dueRows.Count == 0)
        {
            return new
            {
                processed = 0,
                sent = 0,
                skipped = 0
            };
        }

        var sent = 0;
        var skipped = 0;
        var notifications = new List<Notification>();
        foreach (var row in dueRows)
        {
            var customerExists = await _store.IsCustomerActiveAsync(row.CustomerId, cancellationToken);
            if (!customerExists)
            {
                row.Status = "DISMISSED";
                row.CustomerResponse = "Customer inactive or deleted";
                row.UpdatedAt = now;
                skipped++;
                continue;
            }

            notifications.Add(new Notification
            {
                CustomerId = row.CustomerId,
                Title = "Nhắc bảo dưỡng định kỳ",
                Content = BuildReminderContent(row),
                IsRead = false,
                SentAt = _clock.UtcNow,
                LinkAction = $"/my-vehicles/{row.VehicleId}?tab=maintenance&notificationType=maintenance-reminder&reminderId={row.Id}"
            });

            row.Status = "SENT";
            row.SentAt = now;
            row.UpdatedAt = now;
            sent++;
        }

        await _store.AddNotificationsAndSaveInTransactionAsync(notifications, cancellationToken);

        return new
        {
            processed = dueRows.Count,
            sent,
            skipped
        };
    }

    public async Task<object?> PatchStatusAsync(
        long reminderId,
        string status,
        string? customerResponse,
        long? resultAppointmentId,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeStatus(status);
        if (!AllowedStatuses.Contains(normalized))
        {
            throw new ArgumentException("Invalid status.");
        }

        var entity = await _store.GetReminderByIdForUpdateAsync(reminderId, cancellationToken);
        if (entity == null)
        {
            return null;
        }

        entity.Status = normalized;
        entity.CustomerResponse = string.IsNullOrWhiteSpace(customerResponse) ? null : customerResponse.Trim();
        entity.ResultAppointmentId = resultAppointmentId > 0 ? resultAppointmentId : null;
        entity.UpdatedAt = _clock.LocalNow;

        if (string.Equals(normalized, "SENT", StringComparison.OrdinalIgnoreCase) && entity.SentAt == null)
        {
            entity.SentAt = _clock.LocalNow;
        }

        await _store.SaveChangesInTransactionAsync(cancellationToken);
        return BuildReminderPayload(entity);
    }

    private static string BuildReminderContent(MaintenanceReminder reminder)
    {
        var plate = reminder.Vehicle?.LicensePlate;
        var vehicleLabel = string.IsNullOrWhiteSpace(plate) ? $"xe #{reminder.VehicleId}" : $"xe {plate}";
        var mileageNote = reminder.ReminderMileage.HasValue
            ? $" hoặc khi đạt mốc {reminder.ReminderMileage.Value:N0} km"
            : string.Empty;
        return $"Đã đến lịch bảo dưỡng {vehicleLabel} ({reminder.ReminderType.ToLowerInvariant()}){mileageNote}.";
    }

    private static object BuildReminderPayload(MaintenanceReminder x)
    {
        return new
        {
            x.Id,
            x.CustomerId,
            x.VehicleId,
            x.ServiceOrderId,
            x.ReminderType,
            x.ScheduledAt,
            x.ReminderMileage,
            x.EstimatedServices,
            x.Channel,
            x.Status,
            x.SentAt,
            x.CustomerResponse,
            x.ResultAppointmentId,
            x.CreatedAt,
            x.CreatedBy,
            x.UpdatedAt,
            x.UpdatedBy
        };
    }

    private static string NormalizeStatus(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", "_");
        return normalized switch
        {
            "CUSTOMERRESPONDED" => "CUSTOMER_RESPONDED",
            _ => normalized
        };
    }

    private static string NormalizeChannel(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "PUSH" : value.Trim().ToUpperInvariant();
        return AllowedChannels.Contains(normalized) ? normalized : "PUSH";
    }

    private static string NormalizeReminderType(string? value)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? "NextPeriodicService" : value.Trim();
        return trimmed.Length > 50 ? trimmed[..50] : trimmed;
    }

    private static string? NormalizeEstimatedServices(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > 1000 ? trimmed[..1000] : trimmed;
    }
}
