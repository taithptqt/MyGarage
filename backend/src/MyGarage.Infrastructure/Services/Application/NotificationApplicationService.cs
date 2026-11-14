using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Authorization;
using MyGarage.Application.Notifications;
using MyGarage.Application.Notifications.DTOs;
using MyGarage.Common;
using MyGarage.Domain.Rules;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class NotificationApplicationService : INotificationApplicationService
{
    private readonly IGarageContext _context;
    private readonly ICustomerPortalAuthorizationService _authService;

    public NotificationApplicationService(
        IGarageContext context,
        ICustomerPortalAuthorizationService authService)
    {
        _context = context;
        _authService = authService;
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

    public async Task<NotificationPageResult> GetNotificationsAsync(
        long customerId,
        NotificationPageQuery query,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanAccessCustomerDashboardAsync(customerId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền truy cập thông báo của khách hàng này.");
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var q = _context.Notifications.AsNoTracking().Where(n => n.CustomerId == customerId);
        if (query.UnreadOnly == true)
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

        return new NotificationPageResult(items, totalCount, unreadCount, page, pageSize);
    }

    public async Task<bool> MarkNotificationReadAsync(
        long customerId,
        long notificationId,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanAccessNotificationAsync(notificationId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền truy cập thông báo này.");
        }

        var notif = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.CustomerId == customerId, ct);

        if (notif == null)
        {
            return false;
        }

        if (notif.IsRead == true)
        {
            return true; // Idempotent
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
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanAccessCustomerDashboardAsync(customerId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền thao tác trên thông báo của khách hàng này.");
        }

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

    public async Task<NotificationItemDto> CreateNotificationAsync(
        NotificationCreateCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!actor.IsAdmin && !string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Chỉ quản trị viên hoặc nhân viên mới có quyền tạo thông báo.");
        }

        if (string.IsNullOrWhiteSpace(command.Title))
        {
            throw new ArgumentException("Tiêu đề thông báo không được để trống.", nameof(command.Title));
        }

        if (string.IsNullOrWhiteSpace(command.Content))
        {
            throw new ArgumentException("Nội dung thông báo không được để trống.", nameof(command.Content));
        }

        if (!NotificationDomainRules.SanitizeAndValidateLinkAction(command.LinkAction, out var sanitizedLink))
        {
            throw new ArgumentException("Đường dẫn liên kết không hợp lệ hoặc không thuộc danh sách được phép.", nameof(command.LinkAction));
        }

        var now = VietnamTime.Now;
        var notif = new Notification
        {
            CustomerId = command.CustomerId,
            Title = command.Title.Trim(),
            Content = command.Content.Trim(),
            LinkAction = sanitizedLink,
            IsRead = false,
            SentAt = now
        };

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                _context.Notifications.Add(notif);
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

        return new NotificationItemDto(
            notif.Id,
            notif.CustomerId,
            notif.Title,
            notif.Content,
            notif.IsRead ?? false,
            notif.SentAt,
            notif.LinkAction
        );
    }

    public async Task<MaintenanceReminderItemDto> ScheduleMaintenanceReminderAsync(
        MaintenanceReminderScheduleCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanManageMaintenanceRemindersAsync(null, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền lên lịch nhắc bảo dưỡng.");
        }

        if (!NotificationDomainRules.IsValidReminderChannel(command.Channel))
        {
            throw new ArgumentException($"Kênh gửi nhắc nhở '{command.Channel}' không hợp lệ.");
        }

        var now = VietnamTime.Now;
        var reminder = new MaintenanceReminder
        {
            CustomerId = command.CustomerId,
            VehicleId = command.VehicleId,
            ServiceOrderId = command.ServiceOrderId,
            ReminderType = command.ReminderType.Trim(),
            ScheduledAt = command.ScheduledAt,
            ReminderMileage = command.ReminderMileage,
            EstimatedServices = command.EstimatedServices?.Trim(),
            Channel = command.Channel.Trim().ToUpperInvariant(),
            Status = NotificationDomainRules.StatusScheduled,
            CreatedAt = now,
            CreatedBy = actor.UserId,
            UpdatedAt = now,
            UpdatedBy = actor.UserId
        };

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

        return new MaintenanceReminderItemDto(
            reminder.Id,
            reminder.CustomerId,
            reminder.VehicleId,
            reminder.ServiceOrderId,
            reminder.ReminderType,
            reminder.ScheduledAt,
            reminder.ReminderMileage,
            reminder.EstimatedServices,
            reminder.Channel,
            reminder.Status,
            reminder.SentAt,
            reminder.CustomerResponse,
            reminder.ResultAppointmentId
        );
    }

    public async Task<int> ProcessDueRemindersAsync(int batchSize, CancellationToken ct = default)
    {
        var now = VietnamTime.Now;
        var limit = batchSize <= 0 ? 50 : Math.Min(batchSize, 500);

        var dueList = await _context.MaintenanceReminders
            .Where(r => r.Status == NotificationDomainRules.StatusScheduled && r.ScheduledAt <= now)
            .OrderBy(r => r.ScheduledAt)
            .Take(limit)
            .ToListAsync(ct);

        if (dueList.Count == 0)
        {
            return 0;
        }

        var processedCount = 0;
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                foreach (var reminder in dueList)
                {
                    if (NotificationDomainRules.CanTransitionReminderStatus(reminder.Status, NotificationDomainRules.StatusSent))
                    {
                        reminder.Status = NotificationDomainRules.StatusSent;
                        reminder.SentAt = now;
                        reminder.UpdatedAt = now;

                        // Create in-app notification for the customer
                        var notifKey = NotificationDomainRules.BuildMaintenanceNotificationKey(
                            reminder.CustomerId,
                            reminder.VehicleId,
                            reminder.ReminderType
                        );

                        var notif = new Notification
                        {
                            CustomerId = reminder.CustomerId,
                            Title = "Nhắc nhở bảo dưỡng xe định kỳ",
                            Content = $"Xe của bạn đến hạn bảo dưỡng định kỳ ({reminder.ReminderType}). Vui lòng đặt lịch hẹn để được chăm sóc tốt nhất.",
                            LinkAction = "/my-appointments",
                            IsRead = false,
                            SentAt = now
                        };
                        _context.Notifications.Add(notif);

                        processedCount++;
                    }
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

        return processedCount;
    }

    public async Task<MaintenanceReminderItemDto?> PatchReminderStatusAsync(
        long reminderId,
        MaintenanceReminderPatchStatusCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanManageMaintenanceRemindersAsync(null, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền cập nhật trạng thái nhắc bảo dưỡng.");
        }

        var reminder = await _context.MaintenanceReminders
            .FirstOrDefaultAsync(r => r.Id == reminderId, ct);

        if (reminder == null)
        {
            return null;
        }

        if (!NotificationDomainRules.CanTransitionReminderStatus(reminder.Status, command.Status))
        {
            throw new InvalidOperationException($"Không thể chuyển trạng thái nhắc bảo dưỡng từ '{reminder.Status}' sang '{command.Status}'.");
        }

        var now = VietnamTime.Now;
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                reminder.Status = command.Status.Trim().ToUpperInvariant();
                if (!string.IsNullOrWhiteSpace(command.CustomerResponse))
                {
                    reminder.CustomerResponse = command.CustomerResponse.Trim();
                }
                if (command.ResultAppointmentId.HasValue)
                {
                    reminder.ResultAppointmentId = command.ResultAppointmentId;
                }
                reminder.UpdatedAt = now;
                reminder.UpdatedBy = actor.UserId;

                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

        return new MaintenanceReminderItemDto(
            reminder.Id,
            reminder.CustomerId,
            reminder.VehicleId,
            reminder.ServiceOrderId,
            reminder.ReminderType,
            reminder.ScheduledAt,
            reminder.ReminderMileage,
            reminder.EstimatedServices,
            reminder.Channel,
            reminder.Status,
            reminder.SentAt,
            reminder.CustomerResponse,
            reminder.ResultAppointmentId
        );
    }
}
