using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Vehicles;
using MyGarage.Application.Vehicles.DTOs;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Vehicles;

public sealed class EfVehicleStore : IVehicleStore
{
    private readonly IGarageContext _context;

    public EfVehicleStore(IGarageContext context)
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

    public async Task<(IReadOnlyList<VehicleAdminItemDto> Items, int TotalCount)> GetVehicleAdminPageAsync(
        VehicleAdminPageQuery query,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var baseQuery = _context.Vehicles
            .AsNoTracking()
            .Where(v => v.IsDeleted != true);

        if (query.BrandId.HasValue && query.BrandId.Value > 0)
        {
            baseQuery = baseQuery.Where(v => v.Model.BrandId == query.BrandId.Value);
        }

        if (query.ModelId.HasValue && query.ModelId.Value > 0)
        {
            baseQuery = baseQuery.Where(v => v.ModelId == query.ModelId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            baseQuery = baseQuery.Where(v =>
                v.LicensePlate.Contains(search) ||
                (v.Vin != null && v.Vin.Contains(search)) ||
                (v.Color != null && v.Color.Contains(search)) ||
                v.Model.ModelName.Contains(search) ||
                v.Model.Brand.Name.Contains(search) ||
                v.CustomerVehicles.Any(cv => cv.IsDeleted != true && cv.IsCurrentOwner == true &&
                    (cv.Customer.FullName.Contains(search) || (cv.Customer.Phone != null && cv.Customer.Phone.Contains(search)))));
        }

        var totalCount = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderByDescending(v => v.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new VehicleAdminItemDto(
                v.Id,
                v.LicensePlate,
                v.Vin,
                v.Model.Brand.Name,
                v.Model.ModelName,
                v.Color,
                v.Odo ?? 0,
                v.ManufactureYear,
                v.CustomerVehicles
                    .Where(cv => cv.IsDeleted != true && cv.IsCurrentOwner == true)
                    .Select(cv => (long?)cv.CustomerId)
                    .FirstOrDefault(),
                v.CustomerVehicles
                    .Where(cv => cv.IsDeleted != true && cv.IsCurrentOwner == true)
                    .Select(cv => cv.Customer.FullName)
                    .FirstOrDefault(),
                v.CustomerVehicles
                    .Where(cv => cv.IsDeleted != true && cv.IsCurrentOwner == true)
                    .Select(cv => cv.Customer.Phone)
                    .FirstOrDefault(),
                v.IsDeleted,
                v.CreatedAt
            ))
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<Vehicle?> GetVehicleDetailByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.Vehicles
            .AsNoTracking()
            .Include(v => v.Model)
                .ThenInclude(m => m.Brand)
            .Include(v => v.CustomerVehicles.Where(cv => cv.IsDeleted != true && cv.IsCurrentOwner == true))
                .ThenInclude(cv => cv.Customer)
            .FirstOrDefaultAsync(v => v.Id == id && v.IsDeleted != true, ct);
    }

    public async Task<Vehicle?> GetVehicleForUpdateAsync(long id, CancellationToken ct = default)
    {
        return await _context.Vehicles
            .Include(v => v.Model)
                .ThenInclude(m => m.Brand)
            .Include(v => v.CustomerVehicles.Where(cv => cv.IsDeleted != true))
                .ThenInclude(cv => cv.Customer)
            .FirstOrDefaultAsync(v => v.Id == id && v.IsDeleted != true, ct);
    }

    public async Task<Vehicle?> GetVehicleForOwnerChangeAsync(long id, CancellationToken ct = default)
    {
        return await _context.Vehicles
            .Include(v => v.CustomerVehicles.Where(cv => cv.IsDeleted != true))
            .FirstOrDefaultAsync(v => v.Id == id && v.IsDeleted != true, ct);
    }

    public async Task<Vehicle?> GetVehicleForHideAsync(long id, CancellationToken ct = default)
    {
        return await _context.Vehicles
            .FirstOrDefaultAsync(v => v.Id == id && v.IsDeleted != true, ct);
    }

    public async Task<bool> PlateExistsAsync(string plate, long? excludeVehicleId = null, CancellationToken ct = default)
    {
        var query = _context.Vehicles.AsNoTracking().Where(v => v.LicensePlate == plate && v.IsDeleted != true);
        if (excludeVehicleId.HasValue)
        {
            query = query.Where(v => v.Id != excludeVehicleId.Value);
        }
        return await query.AnyAsync(ct);
    }

    public async Task<bool> VinExistsAsync(string vin, long? excludeVehicleId = null, CancellationToken ct = default)
    {
        var query = _context.Vehicles.AsNoTracking().Where(v => v.Vin == vin && v.IsDeleted != true);
        if (excludeVehicleId.HasValue)
        {
            query = query.Where(v => v.Id != excludeVehicleId.Value);
        }
        return await query.AnyAsync(ct);
    }

    public async Task<VehicleModel?> GetActiveModelWithBrandAsync(long modelId, CancellationToken ct = default)
    {
        return await _context.VehicleModels
            .Include(m => m.Brand)
            .FirstOrDefaultAsync(m => m.Id == modelId && m.IsDeleted != true, ct);
    }

    public async Task<bool> ActiveModelExistsAsync(long modelId, CancellationToken ct = default)
    {
        return await _context.VehicleModels
            .AsNoTracking()
            .AnyAsync(m => m.Id == modelId && m.IsDeleted != true, ct);
    }

    public async Task<bool> ActiveCustomerExistsAsync(long customerId, CancellationToken ct = default)
    {
        return await _context.Customers
            .AsNoTracking()
            .AnyAsync(c => c.Id == customerId && c.IsDeleted != true, ct);
    }

    public Task CreateVehicleAsync(Vehicle vehicle, CustomerVehicle? ownerLink, CancellationToken ct = default)
    {
        return ExecuteInTransactionAsync(_ =>
        {
            _context.Vehicles.Add(vehicle);
            if (ownerLink != null)
            {
                _context.CustomerVehicles.Add(ownerLink);
            }
            return Task.CompletedTask;
        }, ct);
    }

    public Task SaveVehicleWithNewOwnerLinksAsync(Vehicle vehicle, IReadOnlyList<CustomerVehicle> newLinks, CancellationToken ct = default)
    {
        return ExecuteInTransactionAsync(_ =>
        {
            foreach (var link in newLinks)
            {
                _context.CustomerVehicles.Add(link);
            }
            return Task.CompletedTask;
        }, ct);
    }
}
