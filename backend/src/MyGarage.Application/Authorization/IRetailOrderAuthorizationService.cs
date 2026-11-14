using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Authorization;

public interface IRetailOrderAuthorizationService
{
    Task<bool> CanViewRetailOrdersAsync(long? branchId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanViewRetailOrderAsync(long retailOrderId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanCheckoutRetailOrderAsync(long branchId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanCancelRetailOrderAsync(long retailOrderId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanDeleteRetailOrderAsync(long retailOrderId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanViewRetailReturnsAsync(long? branchId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanFinalizeRetailReturnAsync(long retailReturnId, UserActorContext actor, CancellationToken ct = default);
}
