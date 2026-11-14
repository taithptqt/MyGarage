using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Authorization;

public interface ICustomerAuthorizationService
{
    Task<bool> CanAccessCustomerAsync(long customerId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanManageCustomersAsync(UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanAccessBranchCustomersAsync(long? requestedBranchId, UserActorContext actor, CancellationToken ct = default);
}
