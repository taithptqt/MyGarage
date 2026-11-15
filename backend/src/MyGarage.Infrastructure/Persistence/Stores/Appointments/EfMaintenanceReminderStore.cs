using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Appointments;

public sealed class EfMaintenanceReminderStore : IMaintenanceReminderStore
{
    private readonly IGarageContext _context;

    public EfMaintenanceReminderStore(IGarageContext context)
    {
        _context = context;
    }

    public Task<bool> CustomerExistsAsync(long customerId, CancellationToken ct = default)
    {
        return _context.Customers
            .AsNoTracking()
            .AnyAsync(x => x.Id == customerId && x.IsDeleted != true, ct);
    }

    public Task<bool> IsCustomerActiveAsync(long customerId, CancellationToken ct = default)
    {
        return _context.Customers
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == customerId &&
                x.IsDeleted != true &&
                x.IsActive != false, ct);
    }

    public Task<bool> VehicleExistsAsync(long vehicleId, CancellationToken ct = default)
    {
        return _context.Vehicles
            .AsNoTracking()
            .AnyAsync(x => x.Id == vehicleId && x.IsDeleted != true, ct);
    }

    public Task<bool> HasPendingReminderOnDateAsync(
        long customerId,
        long vehicleId,
        string reminderType,
        DateTime scheduledDate,
        CancellationToken ct = default)
    {
        return _context.MaintenanceReminders
            .AsNoTracking()
            .AnyAsync(x =>
                x.CustomerId == customerId &&
                x.VehicleId == vehicleId &&
                x.ReminderType == reminderType &&
                x.Status == "PENDING" &&
                x.ScheduledAt.Date == scheduledDate, ct);
    }

    public Task<bool> HasPendingReminderForServiceOrderAsync(
        long customerId,
        long vehicleId,
        long serviceOrderId,
        string reminderType,
        CancellationToken ct = default)
    {
        return _context.MaintenanceReminders
            .AsNoTracking()
            .AnyAsync(x =>
                x.CustomerId == customerId &&
                x.VehicleId == vehicleId &&
                x.ServiceOrderId == serviceOrderId &&
                x.ReminderType == reminderType &&
                x.Status == "PENDING", ct);
    }

    public async Task AddReminderInTransactionAsync(MaintenanceReminder entity, CancellationToken ct = default)
    {
        _context.MaintenanceReminders.Add(entity);
        await SaveChangesInTransactionAsync(ct);
    }

    public async Task AddRemindersInTransactionAsync(IEnumerable<MaintenanceReminder> entities, CancellationToken ct = default)
    {
        foreach (var entity in entities)
        {
            _context.MaintenanceReminders.Add(entity);
        }

        await SaveChangesInTransactionAsync(ct);
    }

    public Task<ServiceOrder?> GetServiceOrderWithVehicleAndDetailsAsync(long serviceOrderId, CancellationToken ct = default)
    {
        return _context.ServiceOrders
            .AsNoTracking()
            .Include(x => x.Vehicle)
            .Include(x => x.ServiceOrderDetails)
            .FirstOrDefaultAsync(x => x.Id == serviceOrderId, ct);
    }

    public Task<List<long>> GetCurrentOwnerCustomerIdsAsync(long vehicleId, CancellationToken ct = default)
    {
        return _context.CustomerVehicles
            .AsNoTracking()
            .Where(x =>
                x.VehicleId == vehicleId &&
                x.IsDeleted != true &&
                x.IsCurrentOwner != false &&
                x.Customer.IsDeleted != true &&
                x.Customer.IsActive != false)
            .Select(x => x.CustomerId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MaintenanceReminder>> GetDueRemindersAsync(
        DateTime fromInclusive,
        DateTime toExclusive,
        CancellationToken ct = default)
    {
        return await _context.MaintenanceReminders
            .AsNoTracking()
            .Where(x =>
                x.Status == "PENDING" &&
                x.ScheduledAt >= fromInclusive &&
                x.ScheduledAt < toExclusive)
            .OrderBy(x => x.ScheduledAt)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);
    }

    public Task<List<MaintenanceReminder>> GetPendingDueRemindersForProcessingAsync(
        DateTime now,
        int batchSize,
        CancellationToken ct = default)
    {
        return _context.MaintenanceReminders
            .Include(x => x.Vehicle)
            .Where(x => x.Status == "PENDING" && x.ScheduledAt <= now)
            .OrderBy(x => x.ScheduledAt)
            .ThenBy(x => x.Id)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public Task<MaintenanceReminder?> GetReminderByIdForUpdateAsync(long reminderId, CancellationToken ct = default)
    {
        return _context.MaintenanceReminders
            .FirstOrDefaultAsync(x => x.Id == reminderId, ct);
    }

    public async Task AddNotificationsAndSaveInTransactionAsync(
        IEnumerable<Notification> notifications,
        CancellationToken ct = default)
    {
        foreach (var notification in notifications)
        {
            _context.Notifications.Add(notification);
        }

        await SaveChangesInTransactionAsync(ct);
    }

    public async Task SaveChangesInTransactionAsync(CancellationToken ct = default)
    {
        await ExecuteInTransactionAsync(async () =>
        {
            await _context.SaveChangesAsync(ct);
        }, ct);
    }

    private async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync(cancellationToken);
            await action();
            if (tx != null)
            {
                await tx.CommitAsync(cancellationToken);
            }
        });
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await _context.Database.BeginTransactionAsync(cancellationToken);
    }
}
