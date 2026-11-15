using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Notifications.DTOs;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Application.Notifications;

public interface INotificationStore
{
    Task<(IReadOnlyList<NotificationResponseDto> Items, int TotalCount)> GetPagedNotificationsAsync(
        long? customerId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<(IReadOnlyList<NotificationItemDto> Items, int TotalCount, int UnreadCount)> GetCustomerNotificationPageAsync(
        long customerId,
        bool unreadOnly,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<bool> MarkNotificationReadAsync(
        long customerId,
        long notificationId,
        CancellationToken ct = default);

    Task<int> MarkAllNotificationsReadAsync(
        long customerId,
        CancellationToken ct = default);

    Task AddNotificationAsync(Notification notification, CancellationToken ct = default);

    Task AddMaintenanceReminderAsync(MaintenanceReminder reminder, CancellationToken ct = default);

    Task<IReadOnlyList<MaintenanceReminder>> GetDueRemindersAsync(
        DateTime now,
        int limit,
        CancellationToken ct = default);

    Task SaveDueRemindersAsync(
        IReadOnlyList<Notification> newNotifications,
        CancellationToken ct = default);

    Task<MaintenanceReminder?> GetMaintenanceReminderByIdAsync(
        long reminderId,
        CancellationToken ct = default);

    Task SaveMaintenanceReminderAsync(
        MaintenanceReminder reminder,
        CancellationToken ct = default);
}
