using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Authorization;

public interface IPaymentAuthorizationService
{
    Task<bool> CanViewPaymentsAsync(long? branchId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanViewPaymentAsync(long paymentId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanCreatePaymentAsync(long? branchId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanTransitionPaymentStatusAsync(long paymentId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanRefundPaymentAsync(long paymentId, UserActorContext actor, CancellationToken ct = default);
}
