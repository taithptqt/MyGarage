using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Customers.DTOs;

namespace MyGarage.Application.Customers;

public interface ICustomerApplicationService
{
    Task<CustomerAdminPageResult> GetCustomerAdminPageAsync(CustomerAdminPageQuery query, UserActorContext actor, CancellationToken ct = default);
    Task<CustomerDetailDto?> GetCustomerByIdAsync(long id, UserActorContext actor, CancellationToken ct = default);
    Task<CustomerDetailDto> CreateCustomerAsync(CustomerCreateCommand command, UserActorContext actor, CancellationToken ct = default);
    Task<CustomerDetailDto?> UpdateCustomerAsync(long id, CustomerUpdateCommand command, UserActorContext actor, CancellationToken ct = default);
    Task<bool> DeleteCustomerAsync(long id, UserActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerVehicleSummaryDto>> GetCustomerVehiclesAsync(long customerId, UserActorContext actor, CancellationToken ct = default);
}
