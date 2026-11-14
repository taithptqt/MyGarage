using System.Collections.Generic;

namespace MyGarage.Application.Catalog.DTOs;

public sealed record StandardServiceAdminPageQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    long? CategoryId = null,
    bool? IsActive = null,
    bool ExcludePackages = true
);

public sealed record StandardServiceAdminPageResult(
    IReadOnlyList<StandardServiceAdminItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public sealed record StandardServiceAdminItemDto(
    long Id,
    string Code,
    string Name,
    long CategoryId,
    string? CategoryName,
    decimal BasePrice,
    decimal TaxRate,
    decimal ConsumableCost,
    int StandardMinutes,
    int WarrantyMonths,
    int WarrantyKm,
    bool? IsActive,
    string? EngineType,
    string? Description
);

public sealed record StandardServiceCreateCommand(
    string Code,
    string Name,
    long CategoryId,
    decimal BasePrice,
    decimal TaxRate = 0m,
    decimal ConsumableCost = 0m,
    int StandardMinutes = 0,
    int WarrantyMonths = 0,
    int WarrantyKm = 0,
    string? EngineType = null,
    string? Description = null,
    string? ImageUrl = null,
    bool IsActive = true
);

public sealed record StandardServiceUpdateCommand(
    string Name,
    long CategoryId,
    decimal BasePrice,
    decimal TaxRate = 0m,
    decimal ConsumableCost = 0m,
    int StandardMinutes = 0,
    int WarrantyMonths = 0,
    int WarrantyKm = 0,
    string? EngineType = null,
    string? Description = null,
    string? ImageUrl = null,
    bool? IsActive = null
);

public sealed record BrandAdminPageQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    bool? IsActive = null
);

public sealed record BrandAdminPageResult(
    IReadOnlyList<BrandItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public sealed record BrandItemDto(
    long Id,
    string Code,
    string Name,
    string? LogoUrl,
    string? CountryOfOrigin,
    bool? IsActive
);

public sealed record ModelAdminPageQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    long? BrandId = null,
    string? Segment = null,
    string? FuelType = null
);

public sealed record ModelAdminPageResult(
    IReadOnlyList<ModelItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public sealed record ModelItemDto(
    long Id,
    long BrandId,
    string? BrandName,
    string ModelName,
    string? Segment,
    string? FuelType,
    string? CylinderCapacity
);

public sealed record CategoryAdminPageQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    bool? IsActive = null
);

public sealed record CategoryAdminPageResult(
    IReadOnlyList<CategoryItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public sealed record CategoryItemDto(
    long Id,
    string Code,
    string Name,
    long? ParentCategoryId,
    string? ParentCategoryName,
    string? Description,
    bool? IsActive,
    int DisplayOrder
);
