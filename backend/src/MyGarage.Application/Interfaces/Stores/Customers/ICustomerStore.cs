using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Customers.DTOs;
using MyGarage.Models;

namespace MyGarage.Application.Customers;

public interface ICustomerStore
{
    Task<(IReadOnlyList<CustomerAdminItemDto> Items, int TotalCount)> GetCustomerAdminPageAsync(
        CustomerAdminPageQuery query,
        long? branchId,
        string? branchTag,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<Customer?> GetCustomerWithDetailsAsync(long id, bool track = false, CancellationToken ct = default);

    Task<Customer?> GetCustomerForDeleteAsync(long id, CancellationToken ct = default);

    Task<bool> PhoneExistsAsync(string normalizedPhone, long? excludeCustomerId = null, CancellationToken ct = default);

    Task<bool> EmailExistsAsync(string normalizedEmail, long? excludeCustomerId = null, CancellationToken ct = default);

    Task AddCustomerAsync(Customer customer, CancellationToken ct = default);

    Task SaveCustomerAsync(Customer customer, CancellationToken ct = default);

    Task<IReadOnlyList<CustomerVehicleSummaryDto>> GetCustomerVehiclesAsync(long customerId, CancellationToken ct = default);

    Task<IReadOnlyList<Customer>> GetCustomersByBranchIdAsync(long branchId, CancellationToken ct = default);

    Task<(bool Exists, string? Note)> GetActiveCustomerNoteAsync(long customerId, CancellationToken ct = default);

    Task<bool> HasBranchServiceOrderAsync(long customerId, long branchId, CancellationToken ct = default);

    Task<bool> HasBranchAppointmentAsync(long customerId, long branchId, CancellationToken ct = default);
}
