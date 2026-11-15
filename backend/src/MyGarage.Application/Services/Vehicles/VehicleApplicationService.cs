using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Vehicles;
using MyGarage.Application.Vehicles.DTOs;
using MyGarage.Common;
using MyGarage.Domain.Rules;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class VehicleApplicationService : IVehicleApplicationService
{
    private readonly IVehicleStore _store;
    private readonly IVehicleAuthorizationService _authService;

    public VehicleApplicationService(
        IVehicleStore store,
        IVehicleAuthorizationService authService)
    {
        _store = store;
        _authService = authService;
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

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
        var (items, totalCount) = await _store.GetVehicleAdminPageAsync(query, page, pageSize, ct);

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

        var vehicle = await _store.GetVehicleDetailByIdAsync(id, ct);
        if (vehicle == null) return null;

        var currentOwnerLink = vehicle.CustomerVehicles.FirstOrDefault();
        return MapDetailDto(vehicle, currentOwnerLink);
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

        if (await _store.PlateExistsAsync(plate, null, ct))
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

            if (await _store.VinExistsAsync(vin, null, ct))
            {
                throw new InvalidOperationException($"Số VIN '{vin}' đã tồn tại trong hệ thống.");
            }
        }

        if (!command.ModelId.HasValue || command.ModelId.Value <= 0)
        {
            throw new ArgumentException("Vui lòng chọn kiểu xe (Model).", nameof(command.ModelId));
        }

        var model = await _store.GetActiveModelWithBrandAsync(command.ModelId.Value, ct);
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
            if (!await _store.ActiveCustomerExistsAsync(command.CustomerId.Value, ct))
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

        await _store.CreateVehicleAsync(vehicle, ownerLink, ct);

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

        var vehicle = await _store.GetVehicleForUpdateAsync(id, ct);
        if (vehicle == null) return null;

        var plate = VehicleDomainRules.NormalizePlate(command.LicensePlate);
        if (!string.IsNullOrEmpty(plate))
        {
            if (!VehicleDomainRules.IsValidPlate(plate))
            {
                throw new ArgumentException("Biển số xe không đúng định dạng.", nameof(command.LicensePlate));
            }

            if (await _store.PlateExistsAsync(plate, id, ct))
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

            if (await _store.VinExistsAsync(vin, id, ct))
            {
                throw new InvalidOperationException($"Số VIN '{vin}' đã được sử dụng bởi xe khác.");
            }
            vehicle.Vin = vin;
        }

        if (command.ModelId.HasValue && command.ModelId.Value > 0 && command.ModelId.Value != vehicle.ModelId)
        {
            if (!await _store.ActiveModelExistsAsync(command.ModelId.Value, ct))
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

        var newLinks = new List<CustomerVehicle>();
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
                        newLinks.Add(new CustomerVehicle
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

        await _store.SaveVehicleWithNewOwnerLinksAsync(vehicle, newLinks, ct);

        var updatedOwner = vehicle.CustomerVehicles.FirstOrDefault(cv => cv.IsCurrentOwner == true);
        return MapDetailDto(vehicle, updatedOwner);
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

        var vehicle = await _store.GetVehicleForOwnerChangeAsync(command.VehicleId, ct);
        if (vehicle == null) return false;

        var now = VietnamTime.Now;
        var today = DateOnly.FromDateTime(now);

        foreach (var link in vehicle.CustomerVehicles.Where(cv => cv.IsCurrentOwner == true))
        {
            link.IsCurrentOwner = false;
            link.TransferDate = today;
            link.UpdatedAt = now;
            link.UpdatedBy = actor.UserId;
        }

        var newLinks = new List<CustomerVehicle>();
        if (command.CustomerId.HasValue && command.CustomerId.Value > 0)
        {
            if (!await _store.ActiveCustomerExistsAsync(command.CustomerId.Value, ct))
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
                newLinks.Add(new CustomerVehicle
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

        await _store.SaveVehicleWithNewOwnerLinksAsync(vehicle, newLinks, ct);
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

        var vehicle = await _store.GetVehicleForHideAsync(vehicleId, ct);
        if (vehicle == null) return false;

        vehicle.IsDeleted = true;
        vehicle.DeletedAt = VietnamTime.Now;
        vehicle.DeletedBy = actor.UserId;

        await _store.SaveVehicleWithNewOwnerLinksAsync(vehicle, Array.Empty<CustomerVehicle>(), ct);
        return true;
    }

    private static VehicleDetailDto MapDetailDto(Vehicle vehicle, CustomerVehicle? ownerLink)
        => new(
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
            ownerLink?.CustomerId,
            ownerLink?.Customer?.FullName,
            ownerLink?.Customer?.Phone,
            ownerLink?.OwnershipStartDate,
            vehicle.CreatedAt
        );
}
