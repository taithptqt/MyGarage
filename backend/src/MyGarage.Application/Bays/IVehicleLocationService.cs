using MyGarage.Application.Authorization;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Bays;

public interface IVehicleLocationService
{
    Task<CustomerGarageDto> GetCustomerGarageAsync(long customerId, CancellationToken ct = default);
    Task<TechnicianLocationWorkspaceDto> GetTechnicianWorkspaceAsync(UserActorContext actor, CancellationToken ct = default);
    Task ReportAsync(long serviceOrderId, VehicleLocationReportInput input, UserActorContext actor, CancellationToken ct = default);
    Task ReportRepairAsync(long serviceOrderId, VehicleRepairReportInput input, UserActorContext actor, CancellationToken ct = default);
}

public interface IBayOccupancySnapshotSource
{
    Task<BayOccupancySnapshot> ReadAsync(CancellationToken ct = default);
}
public sealed record BayOccupancySession(long BayId, long VehicleId, DateTime AssignedAt, DateTime? ReleasedAt, string? Note);
public sealed record BayOccupancySnapshot(IReadOnlyList<BayOccupancySession> Sessions, IReadOnlyDictionary<long, int> Capacities);

public sealed record GarageBayDto(long Id, long BranchId, string Code, string Name, string BayType, string ServiceArea);
public sealed record GarageBranchDto(long Id, string Name, IReadOnlyList<GarageBayDto> Bays);
public sealed record GarageVehicleDto(long VehicleId, string LicensePlate, string ModelLabel, long? ServiceOrderId,
    long? BranchId, string? BranchName, long? BayId, string? BayCode, string? BayName, string? BayType, string? ServiceArea,
    DateTime? ReportedAt, string? ReportedBy, string? Note)
{
    public IReadOnlyList<VehicleRepairDto> Repairs { get; init; } = [];
}
public sealed record CustomerGarageDto(IReadOnlyList<GarageBranchDto> Branches, IReadOnlyList<GarageVehicleDto> Vehicles);
public sealed record LocationOrderDto(long Id, long VehicleId, string Code, string LicensePlate, long BranchId, long? BayId, string? BayCode)
{
    public IReadOnlyList<VehicleRepairDto> Repairs { get; init; } = [];
}
public sealed record LocationBayOptionDto(long Id, long BranchId, string Code, string Name, string BayType, string ServiceArea, IReadOnlyList<long> OccupiedVehicleIds, int Capacity);
public sealed record TechnicianLocationWorkspaceDto(IReadOnlyList<LocationOrderDto> Orders, IReadOnlyList<LocationBayOptionDto> Bays);
public sealed record VehicleLocationReportInput(long BayId, long? ExpectedBayId, string? Note);
public sealed record VehicleRepairReportInput(string PartId, string Note, bool Completed, long? ExpectedReportId = null);
public sealed record VehicleRepairDto(string PartId, string Note, string Technician, DateTime? ReportedAt, long ReportId);
