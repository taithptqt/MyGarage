using System;
using System.Collections.Generic;

namespace MyGarage.Application.Vouchers.DTOs;

public sealed record VoucherDto(
    long Id,
    string Code,
    string VoucherName,
    string? Description,
    string Type,
    decimal Value,
    decimal? MaxDiscountAmount,
    decimal? MinOrderValue,
    DateTime StartDate,
    DateTime EndDate,
    int? UsageLimit,
    int? UsedCount,
    int MaxUsesPerCustomer,
    int MaxUsesPerVehicle,
    bool FirstUseOnly,
    decimal? BudgetLimit,
    decimal BudgetUsed,
    string Status,
    bool? IsActive,
    bool? IsDeleted,
    DateTime? CreatedAt
);

public sealed record VoucherAdminListFilterInput(
    string? Search = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20,
    bool IncludeDeleted = true
);

public sealed record VoucherAdminListResultDto(
    List<VoucherDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages,
    List<string> Statuses
);

public sealed record VoucherCreateInput(
    string Code,
    string VoucherName,
    string? Description,
    string Type,
    decimal Value,
    decimal? MaxDiscountAmount,
    decimal? MinOrderValue,
    DateTime StartDate,
    DateTime EndDate,
    int? UsageLimit = null,
    int MaxUsesPerCustomer = 1,
    int MaxUsesPerVehicle = 1,
    bool FirstUseOnly = false,
    decimal? BudgetLimit = null
);

public sealed record VoucherUpdateInput(
    string VoucherName,
    string? Description,
    string Type,
    decimal Value,
    decimal? MaxDiscountAmount,
    decimal? MinOrderValue,
    DateTime StartDate,
    DateTime EndDate,
    int? UsageLimit,
    int MaxUsesPerCustomer,
    int MaxUsesPerVehicle,
    bool FirstUseOnly,
    decimal? BudgetLimit
);

public sealed record VoucherValidationInput(
    long? VoucherId,
    string? VoucherCode,
    long BranchId,
    long CustomerId,
    long? VehicleId,
    decimal EligibleAmount,
    long? RetailOrderId = null
);

public sealed record VoucherValidationResultDto(
    bool IsEligible,
    string? ReasonCode,
    string? Message,
    long? VoucherId,
    string? VoucherCode,
    decimal DiscountAmount,
    decimal EligibleAmount
);
