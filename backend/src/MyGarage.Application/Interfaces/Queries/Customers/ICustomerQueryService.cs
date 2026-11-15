using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public sealed record CustomerAdminPrimaryVehicleRow(
    long LinkId,
    long VehicleId,
    bool Exists,
    string? Plate,
    string? Color,
    int? Year,
    long ModelId,
    string? ModelName,
    long BrandId,
    string? BrandName);

public sealed record CustomerAdminRow(
    Customer Customer,
    string? TierName,
    CustomerAdminPrimaryVehicleRow? Primary);

public sealed record CustomerAdminPageResult(
    IReadOnlyList<CustomerAdminRow> Rows,
    int Total,
    int TotalCustomers,
    int ActiveCustomers);

public interface ICustomerQueryService
{
    Task<(IReadOnlyList<Customer> Items, int Total)> GetScopedPageAsync(
        long? branchId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<bool> ExistsInScopeAsync(long id, long? branchId, CancellationToken ct = default);

    Task<CustomerAdminPageResult> GetAdminPageAsync(
        long? branchId,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
