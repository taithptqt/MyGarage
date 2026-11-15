using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Notifications;
using MyGarage.Application.Notifications.DTOs;
using MyGarage.Domain.Rules;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Notifications;

public sealed class EfNotificationStore : INotificationStore
{
    private readonly IGarageContext _context;

    public EfNotificationStore(IGarageContext context)
    {
        _context = context;
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        try
        {
            return await _context.Database.BeginTransactionAsync();
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("SqlServerRetryingExecutionStrategy"))
            {
                return null;
            }
            throw;
        }
    }

    public async Task<(IReadOnlyList<NotificationResponseDto> Items, int TotalCount)> GetPagedNotificationsAsync(
        long? customerId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.Notifications.AsNoTracking().AsQueryable();
        if (customerId.HasValue)
        {
            query = query.Where(x => x.CustomerId == customerId.Value);
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new NotificationResponseDto
            {
                Id = x.Id,
                CustomerId = x.CustomerId,
                Title = x.Title,
                Content = x.Content,
                IsRead = x.IsRead,
                SentAt = x.SentAt,
                LinkAction = x.LinkAction
            })
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<NotificationItemDto> Items, int TotalCount, int UnreadCount)> GetCustomerNotificationPageAsync(
        long customerId,
        bool unreadOnly,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var q = _context.Notifications.AsNoTracking().Where(n => n.CustomerId == customerId);
        if (unreadOnly)
        {
            q = q.Where(n => n.IsRead != true);
        }

        var totalCount = await q.CountAsync(ct);
        var unreadCount = await _context.Notifications.AsNoTracking()
            .Where(n => n.CustomerId == customerId && n.IsRead != true)
            .CountAsync(ct);

        var items = await q.OrderByDescending(n => n.SentAt ?? DateTime.MinValue)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationItemDto(
                n.Id,
                n.CustomerId,
                n.Title,
                n.Content,
                n.IsRead ?? false,
                n.SentAt,
                n.LinkAction
            ))
            .ToListAsync(ct);

        return (items, totalCount, unreadCount);
    }

    public async Task<bool> MarkNotificationReadAsync(
        long customerId,
        long notificationId,
        CancellationToken ct = default)
    {
        var notif = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.CustomerId == customerId, ct);

        if (notif == null)
        {
            return false;
        }

        if (notif.IsRead == true)
        {
            return true;
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                notif.IsRead = true;
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

        return true;
    }

    public async Task<int> MarkAllNotificationsReadAsync(
        long customerId,
        CancellationToken ct = default)
    {
        var unread = await _context.Notifications
            .Where(n => n.CustomerId == customerId && n.IsRead != true)
            .ToListAsync(ct);

        if (unread.Count == 0)
        {
            return 0;
        }

        var count = unread.Count;
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                foreach (var n in unread)
                {
                    n.IsRead = true;
                }
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

        return count;
    }

    public async Task AddNotificationAsync(Notification notification, CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task AddMaintenanceReminderAsync(MaintenanceReminder reminder, CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                _context.MaintenanceReminders.Add(reminder);
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task<IReadOnlyList<MaintenanceReminder>> GetDueRemindersAsync(
        DateTime now,
        int limit,
        CancellationToken ct = default)
    {
        return await _context.MaintenanceReminders
            .Where(r => r.Status == NotificationDomainRules.StatusScheduled && r.ScheduledAt <= now)
            .OrderBy(r => r.ScheduledAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task SaveDueRemindersAsync(
        IReadOnlyList<Notification> newNotifications,
        CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                foreach (var notif in newNotifications)
                {
                    _context.Notifications.Add(notif);
                }

                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task<MaintenanceReminder?> GetMaintenanceReminderByIdAsync(
        long reminderId,
        CancellationToken ct = default)
    {
        return await _context.MaintenanceReminders
            .FirstOrDefaultAsync(r => r.Id == reminderId, ct);
    }

    public async Task SaveMaintenanceReminderAsync(
        MaintenanceReminder reminder,
        CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });
    }
}
