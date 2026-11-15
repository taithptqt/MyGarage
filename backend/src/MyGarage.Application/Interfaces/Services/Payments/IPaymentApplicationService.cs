using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Payments.DTOs;

namespace MyGarage.Application.Payments;

public interface IPaymentApplicationService
{
    Task<PaymentDto?> GetByIdAsync(long id, UserActorContext actor, CancellationToken ct = default);
    Task<PaymentDto> CreatePaymentAsync(PaymentCreateInput input, UserActorContext actor, CancellationToken ct = default);
    Task<PaymentDto> TransitionStatusAsync(long id, PaymentTransitionInput input, UserActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentDto>> GetCustomerPaymentsAsync(long customerId, UserActorContext actor, CancellationToken ct = default);
}
