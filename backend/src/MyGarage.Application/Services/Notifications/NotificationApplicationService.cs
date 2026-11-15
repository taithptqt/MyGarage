using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Common.Pagination;
using MyGarage.Application.Notifications;
using MyGarage.Application.Notifications.DTOs;
using MyGarage.Common;
using MyGarage.Domain.Rules;
using MyGarage.DTOs.Responses;
using MyGarage.Extensions;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class NotificationApplicationService : INotificationApplicationService
{
    private readonly INotificationStore _store;
    private readonly ICustomerPortalAuthorizationService _authService;

    public NotificationApplicationService(
        INotificationStore store,
        ICustomerPortalAuthorizationService authService)
    {
        _store = store;
        _authService = authService;
    }

    public async Task<PagedEnvelope<NotificationResponseDto>> GetPagedNotificationsAsync(
        long? customerId,
        int? page,
        int? pageSize,
        CancellationToken ct = default)
    {
        var (normalizedPage, normalizedPageSize) = PaginationExtensions.NormalizePagination(page, pageSize);
        var (items, totalCount) = await _store.GetPagedNotificationsAsync(
            customerId,
            normalizedPage,
            normalizedPageSize,
            ct);

        return PaginationExtensions.CreatePagedEnvelope(items, totalCount, normalizedPage, normalizedPageSize);
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

        var (items, totalCount, unreadCount) = await _store.GetCustomerNotificationPageAsync(
            customerId,
            query.UnreadOnly == true,
            page,
            pageSize,
            ct);

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

        return await _store.MarkNotificationReadAsync(customerId, notificationId, ct);
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

        return await _store.MarkAllNotificationsReadAsync(customerId, ct);
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

        await _store.AddNotificationAsync(notif, ct);

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

        await _store.AddMaintenanceReminderAsync(reminder, ct);

        return MapReminder(reminder);
    }

    public async Task<int> ProcessDueRemindersAsync(int batchSize, CancellationToken ct = default)
    {
        var now = VietnamTime.Now;
        var limit = batchSize <= 0 ? 50 : Math.Min(batchSize, 500);

        var dueList = await _store.GetDueRemindersAsync(now, limit, ct);
        if (dueList.Count == 0)
        {
            return 0;
        }

        var newNotifications = new List<Notification>();
        foreach (var reminder in dueList)
        {
            if (NotificationDomainRules.CanTransitionReminderStatus(reminder.Status, NotificationDomainRules.StatusSent))
            {
                reminder.Status = NotificationDomainRules.StatusSent;
                reminder.SentAt = now;
                reminder.UpdatedAt = now;

                _ = NotificationDomainRules.BuildMaintenanceNotificationKey(
                    reminder.CustomerId,
                    reminder.VehicleId,
                    reminder.ReminderType
                );

                newNotifications.Add(new Notification
                {
                    CustomerId = reminder.CustomerId,
                    Title = "Nhắc nhở bảo dưỡng xe định kỳ",
                    Content = $"Xe của bạn đến hạn bảo dưỡng định kỳ ({reminder.ReminderType}). Vui lòng đặt lịch hẹn để được chăm sóc tốt nhất.",
                    LinkAction = "/my-appointments",
                    IsRead = false,
                    SentAt = now
                });
            }
        }

        if (newNotifications.Count == 0)
        {
            return 0;
        }

        await _store.SaveDueRemindersAsync(newNotifications, ct);
        return newNotifications.Count;
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

        var reminder = await _store.GetMaintenanceReminderByIdAsync(reminderId, ct);
        if (reminder == null)
        {
            return null;
        }

        if (!NotificationDomainRules.CanTransitionReminderStatus(reminder.Status, command.Status))
        {
            throw new InvalidOperationException($"Không thể chuyển trạng thái nhắc bảo dưỡng từ '{reminder.Status}' sang '{command.Status}'.");
        }

        var now = VietnamTime.Now;
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

        await _store.SaveMaintenanceReminderAsync(reminder, ct);

        return MapReminder(reminder);
    }

    private static MaintenanceReminderItemDto MapReminder(MaintenanceReminder reminder) =>
        new(
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
