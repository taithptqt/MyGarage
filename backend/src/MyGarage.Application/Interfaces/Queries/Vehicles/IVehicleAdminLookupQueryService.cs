namespace MyGarage.Application.Interfaces.Queries;

public interface IVehicleAdminLookupQueryService
{
    Task<IReadOnlyList<VehicleAdminBranchLookup>> GetLookupBranchesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<VehicleAdminBrandLookup>> GetLookupBrandsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<VehicleAdminModelLookup>> GetLookupModelsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<VehicleAdminBayLookup>> GetLookupBaysAsync(CancellationToken ct = default);
    Task<IReadOnlyList<VehicleAdminStaffLookup>> GetLookupStaffsAsync(CancellationToken ct = default);

    Task<VehicleAdminRowSourceData> GetVehicleRowSourceDataAsync(
        bool includeHidden,
        IReadOnlyCollection<long> activeSessionBayIds,
        CancellationToken ct = default);

    Task<Dictionary<long, MyGarage.Models.Staff>> GetStaffMapAsync(IReadOnlyCollection<long> staffIds, CancellationToken ct = default);
    Task<Dictionary<long, MyGarage.Models.Branch>> GetBranchMapAsync(IReadOnlyCollection<long> branchIds, CancellationToken ct = default);
}
