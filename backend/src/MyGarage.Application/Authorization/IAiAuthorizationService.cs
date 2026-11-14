using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Authorization;

public interface IAiAuthorizationService
{
    Task<bool> CanManageAiModelConfigsAsync(UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanManageAiKnowledgeBaseAsync(UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanAccessAiForecastsAsync(long? branchId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanAccessSmartDispatchingAsync(long? branchId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanAccessChatbotSessionAsync(long customerId, long sessionId, UserActorContext actor, CancellationToken ct = default);
}
