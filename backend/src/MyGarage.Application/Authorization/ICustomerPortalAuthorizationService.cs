using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Authorization;

public interface ICustomerPortalAuthorizationService
{
    Task<bool> CanAccessCustomerDashboardAsync(long customerId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanManageCustomerVehicleAsync(long customerId, long vehicleId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanAccessConversationAsync(long conversationId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanAccessBranchChatInboxAsync(long branchId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanAccessNotificationAsync(long notificationId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanManageMaintenanceRemindersAsync(long? branchId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanSubmitCustomerFeedbackAsync(long customerId, UserActorContext actor, CancellationToken ct = default);
}
