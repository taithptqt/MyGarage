using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public sealed record BayBranchLookupItem(long Id, string Name, string? Status);

public sealed record BayVehicleLookupItem(
    long Id,
    string LicensePlate,
    string? ModelName,
    string? BrandName,
    string? Segment);

public sealed class BayVehicleDescriptor
{
    public long Id { get; set; }
    public string LicensePlate { get; set; } = "";
    public string? ModelName { get; set; }
    public string? BrandName { get; set; }
    public string? Segment { get; set; }
}

public interface IBayAdminDataService : IBayAdminReadDataService, IBayAdminWriteDataService
{
}
