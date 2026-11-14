using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Authorization;
using MyGarage.Application.Vehicles;
using MyGarage.Application.Vehicles.DTOs;
using MyGarage.Common;
using MyGarage.Domain.Rules;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class VehicleApplicationService : IVehicleApplicationService
{
    private readonly IGarageContext _context;
    private readonly IVehicleAuthorizationService _authService;

    public VehicleApplicationService(
        IGarageContext context,
        IVehicleAuthorizationService authService)
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

    public async Task<VehicleAdminPageResult> GetVehicleAdminPageAsync(
        VehicleAdminPageQuery query,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanAdministerVehiclesAsync(actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền truy cập danh sách xe quản trị.");
        }

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

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

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

        return new VehicleAdminPageResult(items, totalCount, page, pageSize);
    }

    public async Task<VehicleDetailDto?> GetVehicleDetailAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (id <= 0) return null;

        if (!await _authService.CanAccessVehicleAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException($"Không có quyền truy cập thông tin xe #{id}.");
        }

        var vehicle = await _context.Vehicles
            .AsNoTracking()
            .Include(v => v.Model)
                .ThenInclude(m => m.Brand)
            .Include(v => v.CustomerVehicles.Where(cv => cv.IsDeleted != true && cv.IsCurrentOwner == true))
                .ThenInclude(cv => cv.Customer)
            .FirstOrDefaultAsync(v => v.Id == id && v.IsDeleted != true, ct);

        if (vehicle == null) return null;

        var currentOwnerLink = vehicle.CustomerVehicles.FirstOrDefault();

        return new VehicleDetailDto(
            vehicle.Id,
            vehicle.LicensePlate,
            vehicle.Vin,
            vehicle.ModelId,
            vehicle.Model?.ModelName,
            vehicle.Model?.BrandId,
            vehicle.Model?.Brand?.Name,
            vehicle.Color,
            vehicle.Odo ?? 0,
            vehicle.ManufactureYear,
            currentOwnerLink?.CustomerId,
            currentOwnerLink?.Customer?.FullName,
            currentOwnerLink?.Customer?.Phone,
            currentOwnerLink?.OwnershipStartDate,
            vehicle.CreatedAt
        );
    }

    public async Task<VehicleDetailDto> CreateVehicleAsync(
        VehicleUpsertCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanAdministerVehiclesAsync(actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền tạo mới xe.");
        }

        var plate = VehicleDomainRules.NormalizePlate(command.LicensePlate);
        if (string.IsNullOrEmpty(plate))
        {
            plate = VehicleDomainRules.GenerateTemporaryPlate();
        }
        else if (!VehicleDomainRules.IsValidPlate(plate))
        {
            throw new ArgumentException("Biển số xe không đúng định dạng.", nameof(command.LicensePlate));
        }

        // Duplicate plate check
        var plateExists = await _context.Vehicles
            .AsNoTracking()
            .AnyAsync(v => v.LicensePlate == plate && v.IsDeleted != true, ct);
        if (plateExists)
        {
            throw new InvalidOperationException($"Biển số xe '{plate}' đã tồn tại trong hệ thống.");
        }

        var vin = VehicleDomainRules.NormalizeVin(command.Vin);
        if (!string.IsNullOrEmpty(vin))
        {
            if (!VehicleDomainRules.IsValidVin(vin))
            {
                throw new ArgumentException("Số VIN không đúng định dạng (17 ký tự, không chứa I, O, Q).", nameof(command.Vin));
            }

            var vinExists = await _context.Vehicles
                .AsNoTracking()
                .AnyAsync(v => v.Vin == vin && v.IsDeleted != true, ct);
            if (vinExists)
            {
                throw new InvalidOperationException($"Số VIN '{vin}' đã tồn tại trong hệ thống.");
            }
        }

        if (!command.ModelId.HasValue || command.ModelId.Value <= 0)
        {
            throw new ArgumentException("Vui lòng chọn kiểu xe (Model).", nameof(command.ModelId));
        }

        var model = await _context.VehicleModels
            .Include(m => m.Brand)
            .FirstOrDefaultAsync(m => m.Id == command.ModelId.Value && m.IsDeleted != true, ct);
        if (model == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy kiểu xe #{command.ModelId.Value}.");
        }

        if (!VehicleDomainRules.ValidateManufactureYear(command.ManufactureYear))
        {
            throw new ArgumentException("Năm sản xuất không hợp lệ.", nameof(command.ManufactureYear));
        }

        if (!VehicleDomainRules.ValidateOdo(command.Odo))
        {
            throw new ArgumentException("Số ODO không hợp lệ.", nameof(command.Odo));
        }

        var now = VietnamTime.Now;
        var today = DateOnly.FromDateTime(now);

        var vehicle = new Vehicle
        {
            LicensePlate = plate,
            Vin = vin,
            ModelId = command.ModelId.Value,
            Color = command.Color?.Trim(),
            Odo = command.Odo ?? 0,
            OdoLastUpdatedAt = command.Odo.HasValue ? now : null,
            ManufactureYear = command.ManufactureYear,
            CreatedAt = now,
            CreatedBy = actor.UserId,
            UpdatedAt = now,
            UpdatedBy = actor.UserId,
            IsDeleted = false
        };

        CustomerVehicle? ownerLink = null;
        if (command.CustomerId.HasValue && command.CustomerId.Value > 0)
        {
            var customerExists = await _context.Customers
                .AsNoTracking()
                .AnyAsync(c => c.Id == command.CustomerId.Value && c.IsDeleted != true, ct);
            if (!customerExists)
            {
                throw new KeyNotFoundException($"Không tìm thấy khách hàng #{command.CustomerId.Value}.");
            }

            ownerLink = new CustomerVehicle
            {
                CustomerId = command.CustomerId.Value,
                Vehicle = vehicle,
                IsCurrentOwner = true,
                OwnershipStartDate = today,
                Note = command.Note?.Trim(),
                CreatedAt = now,
                CreatedBy = actor.UserId,
                UpdatedAt = now,
                UpdatedBy = actor.UserId,
                IsDeleted = false
            };
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                _context.Vehicles.Add(vehicle);
                if (ownerLink != null)
                {
                    _context.CustomerVehicles.Add(ownerLink);
                }
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

        return new VehicleDetailDto(
            vehicle.Id,
            vehicle.LicensePlate,
            vehicle.Vin,
            vehicle.ModelId,
            model.ModelName,
            model.BrandId,
            model.Brand?.Name,
            vehicle.Color,
            vehicle.Odo ?? 0,
            vehicle.ManufactureYear,
            ownerLink?.CustomerId,
            null,
            null,
            ownerLink?.OwnershipStartDate,
            vehicle.CreatedAt
        );
    }

    public async Task<VehicleDetailDto?> UpdateVehicleAsync(
        long id,
        VehicleUpsertCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (id <= 0) return null;

        if (!await _authService.CanModifyVehicleAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException($"Không có quyền chỉnh sửa thông tin xe #{id}.");
        }

        var vehicle = await _context.Vehicles
            .Include(v => v.Model)
                .ThenInclude(m => m.Brand)
            .Include(v => v.CustomerVehicles.Where(cv => cv.IsDeleted != true))
                .ThenInclude(cv => cv.Customer)
            .FirstOrDefaultAsync(v => v.Id == id && v.IsDeleted != true, ct);

        if (vehicle == null) return null;

        var plate = VehicleDomainRules.NormalizePlate(command.LicensePlate);
        if (!string.IsNullOrEmpty(plate))
        {
            if (!VehicleDomainRules.IsValidPlate(plate))
            {
                throw new ArgumentException("Biển số xe không đúng định dạng.", nameof(command.LicensePlate));
            }

            var plateExists = await _context.Vehicles
                .AsNoTracking()
                .AnyAsync(v => v.LicensePlate == plate && v.Id != id && v.IsDeleted != true, ct);
            if (plateExists)
            {
                throw new InvalidOperationException($"Biển số xe '{plate}' đã được sử dụng bởi xe khác.");
            }
            vehicle.LicensePlate = plate;
        }

        var vin = VehicleDomainRules.NormalizeVin(command.Vin);
        if (!string.IsNullOrEmpty(vin))
        {
            if (!VehicleDomainRules.IsValidVin(vin))
            {
                throw new ArgumentException("Số VIN không đúng định dạng.", nameof(command.Vin));
            }

            var vinExists = await _context.Vehicles
                .AsNoTracking()
                .AnyAsync(v => v.Vin == vin && v.Id != id && v.IsDeleted != true, ct);
            if (vinExists)
            {
                throw new InvalidOperationException($"Số VIN '{vin}' đã được sử dụng bởi xe khác.");
            }
            vehicle.Vin = vin;
        }

        if (command.ModelId.HasValue && command.ModelId.Value > 0 && command.ModelId.Value != vehicle.ModelId)
        {
            var modelExists = await _context.VehicleModels
                .AsNoTracking()
                .AnyAsync(m => m.Id == command.ModelId.Value && m.IsDeleted != true, ct);
            if (!modelExists)
            {
                throw new KeyNotFoundException($"Không tìm thấy kiểu xe #{command.ModelId.Value}.");
            }
            vehicle.ModelId = command.ModelId.Value;
        }

        if (command.Odo.HasValue)
        {
            if (!VehicleDomainRules.ValidateOdo(command.Odo.Value, vehicle.Odo))
            {
                throw new ArgumentException("Số ODO không hợp lệ hoặc nhỏ hơn ODO hiện tại.", nameof(command.Odo));
            }
            vehicle.Odo = command.Odo.Value;
            vehicle.OdoLastUpdatedAt = VietnamTime.Now;
        }

        if (command.ManufactureYear.HasValue)
        {
            if (!VehicleDomainRules.ValidateManufactureYear(command.ManufactureYear.Value))
            {
                throw new ArgumentException("Năm sản xuất không hợp lệ.", nameof(command.ManufactureYear));
            }
            vehicle.ManufactureYear = command.ManufactureYear.Value;
        }

        if (!string.IsNullOrWhiteSpace(command.Color))
        {
            vehicle.Color = command.Color.Trim();
        }

        var now = VietnamTime.Now;
        var today = DateOnly.FromDateTime(now);
        vehicle.UpdatedAt = now;
        vehicle.UpdatedBy = actor.UserId;

        // Check ownership transition if CustomerId provided
        if (command.CustomerId.HasValue)
        {
            var currentOwnerLink = vehicle.CustomerVehicles.FirstOrDefault(cv => cv.IsCurrentOwner == true);
            var previousOwnerId = currentOwnerLink?.CustomerId;
            var newOwnerId = command.CustomerId.Value;

            if (previousOwnerId != newOwnerId)
            {
                if (currentOwnerLink != null)
                {
                    currentOwnerLink.IsCurrentOwner = false;
                    currentOwnerLink.TransferDate = today;
                    currentOwnerLink.UpdatedAt = now;
                    currentOwnerLink.UpdatedBy = actor.UserId;
                }

                if (newOwnerId > 0)
                {
                    var existingLink = vehicle.CustomerVehicles.FirstOrDefault(cv => cv.CustomerId == newOwnerId);
                    if (existingLink != null)
                    {
                        existingLink.IsCurrentOwner = true;
                        existingLink.TransferDate = null;
                        existingLink.OwnershipStartDate ??= today;
                        existingLink.UpdatedAt = now;
                        existingLink.UpdatedBy = actor.UserId;
                    }
                    else
                    {
                        _context.CustomerVehicles.Add(new CustomerVehicle
                        {
                            CustomerId = newOwnerId,
                            VehicleId = id,
                            IsCurrentOwner = true,
                            OwnershipStartDate = today,
                            Note = command.Note?.Trim(),
                            CreatedAt = now,
                            CreatedBy = actor.UserId,
                            UpdatedAt = now,
                            UpdatedBy = actor.UserId,
                            IsDeleted = false
                        });
                    }
                }
            }
        }

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

        var updatedOwner = vehicle.CustomerVehicles.FirstOrDefault(cv => cv.IsCurrentOwner == true);

        return new VehicleDetailDto(
            vehicle.Id,
            vehicle.LicensePlate,
            vehicle.Vin,
            vehicle.ModelId,
            vehicle.Model?.ModelName,
            vehicle.Model?.BrandId,
            vehicle.Model?.Brand?.Name,
            vehicle.Color,
            vehicle.Odo ?? 0,
            vehicle.ManufactureYear,
            updatedOwner?.CustomerId,
            updatedOwner?.Customer?.FullName,
            updatedOwner?.Customer?.Phone,
            updatedOwner?.OwnershipStartDate,
            vehicle.CreatedAt
        );
    }

    public async Task<bool> ChangeOwnerAsync(
        VehicleOwnerChangeCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (command.VehicleId <= 0) return false;

        if (!await _authService.CanAdministerVehiclesAsync(actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền đổi chủ xe.");
        }

        var vehicle = await _context.Vehicles
            .Include(v => v.CustomerVehicles.Where(cv => cv.IsDeleted != true))
            .FirstOrDefaultAsync(v => v.Id == command.VehicleId && v.IsDeleted != true, ct);

        if (vehicle == null) return false;

        var now = VietnamTime.Now;
        var today = DateOnly.FromDateTime(now);

        // Deactivate active owners
        foreach (var link in vehicle.CustomerVehicles.Where(cv => cv.IsCurrentOwner == true))
        {
            link.IsCurrentOwner = false;
            link.TransferDate = today;
            link.UpdatedAt = now;
            link.UpdatedBy = actor.UserId;
        }

        if (command.CustomerId.HasValue && command.CustomerId.Value > 0)
        {
            var customerExists = await _context.Customers
                .AsNoTracking()
                .AnyAsync(c => c.Id == command.CustomerId.Value && c.IsDeleted != true, ct);
            if (!customerExists)
            {
                throw new KeyNotFoundException($"Không tìm thấy khách hàng #{command.CustomerId.Value}.");
            }

            var target = vehicle.CustomerVehicles.FirstOrDefault(cv => cv.CustomerId == command.CustomerId.Value);
            if (target != null)
            {
                target.IsCurrentOwner = true;
                target.TransferDate = null;
                target.OwnershipStartDate ??= today;
                if (!string.IsNullOrWhiteSpace(command.Note)) target.Note = command.Note.Trim();
                target.UpdatedAt = now;
                target.UpdatedBy = actor.UserId;
            }
            else
            {
                _context.CustomerVehicles.Add(new CustomerVehicle
                {
                    CustomerId = command.CustomerId.Value,
                    VehicleId = command.VehicleId,
                    IsCurrentOwner = true,
                    OwnershipStartDate = today,
                    TransferDate = null,
                    Note = command.Note?.Trim(),
                    CreatedAt = now,
                    CreatedBy = actor.UserId,
                    UpdatedAt = now,
                    UpdatedBy = actor.UserId,
                    IsDeleted = false
                });
            }
        }

        vehicle.UpdatedAt = now;
        vehicle.UpdatedBy = actor.UserId;

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

        return true;
    }

    public async Task<bool> HideVehicleAsync(
        long vehicleId,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (vehicleId <= 0) return false;

        if (!await _authService.CanAdministerVehiclesAsync(actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền ẩn phương tiện.");
        }

        var vehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId && v.IsDeleted != true, ct);

        if (vehicle == null) return false;

        var now = VietnamTime.Now;
        vehicle.IsDeleted = true;
        vehicle.DeletedAt = now;
        vehicle.DeletedBy = actor.UserId;

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

        return true;
    }
}
