using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Common.Pagination;
using MyGarage.Application.Notifications.DTOs;
using MyGarage.DTOs.Responses;

namespace MyGarage.Application.Notifications;

public interface INotificationApplicationService
{
    Task<PagedEnvelope<NotificationResponseDto>> GetPagedNotificationsAsync(long? customerId, int? page, int? pageSize, CancellationToken ct = default);
    Task<NotificationPageResult> GetNotificationsAsync(long customerId, NotificationPageQuery query, UserActorContext actor, CancellationToken ct = default);
    Task<bool> MarkNotificationReadAsync(long customerId, long notificationId, UserActorContext actor, CancellationToken ct = default);
    Task<int> MarkAllNotificationsReadAsync(long customerId, UserActorContext actor, CancellationToken ct = default);
    Task<NotificationItemDto> CreateNotificationAsync(NotificationCreateCommand command, UserActorContext actor, CancellationToken ct = default);
    Task<MaintenanceReminderItemDto> ScheduleMaintenanceReminderAsync(MaintenanceReminderScheduleCommand command, UserActorContext actor, CancellationToken ct = default);
    Task<int> ProcessDueRemindersAsync(int batchSize, CancellationToken ct = default);
    Task<MaintenanceReminderItemDto?> PatchReminderStatusAsync(long reminderId, MaintenanceReminderPatchStatusCommand command, UserActorContext actor, CancellationToken ct = default);
}
