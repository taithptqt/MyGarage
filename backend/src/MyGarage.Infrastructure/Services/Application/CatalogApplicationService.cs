using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Authorization;
using MyGarage.Application.Catalog;
using MyGarage.Application.Catalog.DTOs;
using MyGarage.Common;
using MyGarage.Domain.Rules;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class CatalogApplicationService : ICatalogApplicationService
{
    private readonly IGarageContext _context;
    private readonly ICatalogAuthorizationService _authService;

    public CatalogApplicationService(
        IGarageContext context,
        ICatalogAuthorizationService authService)
    {
        _context = context;
        _authService = authService;
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        try
        {
            return await _context.Database.BeginTransactionAsync();
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("SqlServerRetryingExecutionStrategy"))
            {
                return null;
            }
            throw;
        }
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

        var baseQuery = _context.StandardServices
            .AsNoTracking()
            .Where(s => s.IsDeleted != true);

        if (query.ExcludePackages)
        {
            baseQuery = baseQuery.Where(s => !s.Code.Trim().ToUpper().StartsWith(CatalogDomainRules.PackageCodePrefix));
        }

        if (query.CategoryId.HasValue && query.CategoryId.Value > 0)
        {
            baseQuery = baseQuery.Where(s => s.CategoryId == query.CategoryId.Value);
        }

        if (query.IsActive.HasValue)
        {
            baseQuery = baseQuery.Where(s => s.IsActive == query.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            baseQuery = baseQuery.Where(s => s.Code.Contains(search) || s.Name.Contains(search));
        }

        var totalCount = await baseQuery.CountAsync(ct);

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var items = await baseQuery
            .OrderBy(s => s.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StandardServiceAdminItemDto(
                s.Id,
                s.Code,
                s.Name,
                s.CategoryId,
                s.Category != null ? s.Category.Name : null,
                s.BasePrice,
                s.TaxRate ?? 0m,
                s.ConsumableCost ?? 0m,
                s.StandardMinutes,
                s.WarrantyMonths ?? 0,
                s.WarrantyKm ?? 0,
                s.IsActive,
                s.EngineType,
                s.Description
            ))
            .ToListAsync(ct);

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

        var category = await _context.ServiceCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == command.CategoryId && c.IsDeleted != true, ct);
        if (category == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy danh mục dịch vụ #{command.CategoryId}.");
        }

        var codeExists = await _context.StandardServices
            .AsNoTracking()
            .AnyAsync(s => s.Code == normCode && s.IsDeleted != true, ct);
        if (codeExists)
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

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                _context.StandardServices.Add(service);
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

        return new StandardServiceAdminItemDto(
            service.Id,
            service.Code,
            service.Name,
            service.CategoryId,
            category.Name,
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

        var service = await _context.StandardServices
            .Include(s => s.Category)
            .FirstOrDefaultAsync(s => s.Id == id && s.IsDeleted != true, ct);

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
            var categoryExists = await _context.ServiceCategories
                .AsNoTracking()
                .AnyAsync(c => c.Id == command.CategoryId && c.IsDeleted != true, ct);
            if (!categoryExists)
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

        var now = VietnamTime.Now;
        service.UpdatedAt = now;
        service.UpdatedBy = actor.UserId;

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

        return new StandardServiceAdminItemDto(
            service.Id,
            service.Code,
            service.Name,
            service.CategoryId,
            service.Category?.Name,
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

    public async Task<BrandAdminPageResult> GetBrandAdminPageAsync(
        BrandAdminPageQuery query,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        var baseQuery = _context.VehicleBrands
            .AsNoTracking()
            .Where(b => b.IsDeleted != true);

        if (query.IsActive.HasValue)
        {
            baseQuery = baseQuery.Where(b => b.IsActive == query.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            baseQuery = baseQuery.Where(b => b.Code.Contains(search) || b.Name.Contains(search) || (b.CountryOfOrigin != null && b.CountryOfOrigin.Contains(search)));
        }

        var totalCount = await baseQuery.CountAsync(ct);

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var items = await baseQuery
            .OrderBy(b => b.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new BrandItemDto(
                b.Id,
                b.Code,
                b.Name,
                b.LogoUrl,
                b.CountryOfOrigin,
                b.IsActive
            ))
            .ToListAsync(ct);

        return new BrandAdminPageResult(items, totalCount, page, pageSize);
    }

    public async Task<ModelAdminPageResult> GetModelAdminPageAsync(
        ModelAdminPageQuery query,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        var baseQuery = _context.VehicleModels
            .AsNoTracking()
            .Where(m => m.IsDeleted != true);

        if (query.BrandId.HasValue && query.BrandId.Value > 0)
        {
            baseQuery = baseQuery.Where(m => m.BrandId == query.BrandId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Segment))
        {
            baseQuery = baseQuery.Where(m => m.Segment == query.Segment.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.FuelType))
        {
            baseQuery = baseQuery.Where(m => m.FuelType == query.FuelType.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            baseQuery = baseQuery.Where(m => m.ModelName.Contains(search) || m.Brand.Name.Contains(search));
        }

        var totalCount = await baseQuery.CountAsync(ct);

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var items = await baseQuery
            .OrderBy(m => m.Brand.Name)
            .ThenBy(m => m.ModelName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new ModelItemDto(
                m.Id,
                m.BrandId,
                m.Brand.Name,
                m.ModelName,
                m.Segment,
                m.FuelType,
                m.CylinderCapacity
            ))
            .ToListAsync(ct);

        return new ModelAdminPageResult(items, totalCount, page, pageSize);
    }

    public async Task<CategoryAdminPageResult> GetCategoryAdminPageAsync(
        CategoryAdminPageQuery query,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        var baseQuery = _context.ServiceCategories
            .AsNoTracking()
            .Where(c => c.IsDeleted != true);

        if (query.IsActive.HasValue)
        {
            baseQuery = baseQuery.Where(c => c.IsActive == query.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            baseQuery = baseQuery.Where(c => c.Code.Contains(search) || c.Name.Contains(search));
        }

        var totalCount = await baseQuery.CountAsync(ct);

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var items = await baseQuery
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CategoryItemDto(
                c.Id,
                c.Code,
                c.Name,
                c.ParentCategoryId,
                c.ParentCategory != null ? c.ParentCategory.Name : null,
                c.Description,
                c.IsActive,
                c.DisplayOrder ?? 0
            ))
            .ToListAsync(ct);

        return new CategoryAdminPageResult(items, totalCount, page, pageSize);
    }
}
