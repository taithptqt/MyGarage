using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces;

public interface ICustomerPortalService
{
    Task<CustomerPortalDashboardDto> GetDashboardAsync(long customerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerPortalVehicleDto>> GetVehiclesAsync(long customerId, CancellationToken cancellationToken = default);

    Task<CustomerPortalVehicleDto> CreateVehicleAsync(long customerId, CustomerPortalVehicleUpsertDto dto, CancellationToken cancellationToken = default);

    Task<CustomerPortalVehicleDto?> UpdateVehicleAsync(long customerId, long vehicleId, CustomerPortalVehicleUpsertDto dto, CancellationToken cancellationToken = default);

    Task<CustomerPortalVehicleHistoryDto?> GetVehicleHistoryAsync(long customerId, long vehicleId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<CustomerPortalNotificationDto> items, int unreadCount)> GetNotificationsAsync(
        long customerId, bool? unreadOnly, CancellationToken cancellationToken = default);

    Task<bool> MarkNotificationReadAsync(long customerId, long notificationId, CancellationToken cancellationToken = default);

    Task<int> MarkAllNotificationsReadAsync(long customerId, CancellationToken cancellationToken = default);

    Task<CustomerPortalVehicleLookupDto> GetVehicleLookupsAsync(CancellationToken cancellationToken = default);

    Task<CustomerPortalServiceOrderLinkContextDto?> GetServiceOrderLinkContextAsync(
        long customerId,
        long serviceOrderId,
        CancellationToken cancellationToken = default);
}
