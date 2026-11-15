using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.CustomerPortal.DTOs;
using MyGarage.DTOs.Responses;

namespace MyGarage.Application.CustomerPortal;

public interface ICustomerPortalNotificationStore
{
    Task<(IReadOnlyList<CustomerPortalNotificationDto> Items, int UnreadCount, int TotalCount)> GetNotificationsAsync(
        long customerId,
        bool? unreadOnly,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<bool> MarkNotificationReadAsync(long customerId, long notificationId, CancellationToken ct = default);

    Task<int> MarkAllNotificationsReadAsync(long customerId, CancellationToken ct = default);
}
