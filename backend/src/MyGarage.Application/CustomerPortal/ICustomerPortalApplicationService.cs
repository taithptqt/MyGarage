using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.CustomerPortal.DTOs;

namespace MyGarage.Application.CustomerPortal;

public interface ICustomerPortalApplicationService
{
    Task<CustomerPortalDashboardSummary> GetDashboardSummaryAsync(long customerId, UserActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerPortalVehicleItemDto>> GetCustomerVehiclesAsync(long customerId, UserActorContext actor, CancellationToken ct = default);
    Task<CustomerPortalVehicleItemDto> RegisterVehicleAsync(long customerId, CustomerPortalVehicleUpsertCommand command, UserActorContext actor, CancellationToken ct = default);
    Task<CustomerPortalVehicleItemDto?> UpdateVehicleAsync(long customerId, long vehicleId, CustomerPortalVehicleUpsertCommand command, UserActorContext actor, CancellationToken ct = default);
    Task<CustomerPortalOrderLinkContext?> GetServiceOrderLinkContextAsync(long customerId, long serviceOrderId, UserActorContext actor, CancellationToken ct = default);
}
