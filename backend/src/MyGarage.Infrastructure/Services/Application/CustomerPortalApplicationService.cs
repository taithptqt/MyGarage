using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Authorization;
using MyGarage.Application.CustomerPortal;
using MyGarage.Application.CustomerPortal.DTOs;
using MyGarage.Common;
using MyGarage.Domain.Rules;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class CustomerPortalApplicationService : ICustomerPortalApplicationService
{
    private readonly IGarageContext _context;
    private readonly ICustomerPortalAuthorizationService _authService;

    public CustomerPortalApplicationService(
        IGarageContext context,
        ICustomerPortalAuthorizationService authService)
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

        return await _context.Database.BeginTransactionAsync();
    }

    public async Task<CustomerPortalDashboardSummary> GetDashboardSummaryAsync(
        long customerId,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanAccessCustomerDashboardAsync(customerId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền truy cập bảng thông tin khách hàng này.");
        }

        var ownedVehicleIds = await _context.CustomerVehicles.AsNoTracking()
            .Where(cv => cv.CustomerId == customerId && cv.IsDeleted != true && (cv.IsCurrentOwner ?? true))
            .Select(cv => cv.VehicleId)
            .Distinct()
            .ToListAsync(ct);

        var vehicleCount = ownedVehicleIds.Count;

        var activeOrderCount = await _context.ServiceOrders.AsNoTracking()
            .Where(so => so.CustomerId == customerId)
            .Where(so => so.Status != "CLOSED" && so.Status != "CANCELED" && so.Status != "DELIVERED")
            .CountAsync(ct);

        var now = VietnamTime.Now;
        var upcomingAppointmentCount = await _context.Appointments.AsNoTracking()
            .Where(a => a.CustomerId == customerId)
            .Where(a => a.StartTime >= now.AddHours(-1))
            .Where(a => a.Status == "PENDING" || a.Status == "CONFIRMED")
            .CountAsync(ct);

        var unreadNotificationCount = await _context.Notifications.AsNoTracking()
            .Where(n => n.CustomerId == customerId && n.IsRead != true)
            .CountAsync(ct);

        return new CustomerPortalDashboardSummary(
            customerId,
            activeOrderCount,
            upcomingAppointmentCount,
            vehicleCount,
            unreadNotificationCount
        );
    }

    public async Task<IReadOnlyList<CustomerPortalVehicleItemDto>> GetCustomerVehiclesAsync(
        long customerId,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanAccessCustomerDashboardAsync(customerId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền truy cập danh sách xe của khách hàng này.");
        }

        var list = await _context.CustomerVehicles.AsNoTracking()
            .Where(cv => cv.CustomerId == customerId && cv.IsDeleted != true)
            .Include(cv => cv.Vehicle)
                .ThenInclude(v => v.Model)
                    .ThenInclude(m => m!.Brand)
            .OrderByDescending(cv => cv.IsCurrentOwner ?? false)
            .ThenByDescending(cv => cv.UpdatedAt ?? cv.CreatedAt)
            .ToListAsync(ct);

        return list.Select(cv =>
        {
            var v = cv.Vehicle;
            var brandName = v?.Model?.Brand?.Name;
            var modelName = v?.Model?.ModelName;
            return new CustomerPortalVehicleItemDto(
                cv.VehicleId,
                v?.LicensePlate ?? string.Empty,
                brandName,
                modelName,
                v?.ManufactureYear,
                v?.Color,
                v?.Vin,
                v?.Odo,
                cv.IsCurrentOwner ?? false
            );
        }).ToList();
    }

    public async Task<CustomerPortalVehicleItemDto> RegisterVehicleAsync(
        long customerId,
        CustomerPortalVehicleUpsertCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanAccessCustomerDashboardAsync(customerId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền đăng ký xe cho khách hàng này.");
        }

        var validation = CustomerPortalDomainRules.ValidateCustomerVehicleInput(
            command.LicensePlate,
            command.Vin,
            command.Color,
            command.ManufactureYear
        );

        if (!validation.IsValid)
        {
            throw new ArgumentException(validation.Error ?? "Thông tin xe không hợp lệ.");
        }
        if (command.CurrentOdo < 0)
        {
            throw new ArgumentException("Số km không được âm.");
        }

        var model = await _context.VehicleModels.AsNoTracking()
            .Where(m => m.Id == command.ModelId && m.IsDeleted != true)
            .Select(m => new { m.Id, m.ModelName, BrandName = m.Brand.Name })
            .FirstOrDefaultAsync(ct);
        if (model == null) throw new InvalidOperationException("Dòng xe không hợp lệ.");

        var normPlate = VehicleDomainRules.NormalizePlate(command.LicensePlate)!;
        var normVin = VehicleDomainRules.NormalizeVin(command.Vin);
        var now = VietnamTime.Now;
        var staffId = string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase) ? actor.UserId : null;

        var plateKey = normPlate.ToLowerInvariant();
        var existingVehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.LicensePlate.ToLower() == plateKey && v.IsDeleted != true, ct);

        if (existingVehicle != null)
        {
            var otherActiveOwner = await _context.CustomerVehicles
                .AnyAsync(cv => cv.VehicleId == existingVehicle.Id && cv.CustomerId != customerId && cv.IsDeleted != true && (cv.IsCurrentOwner ?? true), ct);
            if (otherActiveOwner)
            {
                throw new InvalidOperationException("Biển số đã thuộc khách hàng khác.");
            }
        }

        if (normVin != null)
        {
            var vinKey = normVin.ToLowerInvariant();
            var existingId = existingVehicle?.Id;
            if (await _context.Vehicles.AsNoTracking().AnyAsync(v =>
                    v.IsDeleted != true && v.Vin != null && v.Vin.ToLower() == vinKey &&
                    (!existingId.HasValue || v.Id != existingId.Value), ct))
            {
                throw new InvalidOperationException(existingVehicle == null
                    ? "Số VIN đã tồn tại."
                    : "Số VIN đã tồn tại trên xe khác.");
            }
        }

        long vehicleId = 0;
        Vehicle? savedVehicle = null;
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                Vehicle v;
                if (existingVehicle != null)
                {
                    v = existingVehicle;
                    v.ModelId = model.Id;
                    if (!string.IsNullOrEmpty(normVin)) v.Vin = normVin;
                    if (!string.IsNullOrWhiteSpace(command.Color)) v.Color = command.Color.Trim();
                    if (command.ManufactureYear.HasValue) v.ManufactureYear = command.ManufactureYear.Value;
                    if (command.CurrentOdo.HasValue && command.CurrentOdo.Value >= 0)
                    {
                        v.Odo = command.CurrentOdo.Value;
                        v.OdoLastUpdatedAt = now;
                    }
                    v.UpdatedAt = now;
                    if (staffId.HasValue) v.UpdatedBy = staffId;
                }
                else
                {
                    v = new Vehicle
                    {
                        LicensePlate = normPlate,
                        ModelId = model.Id,
                        Vin = normVin,
                        Color = command.Color?.Trim(),
                        ManufactureYear = command.ManufactureYear,
                        Odo = command.CurrentOdo ?? 0,
                        OdoLastUpdatedAt = command.CurrentOdo.HasValue ? now : null,
                        CreatedAt = now,
                        CreatedBy = staffId,
                        UpdatedAt = now,
                        UpdatedBy = staffId,
                        IsDeleted = false
                    };
                    _context.Vehicles.Add(v);
                    await _context.SaveChangesAsync(ct);
                }

                vehicleId = v.Id;
                savedVehicle = v;

                var existingLink = await _context.CustomerVehicles
                    .FirstOrDefaultAsync(cv => cv.CustomerId == customerId && cv.VehicleId == vehicleId && cv.IsDeleted != true, ct);

                if (existingLink != null)
                {
                    existingLink.IsCurrentOwner = true;
                    existingLink.UpdatedAt = now;
                    if (staffId.HasValue) existingLink.UpdatedBy = staffId;
                }
                else
                {
                    var link = new CustomerVehicle
                    {
                        CustomerId = customerId,
                        VehicleId = vehicleId,
                        IsCurrentOwner = true,
                        OwnershipStartDate = DateOnly.FromDateTime(now),
                        CreatedAt = now,
                        CreatedBy = staffId,
                        UpdatedAt = now,
                        UpdatedBy = staffId,
                        IsDeleted = false
                    };
                    _context.CustomerVehicles.Add(link);
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

        var resultVehicle = savedVehicle ?? throw new InvalidOperationException("Không tải được thông tin xe.");
        return new CustomerPortalVehicleItemDto(
            vehicleId,
            resultVehicle.LicensePlate,
            model.BrandName,
            model.ModelName,
            resultVehicle.ManufactureYear,
            resultVehicle.Color,
            resultVehicle.Vin,
            resultVehicle.Odo,
            true
        );
    }

    public async Task<CustomerPortalVehicleItemDto?> UpdateVehicleAsync(
        long customerId,
        long vehicleId,
        CustomerPortalVehicleUpsertCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanManageCustomerVehicleAsync(customerId, vehicleId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền chỉnh sửa thông tin xe này.");
        }

        var validation = CustomerPortalDomainRules.ValidateCustomerVehicleInput(
            command.LicensePlate,
            command.Vin,
            command.Color,
            command.ManufactureYear
        );

        if (!validation.IsValid)
        {
            throw new ArgumentException(validation.Error ?? "Thông tin xe không hợp lệ.");
        }
        if (command.CurrentOdo < 0)
        {
            throw new ArgumentException("Số km không được âm.");
        }

        var model = await _context.VehicleModels.AsNoTracking()
            .Where(m => m.Id == command.ModelId && m.IsDeleted != true)
            .Select(m => new { m.Id, m.ModelName, BrandName = m.Brand.Name })
            .FirstOrDefaultAsync(ct);
        if (model == null) throw new InvalidOperationException("Dòng xe không hợp lệ.");

        var normPlate = VehicleDomainRules.NormalizePlate(command.LicensePlate)!;
        var normVin = VehicleDomainRules.NormalizeVin(command.Vin);
        var now = VietnamTime.Now;
        var staffId = string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase) ? actor.UserId : null;

        var v = await _context.Vehicles.FirstOrDefaultAsync(x => x.Id == vehicleId && x.IsDeleted != true, ct);
        if (v == null)
        {
            return null;
        }

        var plateKey = normPlate.ToLowerInvariant();
        if (await _context.Vehicles.AsNoTracking().AnyAsync(other =>
                other.Id != vehicleId && other.IsDeleted != true && other.LicensePlate.ToLower() == plateKey, ct))
        {
            throw new InvalidOperationException("Biển số đã được sử dụng.");
        }
        if (normVin != null)
        {
            var vinKey = normVin.ToLowerInvariant();
            if (await _context.Vehicles.AsNoTracking().AnyAsync(other =>
                    other.Id != vehicleId && other.IsDeleted != true && other.Vin != null && other.Vin.ToLower() == vinKey, ct))
            {
                throw new InvalidOperationException("Số VIN đã tồn tại trên xe khác.");
            }
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                v.LicensePlate = normPlate;
                v.ModelId = model.Id;
                if (!string.IsNullOrEmpty(normVin)) v.Vin = normVin;
                if (!string.IsNullOrWhiteSpace(command.Color)) v.Color = command.Color.Trim();
                if (command.ManufactureYear.HasValue) v.ManufactureYear = command.ManufactureYear.Value;
                if (command.CurrentOdo.HasValue && command.CurrentOdo.Value >= 0)
                {
                    v.Odo = command.CurrentOdo.Value;
                    v.OdoLastUpdatedAt = now;
                }
                v.UpdatedAt = now;
                if (staffId.HasValue) v.UpdatedBy = staffId;

                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

        return new CustomerPortalVehicleItemDto(
            v.Id,
            v.LicensePlate,
            model.BrandName,
            model.ModelName,
            v.ManufactureYear,
            v.Color,
            v.Vin,
            v.Odo,
            true
        );
    }

    public async Task<CustomerPortalOrderLinkContext?> GetServiceOrderLinkContextAsync(
        long customerId,
        long serviceOrderId,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanAccessCustomerDashboardAsync(customerId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền truy cập dữ liệu đơn sửa chữa này.");
        }

        var order = await _context.ServiceOrders.AsNoTracking()
            .Where(so => so.Id == serviceOrderId && so.CustomerId == customerId)
            .Select(so => new { so.Id, so.Code, so.Status, so.CustomerId })
            .FirstOrDefaultAsync(ct);

        if (order == null)
        {
            return null;
        }

        var hasActiveDiag = await _context.Diagnoses.AsNoTracking()
            .AnyAsync(d => d.ServiceOrderId == serviceOrderId && (d.Status == "PENDING" || d.Status == "ASSIGNED"), ct);

        var hasWaitingParts = await _context.ServiceOrderDetails.AsNoTracking()
            .AnyAsync(l => l.ServiceOrderId == serviceOrderId && l.Status != null && l.Status.ToUpper() == "WAITING_PARTS", ct);

        var stageCode = CustomerPortalDomainRules.ResolveStage(order.Status, hasActiveDiag, hasWaitingParts);
        var stageLabel = CustomerPortalDomainRules.ToVietnameseLabel(stageCode);
        var isCanceled = CustomerPortalDomainRules.IsCanceledStatus(order.Status);
        var domainTimeline = CustomerPortalDomainRules.BuildTimeline(stageCode, isCanceled);

        var timelineItems = domainTimeline.Select(t => new CustomerPortalTimelineItem(t.Code, t.Label, t.State)).ToList();

        return new CustomerPortalOrderLinkContext(
            customerId,
            serviceOrderId,
            order.Code ?? $"SO-{serviceOrderId}",
            order.Status,
            stageCode,
            stageLabel,
            timelineItems
        );
    }
}
