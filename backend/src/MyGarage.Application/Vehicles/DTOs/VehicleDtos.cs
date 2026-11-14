using System;
using System.Collections.Generic;

namespace MyGarage.Application.Vehicles.DTOs;

public sealed record VehicleAdminPageQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    long? BrandId = null,
    long? ModelId = null,
    long? BranchId = null,
    string? SortBy = null,
    bool SortDescending = false
);

public sealed record VehicleAdminPageResult(
    IReadOnlyList<VehicleAdminItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public sealed record VehicleAdminItemDto(
    long Id,
    string LicensePlate,
    string? Vin,
    string? BrandName,
    string? ModelName,
    string? Color,
    int Odo,
    int? ManufactureYear,
    long? CurrentOwnerId,
    string? CurrentOwnerName,
    string? CurrentOwnerPhone,
    bool? IsDeleted,
    DateTime? CreatedAt
);

public sealed record VehicleDetailDto(
    long Id,
    string LicensePlate,
    string? Vin,
    long? ModelId,
    string? ModelName,
    long? BrandId,
    string? BrandName,
    string? Color,
    int Odo,
    int? ManufactureYear,
    long? CurrentOwnerId,
    string? CurrentOwnerName,
    string? CurrentOwnerPhone,
    DateOnly? OwnershipStartDate,
    DateTime? CreatedAt
);

public sealed record VehicleUpsertCommand(
    string? LicensePlate,
    string? Vin,
    long? ModelId,
    string? Color,
    int? Odo,
    int? ManufactureYear,
    long? CustomerId,
    string? Note
);

public sealed record VehicleOwnerChangeCommand(
    long VehicleId,
    long? CustomerId,
    string? Note = null
);
