using System;
using System.Collections.Generic;

namespace MyGarage.Application.Customers.DTOs;

public sealed record CustomerAdminPageQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    long? BranchId = null,
    long? TierId = null,
    bool? IsActive = null,
    string? SortBy = null,
    bool SortDescending = false
);

public sealed record CustomerAdminPageResult(
    IReadOnlyList<CustomerAdminItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public sealed record CustomerAdminItemDto(
    long Id,
    string FullName,
    string? Phone,
    string? Email,
    long? TierId,
    string? TierName,
    decimal TotalSpending,
    int NoShowCount,
    bool? IsActive,
    string? Type,
    string? PrimaryPlate,
    string? PrimaryModelName,
    string? Note,
    DateTime? CreatedAt
);

public sealed record CustomerVehicleSummaryDto(
    long Id,
    long VehicleId,
    string LicensePlate,
    string? ModelName,
    string? BrandName,
    bool? IsCurrentOwner,
    DateOnly? OwnershipStartDate
);

public sealed record CustomerDetailDto(
    long Id,
    string FullName,
    string? Phone,
    string? Email,
    long? TierId,
    string? TierName,
    decimal TotalSpending,
    int NoShowCount,
    bool? IsActive,
    string? Type,
    string? Note,
    DateTime? CreatedAt,
    IReadOnlyList<CustomerVehicleSummaryDto> Vehicles
);

public sealed record CustomerCreateCommand(
    string FullName,
    string? Phone,
    string? Email,
    string? Type = null,
    long? TierId = null,
    string? Note = null,
    long? PreferredBranchId = null
);

public sealed record CustomerUpdateCommand(
    string FullName,
    string? Phone,
    string? Email,
    string? Type = null,
    long? TierId = null,
    string? Note = null,
    bool? IsActive = null,
    long? PreferredBranchId = null
);
