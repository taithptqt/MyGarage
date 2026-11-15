using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Catalog;
using MyGarage.Application.Catalog.DTOs;
using MyGarage.Domain.Rules;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Catalog;

public sealed class EfCatalogStore : ICatalogStore
{
    private readonly IGarageContext _context;

    public EfCatalogStore(IGarageContext context)
    {
        _context = context;
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

    private async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                await action(ct);
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task<(IReadOnlyList<StandardServiceAdminItemDto> Items, int TotalCount)> GetStandardServiceAdminPageAsync(
        StandardServiceAdminPageQuery query,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
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

        return (items, totalCount);
    }

    public async Task<string?> GetActiveCategoryNameAsync(long categoryId, CancellationToken ct = default)
    {
        return await _context.ServiceCategories
            .AsNoTracking()
            .Where(c => c.Id == categoryId && c.IsDeleted != true)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ActiveCategoryExistsAsync(long categoryId, CancellationToken ct = default)
    {
        return await _context.ServiceCategories
            .AsNoTracking()
            .AnyAsync(c => c.Id == categoryId && c.IsDeleted != true, ct);
    }

    public async Task<bool> StandardServiceCodeExistsAsync(string normalizedCode, CancellationToken ct = default)
    {
        return await _context.StandardServices
            .AsNoTracking()
            .AnyAsync(s => s.Code == normalizedCode && s.IsDeleted != true, ct);
    }

    public async Task<StandardService?> GetStandardServiceByIdAsync(long id, bool track = false, CancellationToken ct = default)
    {
        var query = _context.StandardServices
            .Include(s => s.Category)
            .AsQueryable();

        if (!track)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(s => s.Id == id && s.IsDeleted != true, ct);
    }

    public Task AddStandardServiceAsync(StandardService service, CancellationToken ct = default)
    {
        return ExecuteInTransactionAsync(_ =>
        {
            _context.StandardServices.Add(service);
            return Task.CompletedTask;
        }, ct);
    }

    public Task SaveStandardServiceAsync(StandardService service, CancellationToken ct = default)
    {
        return ExecuteInTransactionAsync(_ => Task.CompletedTask, ct);
    }

    public async Task<(IReadOnlyList<BrandItemDto> Items, int TotalCount)> GetBrandAdminPageAsync(
        BrandAdminPageQuery query,
        int page,
        int pageSize,
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

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<ModelItemDto> Items, int TotalCount)> GetModelAdminPageAsync(
        ModelAdminPageQuery query,
        int page,
        int pageSize,
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

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<CategoryItemDto> Items, int TotalCount)> GetCategoryAdminPageAsync(
        CategoryAdminPageQuery query,
        int page,
        int pageSize,
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

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<ServiceBomResponseDto>> GetServiceBomsAsync(
        long serviceId,
        long vehicleModelId,
        CancellationToken ct = default)
    {
        return await _context.ServiceBoms
            .AsNoTracking()
            .Where(x => x.ServiceId == serviceId && x.VehicleModelId == vehicleModelId)
            .Select(x => new ServiceBomResponseDto
            {
                Id = x.Id,
                ServiceId = x.ServiceId,
                VehicleModelId = x.VehicleModelId,
                ProductId = x.ProductId,
                ProductName = x.Product.Name,
                Quantity = x.Quantity,
                Unit = x.Unit,
                IsMandatory = x.IsMandatory,
            })
            .ToListAsync(ct);
    }
}
