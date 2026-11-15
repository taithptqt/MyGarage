using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Catalog;
using MyGarage.Application.Catalog.DTOs;
using MyGarage.Common;
using MyGarage.Domain.Rules;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class CatalogApplicationService : ICatalogApplicationService
{
    private readonly ICatalogStore _store;
    private readonly ICatalogAuthorizationService _authService;

    public CatalogApplicationService(
        ICatalogStore store,
        ICatalogAuthorizationService authService)
    {
        _store = store;
        _authService = authService;
    }

    public async Task<StandardServiceAdminPageResult> GetStandardServiceAdminPageAsync(
        StandardServiceAdminPageQuery query,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanViewCatalogAdminListAsync(actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền truy cập danh sách dịch vụ quản trị.");
        }

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
        var (items, totalCount) = await _store.GetStandardServiceAdminPageAsync(query, page, pageSize, ct);

        return new StandardServiceAdminPageResult(items, totalCount, page, pageSize);
    }

    public async Task<StandardServiceAdminItemDto> CreateStandardServiceAsync(
        StandardServiceCreateCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanManageCatalogAsync(actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền tạo dịch vụ tiêu chuẩn.");
        }

        var (isCodeValid, codeError) = CatalogDomainRules.ValidateStandardServiceCode(command.Code);
        if (!isCodeValid)
        {
            throw new ArgumentException(codeError ?? "Mã dịch vụ không hợp lệ.", nameof(command.Code));
        }

        var normCode = CatalogDomainRules.NormalizeCode(command.Code)!;

        var (isPricingValid, pricingError) = CatalogDomainRules.ValidateServicePricing(
            command.BasePrice, command.TaxRate, command.ConsumableCost, command.StandardMinutes);
        if (!isPricingValid)
        {
            throw new ArgumentException(pricingError ?? "Thông tin giá/thời gian không hợp lệ.", nameof(command.BasePrice));
        }

        var (isWarrantyValid, warrantyError) = CatalogDomainRules.ValidateWarranty(command.WarrantyMonths, command.WarrantyKm);
        if (!isWarrantyValid)
        {
            throw new ArgumentException(warrantyError ?? "Thông tin bảo hành không hợp lệ.", nameof(command.WarrantyMonths));
        }

        if (command.CategoryId <= 0)
        {
            throw new ArgumentException("Danh mục dịch vụ là bắt buộc.", nameof(command.CategoryId));
        }

        var categoryName = await _store.GetActiveCategoryNameAsync(command.CategoryId, ct);
        if (categoryName == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy danh mục dịch vụ #{command.CategoryId}.");
        }

        if (await _store.StandardServiceCodeExistsAsync(normCode, ct))
        {
            throw new InvalidOperationException($"Mã dịch vụ '{normCode}' đã tồn tại trong danh mục tiêu chuẩn.");
        }

        var now = VietnamTime.Now;
        var service = new StandardService
        {
            Code = normCode,
            Name = command.Name.Trim(),
            CategoryId = command.CategoryId,
            BasePrice = CatalogDomainRules.RoundPrice(command.BasePrice),
            TaxRate = command.TaxRate,
            ConsumableCost = CatalogDomainRules.RoundPrice(command.ConsumableCost),
            StandardMinutes = command.StandardMinutes,
            WarrantyMonths = command.WarrantyMonths,
            WarrantyKm = command.WarrantyKm,
            EngineType = command.EngineType?.Trim(),
            Description = command.Description?.Trim(),
            ImageUrl = command.ImageUrl?.Trim(),
            IsActive = command.IsActive,
            CreatedAt = now,
            CreatedBy = actor.UserId,
            UpdatedAt = now,
            UpdatedBy = actor.UserId,
            IsDeleted = false
        };

        await _store.AddStandardServiceAsync(service, ct);

        return MapStandardServiceDto(service, categoryName);
    }

    public async Task<StandardServiceAdminItemDto?> UpdateStandardServiceAsync(
        long id,
        StandardServiceUpdateCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (id <= 0) return null;

        if (!await _authService.CanManageCatalogAsync(actor, ct))
        {
            throw new UnauthorizedAccessException($"Không có quyền cập nhật dịch vụ tiêu chuẩn #{id}.");
        }

        var service = await _store.GetStandardServiceByIdAsync(id, track: true, ct);
        if (service == null) return null;

        var (isPricingValid, pricingError) = CatalogDomainRules.ValidateServicePricing(
            command.BasePrice, command.TaxRate, command.ConsumableCost, command.StandardMinutes);
        if (!isPricingValid)
        {
            throw new ArgumentException(pricingError ?? "Thông tin giá/thời gian không hợp lệ.", nameof(command.BasePrice));
        }

        var (isWarrantyValid, warrantyError) = CatalogDomainRules.ValidateWarranty(command.WarrantyMonths, command.WarrantyKm);
        if (!isWarrantyValid)
        {
            throw new ArgumentException(warrantyError ?? "Thông tin bảo hành không hợp lệ.", nameof(command.WarrantyMonths));
        }

        if (command.CategoryId > 0 && command.CategoryId != service.CategoryId)
        {
            if (!await _store.ActiveCategoryExistsAsync(command.CategoryId, ct))
            {
                throw new KeyNotFoundException($"Không tìm thấy danh mục dịch vụ #{command.CategoryId}.");
            }
            service.CategoryId = command.CategoryId;
        }

        service.Name = command.Name.Trim();
        service.BasePrice = CatalogDomainRules.RoundPrice(command.BasePrice);
        service.TaxRate = command.TaxRate;
        service.ConsumableCost = CatalogDomainRules.RoundPrice(command.ConsumableCost);
        service.StandardMinutes = command.StandardMinutes;
        service.WarrantyMonths = command.WarrantyMonths;
        service.WarrantyKm = command.WarrantyKm;
        service.EngineType = command.EngineType?.Trim();
        service.Description = command.Description?.Trim();
        service.ImageUrl = command.ImageUrl?.Trim();

        if (command.IsActive.HasValue)
        {
            service.IsActive = command.IsActive.Value;
        }

        service.UpdatedAt = VietnamTime.Now;
        service.UpdatedBy = actor.UserId;

        await _store.SaveStandardServiceAsync(service, ct);

        return MapStandardServiceDto(service, service.Category?.Name);
    }

    public async Task<BrandAdminPageResult> GetBrandAdminPageAsync(
        BrandAdminPageQuery query,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
        var (items, totalCount) = await _store.GetBrandAdminPageAsync(query, page, pageSize, ct);

        return new BrandAdminPageResult(items, totalCount, page, pageSize);
    }

    public async Task<ModelAdminPageResult> GetModelAdminPageAsync(
        ModelAdminPageQuery query,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
        var (items, totalCount) = await _store.GetModelAdminPageAsync(query, page, pageSize, ct);

        return new ModelAdminPageResult(items, totalCount, page, pageSize);
    }

    public async Task<CategoryAdminPageResult> GetCategoryAdminPageAsync(
        CategoryAdminPageQuery query,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
        var (items, totalCount) = await _store.GetCategoryAdminPageAsync(query, page, pageSize, ct);

        return new CategoryAdminPageResult(items, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<ServiceBomResponseDto>> GetServiceBomsAsync(
        long serviceId,
        long? vehicleModelId,
        CancellationToken ct = default)
    {
        if (serviceId <= 0 || !vehicleModelId.HasValue || vehicleModelId.Value <= 0)
        {
            return Array.Empty<ServiceBomResponseDto>();
        }

        return await _store.GetServiceBomsAsync(serviceId, vehicleModelId.Value, ct);
    }

    private static StandardServiceAdminItemDto MapStandardServiceDto(StandardService service, string? categoryName)
        => new(
            service.Id,
            service.Code,
            service.Name,
            service.CategoryId,
            categoryName,
            service.BasePrice,
            service.TaxRate ?? 0m,
            service.ConsumableCost ?? 0m,
            service.StandardMinutes,
            service.WarrantyMonths ?? 0,
            service.WarrantyKm ?? 0,
            service.IsActive,
            service.EngineType,
            service.Description
        );
}
