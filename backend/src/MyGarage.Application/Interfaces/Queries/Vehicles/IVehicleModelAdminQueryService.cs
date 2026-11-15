using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public sealed record VehicleModelAdminPageData(
    IReadOnlyList<VehicleModel> Rows,
    int Total,
    int TotalModels,
    int BrandCount,
    int ActiveModels);

public interface IVehicleModelAdminQueryService
{
    Task<VehicleModelAdminPageData> GetAdminPageAsync(string? search, int currentPage, int pageSize, CancellationToken ct = default);
}
