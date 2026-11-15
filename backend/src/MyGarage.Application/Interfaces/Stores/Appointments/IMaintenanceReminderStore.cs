using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces;

public interface IMaintenanceReminderStore
{
    Task<bool> CustomerExistsAsync(long customerId, CancellationToken ct = default);
    Task<bool> IsCustomerActiveAsync(long customerId, CancellationToken ct = default);
    Task<bool> VehicleExistsAsync(long vehicleId, CancellationToken ct = default);

    Task<bool> HasPendingReminderOnDateAsync(
        long customerId,
        long vehicleId,
        string reminderType,
        DateTime scheduledDate,
        CancellationToken ct = default);

    Task<bool> HasPendingReminderForServiceOrderAsync(
        long customerId,
        long vehicleId,
        long serviceOrderId,
        string reminderType,
        CancellationToken ct = default);

    Task AddReminderInTransactionAsync(MaintenanceReminder entity, CancellationToken ct = default);
    Task AddRemindersInTransactionAsync(IEnumerable<MaintenanceReminder> entities, CancellationToken ct = default);

    Task<ServiceOrder?> GetServiceOrderWithVehicleAndDetailsAsync(long serviceOrderId, CancellationToken ct = default);
    Task<List<long>> GetCurrentOwnerCustomerIdsAsync(long vehicleId, CancellationToken ct = default);

    Task<IReadOnlyList<MaintenanceReminder>> GetDueRemindersAsync(
        DateTime fromInclusive,
        DateTime toExclusive,
        CancellationToken ct = default);

    Task<List<MaintenanceReminder>> GetPendingDueRemindersForProcessingAsync(
        DateTime now,
        int batchSize,
        CancellationToken ct = default);

    Task<MaintenanceReminder?> GetReminderByIdForUpdateAsync(long reminderId, CancellationToken ct = default);

    Task AddNotificationsAndSaveInTransactionAsync(
        IEnumerable<Notification> notifications,
        CancellationToken ct = default);

    Task SaveChangesInTransactionAsync(CancellationToken ct = default);
}
