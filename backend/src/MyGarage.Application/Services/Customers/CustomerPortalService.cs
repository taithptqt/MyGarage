using Microsoft.Extensions.Options;
using MyGarage.Application.CustomerPortal;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Options;

namespace MyGarage.Services;

public sealed class CustomerPortalService : ICustomerPortalService
{
    private readonly ICustomerPortalStore _store;
    private readonly IOptions<CustomerAutomationJobsOptions> _jobOptions;

    public CustomerPortalService(ICustomerPortalStore store, IOptions<CustomerAutomationJobsOptions> jobOptions)
    {
        _store = store;
        _jobOptions = jobOptions;
    }

    public Task<CustomerPortalDashboardDto> GetDashboardAsync(long customerId, CancellationToken cancellationToken = default)
    {
        return _store.GetDashboardAsync(customerId, cancellationToken);
    }

    public Task<IReadOnlyList<CustomerPortalVehicleDto>> GetVehiclesAsync(long customerId,
        CancellationToken cancellationToken = default)
    {
        return _store.GetPortalVehiclesAsync(customerId, cancellationToken);
    }

    public async Task<CustomerPortalVehicleDto> CreateVehicleAsync(long customerId, CustomerPortalVehicleUpsertDto dto,
        CancellationToken cancellationToken = default)
    {
        var plate = NormalizeVehiclePlate(dto.LicensePlate);
        if (string.IsNullOrEmpty(plate))
            throw new InvalidOperationException("Biển số không hợp lệ.");

        var modelInfo = await _store.GetActiveModelInfoAsync(dto.ModelId, cancellationToken);
        if (modelInfo == null)
            throw new InvalidOperationException("Dòng xe không hợp lệ.");

        var existingVehicle = await _store.GetActiveVehicleByPlateAsync(plate, cancellationToken);

        if (existingVehicle != null)
        {
            var otherOwns = await _store.HasOtherActiveOwnerAsync(existingVehicle.Id, customerId, cancellationToken);
            if (otherOwns)
                throw new InvalidOperationException("Biển số đã thuộc khách hàng khác.");

            if (!string.IsNullOrWhiteSpace(dto.Vin))
            {
                var vinNorm = NormalizeVehicleVin(dto.Vin);
                if (!string.Equals(existingVehicle.Vin, vinNorm, StringComparison.OrdinalIgnoreCase) &&
                    await _store.VinExistsOnOtherVehicleAsync(vinNorm, existingVehicle.Id, cancellationToken))
                    throw new InvalidOperationException("Số VIN đã tồn tại trên xe khác.");
            }

            var now = VietnamTime.Now;
            existingVehicle.Vin = string.IsNullOrWhiteSpace(dto.Vin) ? existingVehicle.Vin : NormalizeVehicleVin(dto.Vin);
            existingVehicle.ModelId = dto.ModelId;
            existingVehicle.Color = dto.Color?.Trim();
            existingVehicle.ManufactureYear = dto.ManufactureYear ?? existingVehicle.ManufactureYear;
            existingVehicle.Odo = dto.Odo ?? existingVehicle.Odo;
            existingVehicle.OdoLastUpdatedAt = dto.Odo.HasValue ? now : existingVehicle.OdoLastUpdatedAt;
            existingVehicle.UpdatedAt = now;

            await _store.UpsertCustomerVehicleAsync(customerId, existingVehicle, null, null, now, cancellationToken);
            return await _store.GetSinglePortalVehicleDtoAsync(customerId, existingVehicle.Id, cancellationToken)
                   ?? throw new InvalidOperationException("Không tải được thông tin xe.");
        }

        if (!string.IsNullOrWhiteSpace(dto.Vin))
        {
            var vinNorm = NormalizeVehicleVin(dto.Vin);
            if (await _store.VinExistsOnOtherVehicleAsync(vinNorm, null, cancellationToken))
                throw new InvalidOperationException("Số VIN đã tồn tại.");
        }

        var createNow = VietnamTime.Now;
        var newVehicle = new Vehicle
        {
            LicensePlate = plate,
            Vin = string.IsNullOrWhiteSpace(dto.Vin) ? null : NormalizeVehicleVin(dto.Vin),
            ModelId = dto.ModelId,
            Color = dto.Color?.Trim(),
            ManufactureYear = dto.ManufactureYear,
            Odo = dto.Odo,
            OdoLastUpdatedAt = dto.Odo.HasValue ? createNow : null,
            CreatedAt = createNow
        };

        var savedVehicle = await _store.UpsertCustomerVehicleAsync(customerId, null, newVehicle, null, createNow, cancellationToken);

        return await _store.GetSinglePortalVehicleDtoAsync(customerId, savedVehicle.Id, cancellationToken)
               ?? throw new InvalidOperationException("Không tải được thông tin xe.");
    }

    public async Task<CustomerPortalVehicleDto?> UpdateVehicleAsync(long customerId, long vehicleId,
        CustomerPortalVehicleUpsertDto dto, CancellationToken cancellationToken = default)
    {
        var owns = await _store.CustomerHasVehicleAsync(customerId, vehicleId, requireCurrentOwner: true, cancellationToken);
        if (!owns) return null;

        var vehicle = await _store.GetActiveVehicleByIdAsync(vehicleId, cancellationToken);
        if (vehicle == null) return null;

        var newPlate = NormalizeVehiclePlate(dto.LicensePlate);
        if (await _store.PlateExistsOnOtherVehicleAsync(newPlate, vehicleId, cancellationToken))
            throw new InvalidOperationException("Biển số đã được sử dụng.");

        if (!string.IsNullOrWhiteSpace(dto.Vin))
        {
            var vinNorm = NormalizeVehicleVin(dto.Vin);
            if (await _store.VinExistsOnOtherVehicleAsync(vinNorm, vehicleId, cancellationToken))
                throw new InvalidOperationException("Số VIN đã tồn tại trên xe khác.");
        }

        var modelInfo = await _store.GetActiveModelInfoAsync(dto.ModelId, cancellationToken);
        if (modelInfo == null)
            throw new InvalidOperationException("Dòng xe không hợp lệ.");

        vehicle.LicensePlate = newPlate;
        if (!string.IsNullOrWhiteSpace(dto.Vin))
            vehicle.Vin = NormalizeVehicleVin(dto.Vin);
        vehicle.ModelId = dto.ModelId;
        vehicle.Color = dto.Color?.Trim();
        vehicle.ManufactureYear = dto.ManufactureYear ?? vehicle.ManufactureYear;
        if (dto.Odo.HasValue)
        {
            vehicle.Odo = dto.Odo;
            vehicle.OdoLastUpdatedAt = VietnamTime.Now;
        }

        vehicle.UpdatedAt = VietnamTime.Now;
        await _store.SaveVehicleUpdateAsync(vehicle, cancellationToken);
        return await _store.GetSinglePortalVehicleDtoAsync(customerId, vehicleId, cancellationToken);
    }

    public async Task<CustomerPortalVehicleHistoryDto?> GetVehicleHistoryAsync(long customerId, long vehicleId,
        CancellationToken cancellationToken = default)
    {
        if (!await _store.CustomerHasVehicleAsync(customerId, vehicleId, requireCurrentOwner: true, cancellationToken))
            return null;

        return await _store.GetVehicleHistoryAsync(
            customerId,
            vehicleId,
            _jobOptions.Value.MaintenanceOutreach,
            cancellationToken);
    }

    public async Task<(IReadOnlyList<CustomerPortalNotificationDto> items, int unreadCount, int totalCount)> GetNotificationsAsync(
        long customerId, bool? unreadOnly, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var (list, unreadCount, totalCount) = await _store.GetNotificationsAsync(
            customerId,
            unreadOnly,
            page,
            pageSize,
            cancellationToken);

        foreach (var n in list)
            n.DerivedType = DeriveNotificationType(n.LinkAction);

        return (list, unreadCount, totalCount);
    }

    public Task<bool> MarkNotificationReadAsync(long customerId, long notificationId,
        CancellationToken cancellationToken = default)
    {
        return _store.MarkNotificationReadAsync(customerId, notificationId, cancellationToken);
    }

    public Task<int> MarkAllNotificationsReadAsync(long customerId, CancellationToken cancellationToken = default)
    {
        return _store.MarkAllNotificationsReadAsync(customerId, cancellationToken);
    }

    public Task<CustomerPortalVehicleLookupDto> GetVehicleLookupsAsync(CancellationToken cancellationToken = default)
    {
        return _store.GetVehicleLookupsAsync(cancellationToken);
    }

    public Task<CustomerPortalServiceOrderLinkContextDto?> GetServiceOrderLinkContextAsync(
        long customerId,
        long serviceOrderId,
        CancellationToken cancellationToken = default)
    {
        return _store.GetServiceOrderLinkContextAsync(customerId, serviceOrderId, cancellationToken);
    }

    private static string DeriveNotificationType(string? link)
    {
        if (string.IsNullOrWhiteSpace(link)) return "general";
        var l = link.Trim();
        if (l.Contains("notificationType=maintenance", StringComparison.OrdinalIgnoreCase)) return "maintenance";
        if (l.Contains("notificationType=survey", StringComparison.OrdinalIgnoreCase)) return "survey";
        if (l.Contains("notificationType=insurance", StringComparison.OrdinalIgnoreCase)) return "insurance";
        if (l.Contains("notificationType=service_status", StringComparison.OrdinalIgnoreCase)) return "service_status";
        if (l.StartsWith("/jobs/maintenance/", StringComparison.OrdinalIgnoreCase)) return "maintenance";
        if (l.StartsWith("/jobs/survey/service-order/", StringComparison.OrdinalIgnoreCase)) return "survey";
        if (l.StartsWith("/jobs/insurance/", StringComparison.OrdinalIgnoreCase)) return "insurance";
        if (l.StartsWith("/my-garage-dashboard", StringComparison.OrdinalIgnoreCase)) return "service_status";
        return "general";
    }

    private static string NormalizeVehiclePlate(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return string.Empty;

        return rawValue.Trim().ToUpperInvariant();
    }

    private static string NormalizeVehicleVin(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return string.Empty;

        return rawValue.Trim().ToUpperInvariant();
    }
}
