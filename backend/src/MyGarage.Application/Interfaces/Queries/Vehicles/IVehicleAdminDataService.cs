using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public sealed record VehicleAdminBranchLookup(long Id, string Name, string? Status);
public sealed record VehicleAdminBrandLookup(long Id, string? Code, string Name);
public sealed record VehicleAdminModelLookup(long Id, long BrandId, string? BrandName, string ModelName, string? Segment);
public sealed record VehicleAdminBayLookup(
    long Id,
    string Code,
    string Name,
    long BranchId,
    string? BranchName,
    string? Status,
    bool IsLocked);
public sealed record VehicleAdminStaffLookup(
    long Id,
    string FullName,
    long? BranchId,
    string? BranchName,
    string? RoleName,
    string? Status);

public sealed class VehicleAdminServiceOrderLite
{
    public long VehicleId { get; set; }
    public long ExecuteBranchId { get; set; }
    public string? Status { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public sealed record VehicleAdminRowSourceData(
    IReadOnlyList<Vehicle> Vehicles,
    IReadOnlyList<CustomerVehicle> OwnerLinks,
    IReadOnlyList<VehicleAdminServiceOrderLite> Orders,
    IReadOnlyList<WorkSlot> WorkSlots,
    IReadOnlyList<Bay> FallbackOccupiedBays,
    IReadOnlyList<Bay> BaysByIds);

public interface IVehicleAdminDataService :
    IVehicleAdminLookupQueryService,
    IVehicleAdminBayStaffQueryService,
    IVehicleAdminEntityDataService
{
}
