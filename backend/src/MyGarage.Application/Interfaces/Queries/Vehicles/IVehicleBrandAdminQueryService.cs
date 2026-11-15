using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public sealed record VehicleBrandAdminPageData(
    IReadOnlyList<VehicleBrand> Rows,
    int Total,
    int TotalBrands,
    int ActiveBrands,
    int InactiveBrands);

public interface IVehicleBrandAdminQueryService
{
    Task<VehicleBrandAdminPageData> GetAdminPageAsync(string? search, int currentPage, int pageSize, CancellationToken ct = default);
}
