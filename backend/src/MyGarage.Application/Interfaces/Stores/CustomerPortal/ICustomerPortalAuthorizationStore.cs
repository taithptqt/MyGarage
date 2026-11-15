using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.CustomerPortal;

public interface ICustomerPortalAuthorizationStore
{
    Task<CustomerPortalOrderSnapshot?> GetOrderSnapshotAsync(long customerId, long serviceOrderId, CancellationToken ct = default);

    Task<(bool Exists, string? Note)> GetActiveCustomerNoteAsync(long customerId, CancellationToken ct = default);

    Task<bool> HasBranchServiceOrderAsync(long customerId, long branchId, CancellationToken ct = default);

    Task<bool> HasBranchAppointmentAsync(long customerId, long branchId, CancellationToken ct = default);

    Task<bool> CustomerHasVehicleAsync(long customerId, long vehicleId, bool requireCurrentOwner, CancellationToken ct = default);

    Task<CustomerPortalConversationAccessInfo?> GetConversationAccessInfoAsync(long conversationId, CancellationToken ct = default);

    Task<bool> IsCustomerNotificationAsync(long notificationId, long customerId, CancellationToken ct = default);
}
