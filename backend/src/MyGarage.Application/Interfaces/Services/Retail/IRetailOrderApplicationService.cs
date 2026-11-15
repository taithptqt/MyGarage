using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Retail.DTOs;

namespace MyGarage.Application.Retail;

public interface IRetailOrderApplicationService
{
    Task<RetailOrderDto> CheckoutAsync(RetailCheckoutInput input, UserActorContext actor, CancellationToken ct = default);
    Task<RetailOrderDto?> GetOrderByIdAsync(long id, UserActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<RetailOrderDto>> GetOrdersAsync(RetailOrderFilterInput filter, UserActorContext actor, CancellationToken ct = default);
    Task<RetailCustomerOrderPageResultDto> GetCurrentCustomerOrdersPageAsync(RetailCustomerOrderPageInput input, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CancelOrderAsync(long id, UserActorContext actor, CancellationToken ct = default);
    Task<bool> DeleteOrderAsync(long id, UserActorContext actor, CancellationToken ct = default);
}
