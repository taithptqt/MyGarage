using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.CustomerPortal;
using MyGarage.Application.CustomerPortal.DTOs;
using MyGarage.Common;
using MyGarage.Domain.Rules;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class CustomerPortalApplicationService : ICustomerPortalApplicationService
{
    private readonly ICustomerPortalStore _store;
    private readonly ICustomerPortalAuthorizationService _authService;

    public CustomerPortalApplicationService(
        ICustomerPortalStore store,
        ICustomerPortalAuthorizationService authService)
    {
        _store = store;
        _authService = authService;
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

        return await _store.GetDashboardSummaryAsync(customerId, VietnamTime.Now, ct);
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

        return await _store.GetCustomerVehiclesAsync(customerId, ct);
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

        var model = await ValidateVehicleCommandAndLoadModelAsync(command, ct);
        var normPlate = VehicleDomainRules.NormalizePlate(command.LicensePlate)!;
        var normVin = VehicleDomainRules.NormalizeVin(command.Vin);
        var now = VietnamTime.Now;
        var staffId = string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase) ? actor.UserId : null;

        var existingVehicle = await _store.GetActiveVehicleByPlateAsync(normPlate, ct);
        if (existingVehicle != null && await _store.HasOtherActiveOwnerAsync(existingVehicle.Id, customerId, ct))
        {
            throw new InvalidOperationException("Biển số đã thuộc khách hàng khác.");
        }

        if (normVin != null && await _store.VinExistsOnOtherVehicleAsync(normVin, existingVehicle?.Id, ct))
        {
            throw new InvalidOperationException(existingVehicle == null
                ? "Số VIN đã tồn tại."
                : "Số VIN đã tồn tại trên xe khác.");
        }

        Vehicle? newVehicle = null;
        if (existingVehicle != null)
        {
            ApplyVehicleFields(existingVehicle, model.Id, normPlate, normVin, command, now, staffId, updatePlate: false);
        }
        else
        {
            newVehicle = new Vehicle
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
        }

        var savedVehicle = await _store.UpsertCustomerVehicleAsync(customerId, existingVehicle, newVehicle, staffId, now, ct);

        return new CustomerPortalVehicleItemDto(
            savedVehicle.Id,
            savedVehicle.LicensePlate,
            model.BrandName,
            model.ModelName,
            savedVehicle.ManufactureYear,
            savedVehicle.Color,
            savedVehicle.Vin,
            savedVehicle.Odo,
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

        var model = await ValidateVehicleCommandAndLoadModelAsync(command, ct);
        var normPlate = VehicleDomainRules.NormalizePlate(command.LicensePlate)!;
        var normVin = VehicleDomainRules.NormalizeVin(command.Vin);
        var now = VietnamTime.Now;
        var staffId = string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase) ? actor.UserId : null;

        var vehicle = await _store.GetActiveVehicleByIdAsync(vehicleId, ct);
        if (vehicle == null)
        {
            return null;
        }

        if (await _store.PlateExistsOnOtherVehicleAsync(normPlate, vehicleId, ct))
        {
            throw new InvalidOperationException("Biển số đã được sử dụng.");
        }

        if (normVin != null && await _store.VinExistsOnOtherVehicleAsync(normVin, vehicleId, ct))
        {
            throw new InvalidOperationException("Số VIN đã tồn tại trên xe khác.");
        }

        ApplyVehicleFields(vehicle, model.Id, normPlate, normVin, command, now, staffId, updatePlate: true);
        await _store.SaveVehicleUpdateAsync(vehicle, ct);

        return new CustomerPortalVehicleItemDto(
            vehicle.Id,
            vehicle.LicensePlate,
            model.BrandName,
            model.ModelName,
            vehicle.ManufactureYear,
            vehicle.Color,
            vehicle.Vin,
            vehicle.Odo,
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

        var snapshot = await _store.GetOrderSnapshotAsync(customerId, serviceOrderId, ct);
        if (snapshot == null)
        {
            return null;
        }

        var stageCode = CustomerPortalDomainRules.ResolveStage(snapshot.Status, snapshot.HasActiveDiagnosis, snapshot.HasWaitingParts);
        var stageLabel = CustomerPortalDomainRules.ToVietnameseLabel(stageCode);
        var isCanceled = CustomerPortalDomainRules.IsCanceledStatus(snapshot.Status);
        var domainTimeline = CustomerPortalDomainRules.BuildTimeline(stageCode, isCanceled);

        var timelineItems = domainTimeline.Select(t => new CustomerPortalTimelineItem(t.Code, t.Label, t.State)).ToList();

        return new CustomerPortalOrderLinkContext(
            customerId,
            serviceOrderId,
            snapshot.Code ?? $"SO-{serviceOrderId}",
            snapshot.Status,
            stageCode,
            stageLabel,
            timelineItems
        );
    }

    private async Task<CustomerPortalModelInfo> ValidateVehicleCommandAndLoadModelAsync(
        CustomerPortalVehicleUpsertCommand command,
        CancellationToken ct)
    {
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

        var model = await _store.GetActiveModelInfoAsync(command.ModelId, ct);
        return model ?? throw new InvalidOperationException("Dòng xe không hợp lệ.");
    }

    private static void ApplyVehicleFields(
        Vehicle vehicle,
        long modelId,
        string normPlate,
        string? normVin,
        CustomerPortalVehicleUpsertCommand command,
        DateTime now,
        long? staffId,
        bool updatePlate)
    {
        if (updatePlate)
        {
            vehicle.LicensePlate = normPlate;
        }
        vehicle.ModelId = modelId;
        if (!string.IsNullOrEmpty(normVin)) vehicle.Vin = normVin;
        if (!string.IsNullOrWhiteSpace(command.Color)) vehicle.Color = command.Color.Trim();
        if (command.ManufactureYear.HasValue) vehicle.ManufactureYear = command.ManufactureYear.Value;
        if (command.CurrentOdo.HasValue && command.CurrentOdo.Value >= 0)
        {
            vehicle.Odo = command.CurrentOdo.Value;
            vehicle.OdoLastUpdatedAt = now;
        }
        vehicle.UpdatedAt = now;
        if (staffId.HasValue) vehicle.UpdatedBy = staffId;
    }
}
