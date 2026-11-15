using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.CustomerPortal;
using MyGarage.Application.CustomerPortal.DTOs;
using MyGarage.Common;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Options;
using MyGarage.Support;

namespace MyGarage.Infrastructure.CustomerPortal;

public sealed partial class EfCustomerPortalStore
{
    public async Task<CustomerPortalDashboardSummary> GetDashboardSummaryAsync(long customerId, DateTime now, CancellationToken ct = default)
    {
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

    public async Task<IReadOnlyList<CustomerPortalVehicleItemDto>> GetCustomerVehiclesAsync(long customerId, CancellationToken ct = default)
    {
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
            return new CustomerPortalVehicleItemDto(
                cv.VehicleId,
                v?.LicensePlate ?? string.Empty,
                v?.Model?.Brand?.Name,
                v?.Model?.ModelName,
                v?.ManufactureYear,
                v?.Color,
                v?.Vin,
                v?.Odo,
                cv.IsCurrentOwner ?? false
            );
        }).ToList();
    }

    public async Task<CustomerPortalModelInfo?> GetActiveModelInfoAsync(long modelId, CancellationToken ct = default)
    {
        return await _context.VehicleModels.AsNoTracking()
            .Where(m => m.Id == modelId && m.IsDeleted != true)
            .Select(m => new CustomerPortalModelInfo(m.Id, m.ModelName, m.Brand.Name))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Vehicle?> GetActiveVehicleByPlateAsync(string normalizedPlate, CancellationToken ct = default)
    {
        var plateKey = normalizedPlate.ToLowerInvariant();
        return await _context.Vehicles
            .FirstOrDefaultAsync(v => v.LicensePlate.ToLower() == plateKey && v.IsDeleted != true, ct);
    }

    public async Task<Vehicle?> GetActiveVehicleByIdAsync(long vehicleId, CancellationToken ct = default)
    {
        return await _context.Vehicles
            .FirstOrDefaultAsync(x => x.Id == vehicleId && x.IsDeleted != true, ct);
    }

    public async Task<bool> HasOtherActiveOwnerAsync(long vehicleId, long customerId, CancellationToken ct = default)
    {
        return await _context.CustomerVehicles
            .AnyAsync(cv => cv.VehicleId == vehicleId && cv.CustomerId != customerId && cv.IsDeleted != true && (cv.IsCurrentOwner ?? true), ct);
    }

    public async Task<bool> VinExistsOnOtherVehicleAsync(string normalizedVin, long? excludeVehicleId, CancellationToken ct = default)
    {
        var vinKey = normalizedVin.ToLowerInvariant();
        return await _context.Vehicles.AsNoTracking().AnyAsync(v =>
            v.IsDeleted != true && v.Vin != null && v.Vin.ToLower() == vinKey &&
            (!excludeVehicleId.HasValue || v.Id != excludeVehicleId.Value), ct);
    }

    public async Task<bool> PlateExistsOnOtherVehicleAsync(string normalizedPlate, long excludeVehicleId, CancellationToken ct = default)
    {
        var plateKey = normalizedPlate.ToLowerInvariant();
        return await _context.Vehicles.AsNoTracking().AnyAsync(other =>
            other.Id != excludeVehicleId && other.IsDeleted != true && other.LicensePlate.ToLower() == plateKey, ct);
    }

    public async Task<Vehicle> UpsertCustomerVehicleAsync(
        long customerId,
        Vehicle? existingVehicle,
        Vehicle? newVehicle,
        long? staffId,
        DateTime now,
        CancellationToken ct = default)
    {
        Vehicle? savedVehicle = null;

        await ExecuteInTransactionAsync(async token =>
        {
            Vehicle v;
            if (existingVehicle != null)
            {
                v = existingVehicle;
            }
            else
            {
                v = newVehicle ?? throw new InvalidOperationException("Không có thông tin xe để lưu.");
                _context.Vehicles.Add(v);
                await _context.SaveChangesAsync(token);
            }

            savedVehicle = v;
            var vehicleId = v.Id;

            var existingLink = await _context.CustomerVehicles
                .FirstOrDefaultAsync(cv => cv.CustomerId == customerId && cv.VehicleId == vehicleId && cv.IsDeleted != true, token);

            if (existingLink != null)
            {
                existingLink.IsCurrentOwner = true;
                existingLink.UpdatedAt = now;
                if (staffId.HasValue) existingLink.UpdatedBy = staffId;
            }
            else
            {
                _context.CustomerVehicles.Add(new CustomerVehicle
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
                });
            }
        }, ct);

        return savedVehicle ?? throw new InvalidOperationException("Không tải được thông tin xe.");
    }

    public Task SaveVehicleUpdateAsync(Vehicle vehicle, CancellationToken ct = default)
    {
        return ExecuteInTransactionAsync(_ => Task.CompletedTask, ct);
    }

    public async Task<CustomerPortalOrderSnapshot?> GetOrderSnapshotAsync(long customerId, long serviceOrderId, CancellationToken ct = default)
    {
        var order = await _context.ServiceOrders.AsNoTracking()
            .Where(so => so.Id == serviceOrderId && so.CustomerId == customerId)
            .Select(so => new { so.Id, so.Code, so.Status })
            .FirstOrDefaultAsync(ct);

        if (order == null)
        {
            return null;
        }

        var hasActiveDiag = await _context.Diagnoses.AsNoTracking()
            .AnyAsync(d => d.ServiceOrderId == serviceOrderId && (d.Status == "PENDING" || d.Status == "ASSIGNED"), ct);

        var hasWaitingParts = await _context.ServiceOrderDetails.AsNoTracking()
            .AnyAsync(l => l.ServiceOrderId == serviceOrderId && l.Status != null && l.Status.ToUpper() == "WAITING_PARTS", ct);

        return new CustomerPortalOrderSnapshot(
            order.Id,
            order.Code,
            order.Status,
            hasActiveDiag,
            hasWaitingParts);
    }


    public async Task<IReadOnlyList<CustomerPortalVehicleDto>> GetPortalVehiclesAsync(long customerId, CancellationToken ct = default)
    {
        return await _context.CustomerVehicles.AsNoTracking()
            .Where(cv => cv.CustomerId == customerId && cv.IsDeleted != true && (cv.IsCurrentOwner ?? true))
            .Select(cv => new CustomerPortalVehicleDto
            {
                VehicleId = cv.Vehicle.Id,
                CustomerVehicleId = cv.Id,
                LicensePlate = cv.Vehicle.LicensePlate,
                Vin = cv.Vehicle.Vin,
                ModelId = cv.Vehicle.ModelId,
                BrandName = cv.Vehicle.Model.Brand.Name,
                ModelName = cv.Vehicle.Model.ModelName,
                Color = cv.Vehicle.Color,
                ManufactureYear = cv.Vehicle.ManufactureYear,
                Odo = cv.Vehicle.Odo,
                IsCurrentOwner = cv.IsCurrentOwner ?? true
            })
            .OrderBy(v => v.LicensePlate)
            .ToListAsync(ct);
    }

    public async Task<CustomerPortalVehicleDto?> GetSinglePortalVehicleDtoAsync(long customerId, long vehicleId, CancellationToken ct = default)
    {
        return await _context.CustomerVehicles.AsNoTracking()
            .Where(cv => cv.CustomerId == customerId && cv.VehicleId == vehicleId && cv.IsDeleted != true)
            .Select(cv => new CustomerPortalVehicleDto
            {
                VehicleId = cv.Vehicle.Id,
                CustomerVehicleId = cv.Id,
                LicensePlate = cv.Vehicle.LicensePlate,
                Vin = cv.Vehicle.Vin,
                ModelId = cv.Vehicle.ModelId,
                BrandName = cv.Vehicle.Model.Brand.Name,
                ModelName = cv.Vehicle.Model.ModelName,
                Color = cv.Vehicle.Color,
                ManufactureYear = cv.Vehicle.ManufactureYear,
                Odo = cv.Vehicle.Odo,
                IsCurrentOwner = cv.IsCurrentOwner ?? true
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CustomerPortalVehicleHistoryDto> GetVehicleHistoryAsync(
        long customerId,
        long vehicleId,
        ProactiveMaintenanceOutreachOptions maintenanceSettings,
        CancellationToken ct = default)
    {
        var orders = await _context.ServiceOrders.AsNoTracking()
            .Where(so => so.VehicleId == vehicleId && so.CustomerId == customerId)
            .OrderByDescending(so => so.CheckInTime ?? so.CreatedAt)
            .Take(80)
            .Select(so => new
            {
                so.Id,
                so.Code,
                so.ExecuteBranchId,
                so.CheckInTime,
                CompletedAt = so.ActualHandover ?? so.UpdatedAt,
                so.Status,
                so.FinalAmount
            })
            .ToListAsync(ct);

        var orderIds = orders.Select(o => o.Id).ToList();
        var details = new List<(long ServiceOrderId, string? ItemName, string? ItemType)>();
        if (orderIds.Count > 0)
        {
            var detailRows = await _context.ServiceOrderDetails.AsNoTracking()
                .Where(d => orderIds.Contains(d.ServiceOrderId))
                .Select(d => new { d.ServiceOrderId, d.ItemName, d.ItemType })
                .ToListAsync(ct);
            foreach (var d in detailRows)
                details.Add((d.ServiceOrderId, d.ItemName, d.ItemType));
        }

        var detailMap = details.GroupBy(d => d.ServiceOrderId)
            .ToDictionary(g => g.Key, g => g.Select(x =>
                    string.IsNullOrWhiteSpace(x.ItemName) ? x.ItemType ?? "" : x.ItemName!)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Take(8)
                .ToList());

        Dictionary<long, (long Id, string? Url, string? Pay)> invMap = new();
        if (orderIds.Count > 0)
        {
            var invRows = await _context.Invoices.AsNoTracking()
                .Where(i => i.ServiceOrderId != null && orderIds.Contains(i.ServiceOrderId.Value))
                .Select(i => new { i.ServiceOrderId, i.Id, i.InvoiceUrl, i.PaymentStatus })
                .ToListAsync(ct);
            foreach (var g in invRows.GroupBy(x => x.ServiceOrderId!.Value))
                invMap[g.Key] = g.OrderByDescending(x => x.Id).Select(x => (x.Id, x.InvoiceUrl, x.PaymentStatus)).First();
        }

        var branchIds = orders.Select(o => o.ExecuteBranchId).Distinct().ToList();
        var branchMap = await _context.Branches.AsNoTracking()
            .Where(b => branchIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Name, ct);

        HashSet<long> diagActiveSet = new();
        HashSet<long> wpLineSet = new();
        if (orderIds.Count > 0)
        {
            diagActiveSet = (await _context.Diagnoses.AsNoTracking()
                .Where(d => orderIds.Contains(d.ServiceOrderId))
                .Where(d =>
                    d.Status == nameof(DiagnosisStatus.PENDING) || d.Status == nameof(DiagnosisStatus.ASSIGNED))
                .Select(d => d.ServiceOrderId)
                .Distinct()
                .ToListAsync(ct)).ToHashSet();

            wpLineSet = (await _context.ServiceOrderDetails.AsNoTracking()
                .Where(d => orderIds.Contains(d.ServiceOrderId))
                .Where(d =>
                    d.Status == nameof(ServiceOrderDetailStatusCode.WAITING_PARTS))
                .Select(d => d.ServiceOrderId)
                .Distinct()
                .ToListAsync(ct)).ToHashSet();
        }

        var conditionLogMap = new Dictionary<long, List<CustomerPortalConditionLogDto>>();
        if (orderIds.Count > 0)
        {
            var condRows = await _context.VehicleConditionLogs.AsNoTracking()
                .Where(l => orderIds.Contains(l.ServiceOrderId))
                .OrderBy(l => l.CreatedAt)
                .Select(l => new
                {
                    l.ServiceOrderId,
                    Log = new CustomerPortalConditionLogDto
                    {
                        Id = l.Id,
                        LogType = l.LogType,
                        OdoReading = l.OdoReading,
                        FuelLevel = l.FuelLevel,
                        DashboardWarningLights = NormalizeConditionWarningLights(l.DashboardWarningLights),
                        Valuables = l.Valuables,
                        DefectsAndImages = l.DefectsAndImages,
                        CustomerSignatureUrl = l.CustomerSignatureUrl,
                        CreatedAt = l.CreatedAt
                    }
                })
                .ToListAsync(ct);

            foreach (var row in condRows)
            {
                if (!IsMeaningfulConditionLog(row.Log))
                    continue;
                if (!conditionLogMap.TryGetValue(row.ServiceOrderId, out var logList))
                    conditionLogMap[row.ServiceOrderId] = logList = new List<CustomerPortalConditionLogDto>();
                logList.Add(row.Log);
            }
        }

        var items = new List<CustomerPortalHistoryItemDto>();
        foreach (var o in orders)
        {
            detailMap.TryGetValue(o.Id, out var lines);
            invMap.TryGetValue(o.Id, out var inv);
            var hasDiag = diagActiveSet.Contains(o.Id);
            var hasWp = wpLineSet.Contains(o.Id);
            var stage = CustomerPortalStageMapper.ResolveStage(o.Status, hasDiag, hasWp);
            branchMap.TryGetValue(o.ExecuteBranchId, out var bn);
            long? invoiceId = null;
            string? invoiceUrl = null;
            string? pay = null;
            if (inv.Id != 0)
            {
                invoiceId = inv.Id;
                invoiceUrl = inv.Url;
                pay = inv.Pay;
            }
            conditionLogMap.TryGetValue(o.Id, out var condLogs);

            items.Add(new CustomerPortalHistoryItemDto
            {
                ServiceOrderId = o.Id,
                OrderCode = o.Code,
                BranchId = o.ExecuteBranchId,
                BranchName = bn,
                CheckInTime = o.CheckInTime,
                CompletedAt = o.CompletedAt,
                Status = o.Status,
                CustomerStageLabel = CustomerPortalStageMapper.ToVietnameseLabel(stage),
                LineSummaries = lines ?? new List<string>(),
                TotalAmount = o.FinalAmount,
                PaymentStatus = pay,
                InvoiceId = invoiceId,
                InvoiceUrl = invoiceUrl,
                ConditionLogs = condLogs ?? new List<CustomerPortalConditionLogDto>()
            });
        }

        var hints = await BuildMaintenanceHintsAsync(vehicleId, maintenanceSettings, ct);

        return new CustomerPortalVehicleHistoryDto { Items = items, MaintenanceHints = hints };
    }


    private static string? NormalizeConditionWarningLights(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return null;

        var trimmed = rawValue.Trim();
        return trimmed.ToUpperInvariant() switch
        {
            "PENDING" or "ASSIGNED" or "COMPLETED" => null,
            _ => trimmed
        };
    }

    private static bool IsMeaningfulConditionLog(CustomerPortalConditionLogDto log)
    {
        return log.OdoReading.HasValue ||
               log.FuelLevel.HasValue ||
               !string.IsNullOrWhiteSpace(log.DashboardWarningLights) ||
               !string.IsNullOrWhiteSpace(log.Valuables) ||
               !string.IsNullOrWhiteSpace(log.DefectsAndImages) ||
               !string.IsNullOrWhiteSpace(log.CustomerSignatureUrl);
    }
}
