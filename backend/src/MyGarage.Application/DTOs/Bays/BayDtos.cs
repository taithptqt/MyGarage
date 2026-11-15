using System;
using System.Collections.Generic;

namespace MyGarage.Application.Bays.DTOs;

public sealed record BayDetailDto(
    long Id,
    long BranchId,
    string Code,
    string Name,
    string? BayType,
    string? ServiceArea,
    decimal? MaxTonnage,
    string OperationalStatus,
    long? CurrentOccupiedVehicleId,
    string? CurrentVehicleLicensePlate,
    int? ReservationStatus,
    bool? IsActive
);

public sealed record BayAllocationResultDto(
    long Id,
    long BayId,
    string BayName,
    long ServiceOrderId,
    string ServiceOrderCode,
    long VehicleId,
    string LicensePlate,
    DateTime AllocatedAt,
    long? AllocatedBy,
    string? AllocatedByName,
    DateTime? ReleasedAt,
    long? ReleasedBy,
    string? ReleasedByName,
    int? PlannedDuration,
    int ActualDuration,
    string? Notes,
    DateTime? CreatedAt
);

public sealed record BayTimelineAllocationDto(
    long Id,
    long BayId,
    long ServiceOrderId,
    string ServiceOrderCode,
    long VehicleId,
    string LicensePlate,
    DateTime AllocatedAt,
    DateTime? ReleasedAt,
    int? PlannedDuration,
    int ActualDuration
);

public sealed record BayTimelineItemDto(
    long BayId,
    string BayName,
    string ServiceArea,
    string OperationalStatus,
    IReadOnlyList<BayTimelineAllocationDto> Allocations
);

public sealed record BayCreateInput(
    long BranchId,
    string Code,
    string Name,
    string? BayType,
    string? ServiceArea,
    decimal? MaxTonnage
);

public sealed record BayUpdateInput(
    string? Name,
    string? BayType,
    string? ServiceArea,
    decimal? MaxTonnage,
    string? OperationalStatus,
    bool? IsActive
);

public sealed record BayAllocationInput(
    long BayId,
    long ServiceOrderId,
    long VehicleId,
    int? PlannedDuration,
    string? Notes
);
