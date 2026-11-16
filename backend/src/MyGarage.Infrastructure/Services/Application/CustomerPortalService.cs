using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Options;
using MyGarage.Support;
using MyGarage.Application.Bays;

namespace MyGarage.Services;

public sealed class CustomerPortalService : ICustomerPortalService
{
    private readonly IGarageContext _context;
    private readonly IOptions<CustomerAutomationJobsOptions> _jobOptions;
    private readonly IVehicleLocationService _locations;

    private static readonly HashSet<string> TerminalOrderStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(ServiceOrderLifecycleStatus.CLOSED),
        nameof(ServiceOrderLifecycleStatus.CANCELED),
        "CANCELED",
        "DELIVERED"
    };

    private static readonly HashSet<string> CompletedForMaintenance = new(StringComparer.OrdinalIgnoreCase)
    {
        "CLOSED", "READY", "COMPLETED", "DONE"
    };

    private static readonly HashSet<string> ActiveAppointmentStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "PENDING", "CONFIRMED", "ARRIVED", "ARRIVED_LATE", "LATE"
    };

    private static readonly HashSet<string> TerminalWorkSlotStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(WorkSlotStatus.COMPLETED),
        nameof(WorkSlotStatus.CANCELED),
        "CANCELED",
        "CANCELED"
    };

    private static readonly HashSet<string> DashboardOwnedAssignmentStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(ServiceProgressStatus.PENDING),
        nameof(ServiceProgressStatus.IN_PROGRESS),
        nameof(ServiceProgressStatus.PAUSED)
    };

    public CustomerPortalService(IGarageContext context, IOptions<CustomerAutomationJobsOptions> jobOptions, IVehicleLocationService? locations = null)
    {
        _context = context;
        _jobOptions = jobOptions;
        _locations = locations ?? new VehicleLocationService(context);
    }

    public async Task<CustomerPortalDashboardDto> GetDashboardAsync(long customerId, CancellationToken cancellationToken = default)
    {
        var ownedVehicleIds = await _context.CustomerVehicles.AsNoTracking()
            .Where(cv => cv.CustomerId == customerId && cv.IsDeleted != true && (cv.IsCurrentOwner ?? true))
            .Select(cv => cv.VehicleId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var ownedSet = ownedVehicleIds.ToHashSet();

        var activeOrders = await _context.ServiceOrders.AsNoTracking()
            .Where(so => so.CustomerId == customerId && ownedSet.Contains(so.VehicleId))
            .Where(so => !TerminalOrderStatuses.Contains(so.Status))
            .OrderByDescending(so => so.UpdatedAt ?? so.CreatedAt)
            .Take(12)
            .Select(so => new
            {
                so.Id,
                so.Code,
                so.Status,
                so.VehicleId,
                so.ExecuteBranchId,
                so.EstimatedHandover,
                so.PromisedHandoverTime,
                so.ServiceAdvisorId
            })
            .ToListAsync(cancellationToken);

        var orderIds = activeOrders.Select(o => o.Id).ToList();
        var vehicleIds = activeOrders.Select(o => o.VehicleId).Distinct().ToList();

        var diagBusy = orderIds.Count == 0
            ? new HashSet<long>()
            : (await _context.Diagnoses.AsNoTracking()
                .Where(d => orderIds.Contains(d.ServiceOrderId))
                .Where(d =>
                    d.Status == nameof(DiagnosisStatus.PENDING) || d.Status == nameof(DiagnosisStatus.ASSIGNED))
                .Select(d => d.ServiceOrderId)
                .Distinct()
                .ToListAsync(cancellationToken)).ToHashSet();

        var waitingParts = orderIds.Count == 0
            ? new HashSet<long>()
            : (await _context.ServiceOrderDetails.AsNoTracking()
                .Where(d => orderIds.Contains(d.ServiceOrderId))
                .Where(d =>
                    d.Status != null &&
                    d.Status.ToUpper() == nameof(ServiceOrderDetailStatusCode.WAITING_PARTS))
                .Select(d => d.ServiceOrderId)
                .Distinct()
                .ToListAsync(cancellationToken)).ToHashSet();

        var vehicleMeta = await _context.Vehicles.AsNoTracking()
            .Where(v => vehicleIds.Contains(v.Id))
            .Select(v => new
            {
                v.Id,
                v.LicensePlate,
                ModelLabel = v.Model.Brand.Name + " " + v.Model.ModelName
            })
            .ToListAsync(cancellationToken);
        var vehicleMap = vehicleMeta.ToDictionary(x => x.Id);

        var branchIds = activeOrders.Select(o => o.ExecuteBranchId).Distinct().ToList();
        var branches = await _context.Branches.AsNoTracking()
            .Where(b => branchIds.Contains(b.Id))
            .Select(b => new { b.Id, b.Name })
            .ToListAsync(cancellationToken);
        var branchMap = branches.ToDictionary(b => b.Id);

        var advisorIds = activeOrders.Where(o => o.ServiceAdvisorId.HasValue).Select(o => o.ServiceAdvisorId!.Value).Distinct().ToList();
        var advisors = advisorIds.Count == 0
            ? new Dictionary<long, string>()
            : await _context.Staffs.AsNoTracking()
                .Where(s => advisorIds.Contains(s.Id))
                .Select(s => new { s.Id, s.FullName })
                .ToDictionaryAsync(x => x.Id, x => x.FullName ?? "", cancellationToken);

        var physicalGarage = await _locations.GetCustomerGarageAsync(customerId, cancellationToken);

        // Issue 4: resolve actual assigned technicians via ServiceStaffAssignments.
        var techByOrder = new Dictionary<long, List<string>>();
        if (orderIds.Count > 0)
        {
            var techRows = await (
                from ssa in _context.ServiceStaffAssignments.AsNoTracking()
                join sod in _context.ServiceOrderDetails.AsNoTracking() on ssa.ServiceOrderDetailId equals sod.Id
                where orderIds.Contains(sod.ServiceOrderId)
                where
                    (ssa.Status != null && DashboardOwnedAssignmentStatuses.Contains(ssa.Status)) ||
                    (ssa.Status == null && !ssa.ActualEndTime.HasValue)
                select new { sod.ServiceOrderId, ssa.TechnicianId }
            ).Distinct().ToListAsync(cancellationToken);

            var techIds = techRows.Select(r => r.TechnicianId).Distinct().ToList();
            var techNames = techIds.Count == 0
                ? new Dictionary<long, string>()
                : await _context.Staffs.AsNoTracking()
                    .Where(s => techIds.Contains(s.Id))
                    .Select(s => new { s.Id, s.FullName })
                    .ToDictionaryAsync(x => x.Id, x => x.FullName ?? "", cancellationToken);

            foreach (var row in techRows)
            {
                techNames.TryGetValue(row.TechnicianId, out var name);
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (!techByOrder.TryGetValue(row.ServiceOrderId, out var list))
                    techByOrder[row.ServiceOrderId] = list = new List<string>();
                if (!list.Contains(name))
                    list.Add(name);
            }
        }

        var tracks = new List<CustomerPortalActiveTrackDto>();
        foreach (var o in activeOrders)
        {
            var hasDiag = diagBusy.Contains(o.Id);
            var hasWp = waitingParts.Contains(o.Id);
            var stage = CustomerPortalStageMapper.ResolveStage(o.Status, hasDiag, hasWp);
            var canceled = CustomerPortalStageMapper.IsCanceledStatus(o.Status);
            var timeline = CustomerPortalStageMapper.BuildTimeline(stage, canceled);
            vehicleMap.TryGetValue(o.VehicleId, out var vm);
            branchMap.TryGetValue(o.ExecuteBranchId, out var br);
            var bay = physicalGarage.Vehicles.FirstOrDefault(vehicle => vehicle.VehicleId == o.VehicleId && vehicle.BranchId == o.ExecuteBranchId);
            string? adv = null;
            if (o.ServiceAdvisorId.HasValue)
                advisors.TryGetValue(o.ServiceAdvisorId.Value, out adv);
            techByOrder.TryGetValue(o.Id, out var techs);

            tracks.Add(new CustomerPortalActiveTrackDto
            {
                VehicleId = o.VehicleId,
                LicensePlate = vm?.LicensePlate ?? "",
                ModelLabel = vm?.ModelLabel,
                ServiceOrderId = o.Id,
                OrderCode = o.Code,
                LifecycleStatus = o.Status,
                CustomerStageCode = stage,
                CustomerStageLabel = CustomerPortalStageMapper.ToVietnameseLabel(stage),
                Timeline = timeline.Select(t => new CustomerPortalTimelineStepDto
                    { Code = t.Code, Label = t.Label, State = t.State }).ToList(),
                BranchId = o.ExecuteBranchId,
                BranchName = br?.Name,
                EstimatedHandover = o.EstimatedHandover,
                PromisedHandoverTime = o.PromisedHandoverTime,
                ServiceAdvisorName = adv,
                AssignedTechnicianNames = techs ?? new List<string>(),
                BayCode = bay?.BayCode,
                BayName = bay?.BayName,
                BayType = bay?.BayType,
                ServiceArea = bay?.ServiceArea,
                LocationReportedAt = bay?.ReportedAt,
                LocationReportedBy = bay?.ReportedBy,
                LocationNote = bay?.Note
            });
        }

        var now = VietnamTime.Now;
        var upcoming = await _context.Appointments.AsNoTracking()
            .Where(a => a.CustomerId == customerId)
            .Where(a => ActiveAppointmentStatuses.Contains(a.Status))
            .Where(a => a.StartTime >= now.AddHours(-1))
            .OrderBy(a => a.StartTime)
            .Take(8)
            .Select(a => new CustomerPortalUpcomingAppointmentDto
            {
                Id = a.Id,
                VehicleId = a.VehicleId,
                BranchId = a.BranchId,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                Status = a.Status
            })
            .ToListAsync(cancellationToken);

        foreach (var u in upcoming)
        {
            u.BranchName = await _context.Branches.AsNoTracking()
                .Where(b => b.Id == u.BranchId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync(cancellationToken);
            if (u.VehicleId.HasValue)
            {
                u.VehiclePlate = await _context.Vehicles.AsNoTracking()
                    .Where(v => v.Id == u.VehicleId.Value)
                    .Select(v => v.LicensePlate)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        CustomerPortalLastServiceDto? last = null;
        var lastRow = await _context.ServiceOrders.AsNoTracking()
            .Where(so => so.CustomerId == customerId)
            .Where(so => so.Status == nameof(ServiceOrderLifecycleStatus.CLOSED))
            .OrderByDescending(so => so.ActualHandover ?? so.UpdatedAt ?? so.CreatedAt)
            .Select(so => new
            {
                so.Id,
                so.Code,
                so.VehicleId,
                CompletedAt = so.ActualHandover ?? so.UpdatedAt ?? so.CreatedAt,
                so.ExecuteBranchId
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (lastRow != null)
        {
            var plate = await _context.Vehicles.AsNoTracking()
                .Where(v => v.Id == lastRow.VehicleId)
                .Select(v => v.LicensePlate)
                .FirstOrDefaultAsync(cancellationToken);
            var bname = await _context.Branches.AsNoTracking()
                .Where(b => b.Id == lastRow.ExecuteBranchId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync(cancellationToken);
            last = new CustomerPortalLastServiceDto
            {
                ServiceOrderId = lastRow.Id,
                OrderCode = lastRow.Code,
                VehicleId = lastRow.VehicleId,
                LicensePlate = plate,
                CompletedAt = lastRow.CompletedAt,
                BranchName = bname
            };
        }

        return new CustomerPortalDashboardDto
        {
            ActiveTracks = tracks,
            UpcomingAppointments = upcoming,
            LastCompletedService = last
        };
    }

    public async Task<IReadOnlyList<CustomerPortalVehicleDto>> GetVehiclesAsync(long customerId,
        CancellationToken cancellationToken = default)
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
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerPortalVehicleDto> CreateVehicleAsync(long customerId, CustomerPortalVehicleUpsertDto dto,
        CancellationToken cancellationToken = default)
    {
        var plate = NormalizeVehiclePlate(dto.LicensePlate);
        if (string.IsNullOrEmpty(plate))
            throw new InvalidOperationException("Biển số không hợp lệ.");

        var modelOk = await _context.VehicleModels.AsNoTracking()
            .AnyAsync(m => m.Id == dto.ModelId && m.IsDeleted != true, cancellationToken);
        if (!modelOk)
            throw new InvalidOperationException("Dòng xe không hợp lệ.");

        var plateLower = plate.ToLowerInvariant();
        var existingVehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.IsDeleted != true && v.LicensePlate.ToLower() == plateLower, cancellationToken);

        if (existingVehicle != null)
        {
            var otherOwns = await _context.CustomerVehicles.AsNoTracking()
                .AnyAsync(cv =>
                        cv.VehicleId == existingVehicle.Id &&
                        cv.CustomerId != customerId &&
                        cv.IsDeleted != true &&
                        (cv.IsCurrentOwner ?? true),
                    cancellationToken);
            if (otherOwns)
                throw new InvalidOperationException("Biển số đã thuộc khách hàng khác.");

            if (!string.IsNullOrWhiteSpace(dto.Vin))
            {
                var vinNorm = NormalizeVehicleVin(dto.Vin);
                if (!string.Equals(existingVehicle.Vin, vinNorm, StringComparison.OrdinalIgnoreCase) &&
                    await _context.Vehicles.AsNoTracking()
                        .AnyAsync(v =>
                            v.Id != existingVehicle.Id &&
                            v.IsDeleted != true &&
                            v.Vin != null &&
                            v.Vin.ToLower() == vinNorm.ToLowerInvariant(), cancellationToken))
                    throw new InvalidOperationException("Số VIN đã tồn tại trên xe khác.");
            }

            var myLink = await _context.CustomerVehicles
                .FirstOrDefaultAsync(cv =>
                    cv.CustomerId == customerId && cv.VehicleId == existingVehicle.Id && cv.IsDeleted != true,
                    cancellationToken);
            if (myLink == null)
            {
                myLink = new CustomerVehicle
                {
                    CustomerId = customerId,
                    VehicleId = existingVehicle.Id,
                    IsCurrentOwner = true,
                    OwnershipStartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
                    CreatedAt = VietnamTime.Now
                };
                _context.CustomerVehicles.Add(myLink);
            }
            else
            {
                myLink.IsCurrentOwner = true;
                myLink.UpdatedAt = VietnamTime.Now;
            }

            existingVehicle.Vin = string.IsNullOrWhiteSpace(dto.Vin) ? existingVehicle.Vin : NormalizeVehicleVin(dto.Vin);
            existingVehicle.ModelId = dto.ModelId;
            existingVehicle.Color = dto.Color?.Trim();
            existingVehicle.ManufactureYear = dto.ManufactureYear ?? existingVehicle.ManufactureYear;
            existingVehicle.Odo = dto.Odo ?? existingVehicle.Odo;
            existingVehicle.OdoLastUpdatedAt = dto.Odo.HasValue ? VietnamTime.Now : existingVehicle.OdoLastUpdatedAt;
            existingVehicle.UpdatedAt = VietnamTime.Now;

            await _context.SaveChangesAsync(cancellationToken);
            return await GetSingleVehicleDtoAsync(customerId, existingVehicle.Id, cancellationToken)
                   ?? throw new InvalidOperationException("Không tải được thông tin xe.");
        }

        if (!string.IsNullOrWhiteSpace(dto.Vin))
        {
            var vinNorm = NormalizeVehicleVin(dto.Vin);
            if (await _context.Vehicles.AsNoTracking()
                    .AnyAsync(v => v.IsDeleted != true && v.Vin != null && v.Vin.ToLower() == vinNorm.ToLowerInvariant(),
                        cancellationToken))
                throw new InvalidOperationException("Số VIN đã tồn tại.");
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        var newVehicleId = await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync(cancellationToken);
            try
            {
                var now = VietnamTime.Now;
                var vehicle = new Vehicle
                {
                    LicensePlate = plate,
                    Vin = string.IsNullOrWhiteSpace(dto.Vin) ? null : NormalizeVehicleVin(dto.Vin),
                    ModelId = dto.ModelId,
                    Color = dto.Color?.Trim(),
                    ManufactureYear = dto.ManufactureYear,
                    Odo = dto.Odo,
                    OdoLastUpdatedAt = dto.Odo.HasValue ? now : null,
                    CreatedAt = now
                };

                _context.Vehicles.Add(vehicle);
                _context.CustomerVehicles.Add(new CustomerVehicle
                {
                    CustomerId = customerId,
                    Vehicle = vehicle,
                    IsCurrentOwner = true,
                    OwnershipStartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
                    CreatedAt = now
                });
                await _context.SaveChangesAsync(cancellationToken);

                if (tx is not null)
                    await tx.CommitAsync(cancellationToken);

                return vehicle.Id;
            }
            catch
            {
                if (tx is not null)
                    await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });

        return await GetSingleVehicleDtoAsync(customerId, newVehicleId, cancellationToken)
               ?? throw new InvalidOperationException("Không tải được thông tin xe.");
    }

    public async Task<CustomerPortalVehicleDto?> UpdateVehicleAsync(long customerId, long vehicleId,
        CustomerPortalVehicleUpsertDto dto, CancellationToken cancellationToken = default)
    {
        var owns = await OwnsVehicleAsync(customerId, vehicleId, cancellationToken);
        if (!owns) return null;

        var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId && v.IsDeleted != true,
            cancellationToken);
        if (vehicle == null) return null;

        var newPlate = NormalizeVehiclePlate(dto.LicensePlate);
        var newPlateLower = newPlate.ToLowerInvariant();
        if (await _context.Vehicles.AsNoTracking()
                .AnyAsync(v => v.Id != vehicleId && v.IsDeleted != true && v.LicensePlate.ToLower() == newPlateLower,
                    cancellationToken))
            throw new InvalidOperationException("Biển số đã được sử dụng.");

        if (!string.IsNullOrWhiteSpace(dto.Vin))
        {
            var vinNorm = NormalizeVehicleVin(dto.Vin);
            if (await _context.Vehicles.AsNoTracking()
                    .AnyAsync(v =>
                        v.Id != vehicleId && v.IsDeleted != true && v.Vin != null &&
                        v.Vin.ToLower() == vinNorm.ToLowerInvariant(), cancellationToken))
                throw new InvalidOperationException("Số VIN đã tồn tại trên xe khác.");
        }

        var modelOk = await _context.VehicleModels.AsNoTracking()
            .AnyAsync(m => m.Id == dto.ModelId && m.IsDeleted != true, cancellationToken);
        if (!modelOk)
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
        await _context.SaveChangesAsync(cancellationToken);
        return await GetSingleVehicleDtoAsync(customerId, vehicleId, cancellationToken);
    }

    public async Task<CustomerPortalVehicleHistoryDto?> GetVehicleHistoryAsync(long customerId, long vehicleId,
        CancellationToken cancellationToken = default)
    {
        if (!await OwnsVehicleAsync(customerId, vehicleId, cancellationToken))
            return null;

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
            .ToListAsync(cancellationToken);

        var orderIds = orders.Select(o => o.Id).ToList();
        var details = new List<(long ServiceOrderId, string? ItemName, string? ItemType)>();
        if (orderIds.Count > 0)
        {
            var detailRows = await _context.ServiceOrderDetails.AsNoTracking()
                .Where(d => orderIds.Contains(d.ServiceOrderId))
                .Select(d => new { d.ServiceOrderId, d.ItemName, d.ItemType })
                .ToListAsync(cancellationToken);
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
                .ToListAsync(cancellationToken);
            foreach (var g in invRows.GroupBy(x => x.ServiceOrderId!.Value))
                invMap[g.Key] = g.OrderByDescending(x => x.Id).Select(x => (x.Id, x.InvoiceUrl, x.PaymentStatus)).First();
        }

        var branchIds = orders.Select(o => o.ExecuteBranchId).Distinct().ToList();
        var branchMap = await _context.Branches.AsNoTracking()
            .Where(b => branchIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Name, cancellationToken);

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
                .ToListAsync(cancellationToken)).ToHashSet();

            wpLineSet = (await _context.ServiceOrderDetails.AsNoTracking()
                .Where(d => orderIds.Contains(d.ServiceOrderId))
                .Where(d =>
                    d.Status == nameof(ServiceOrderDetailStatusCode.WAITING_PARTS))
                .Select(d => d.ServiceOrderId)
                .Distinct()
                .ToListAsync(cancellationToken)).ToHashSet();
        }

        // Issue 4 (history): expose VehicleConditionLogs (check-in / check-out evidence).
        var conditionLogMap = new Dictionary<long, List<CustomerPortalConditionLogDto>>();
        if (orderIds.Count > 0)
        {
            var condRows = await _context.VehicleConditionLogs.AsNoTracking()
                .Where(l => orderIds.Contains(l.ServiceOrderId) && l.LogType != "REPAIR_3D")
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
                .ToListAsync(cancellationToken);

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

        var hints = await BuildMaintenanceHintsAsync(vehicleId, cancellationToken);

        return new CustomerPortalVehicleHistoryDto { Items = items, MaintenanceHints = hints };
    }

    public async Task<(IReadOnlyList<CustomerPortalNotificationDto> items, int unreadCount, int totalCount)> GetNotificationsAsync(
        long customerId, bool? unreadOnly, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var q = _context.Notifications.AsNoTracking()
            .Where(n => n.CustomerId == customerId);
        if (unreadOnly == true)
            q = q.Where(n => n.IsRead != true);

        var unreadCount = await _context.Notifications.AsNoTracking()
            .CountAsync(n => n.CustomerId == customerId && n.IsRead != true, cancellationToken);

        var totalCount = await q.CountAsync(cancellationToken);
        var offset = ((long)page - 1) * pageSize;
        if (offset >= totalCount)
            return (Array.Empty<CustomerPortalNotificationDto>(), unreadCount, totalCount);

        var list = await q
            .OrderByDescending(n => n.SentAt)
            .ThenByDescending(n => n.Id)
            .Skip((int)offset).Take(pageSize)
            .Select(n => new CustomerPortalNotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Content = n.Content,
                IsRead = n.IsRead == true,
                SentAt = n.SentAt,
                LinkAction = n.LinkAction,
                DerivedType = ""
            })
            .ToListAsync(cancellationToken);

        foreach (var n in list)
            n.DerivedType = DeriveNotificationType(n.LinkAction);

        return (list, unreadCount, totalCount);
    }

    public async Task<bool> MarkNotificationReadAsync(long customerId, long notificationId,
        CancellationToken cancellationToken = default)
    {
        var n = await _context.Notifications.FirstOrDefaultAsync(
            x => x.Id == notificationId && x.CustomerId == customerId, cancellationToken);
        if (n == null) return false;
        n.IsRead = true;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> MarkAllNotificationsReadAsync(long customerId, CancellationToken cancellationToken = default)
    {
        if (_context.Database.IsRelational())
        {
            return await _context.Notifications
                .Where(n => n.CustomerId == customerId && n.IsRead != true)
                .ExecuteUpdateAsync(update => update.SetProperty(n => n.IsRead, true), cancellationToken);
        }
        var rows = await _context.Notifications
            .Where(n => n.CustomerId == customerId && n.IsRead != true)
            .ToListAsync(cancellationToken);
        foreach (var n in rows)
            n.IsRead = true;
        if (rows.Count > 0)
            await _context.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }

    public async Task<CustomerPortalVehicleLookupDto> GetVehicleLookupsAsync(CancellationToken cancellationToken = default)
    {
        var brands = await _context.VehicleBrands.AsNoTracking()
            .Where(b => b.IsDeleted != true)
            .OrderBy(b => b.Name)
            .Select(b => new CustomerPortalBrandLookupDto { Id = b.Id, Name = b.Name })
            .ToListAsync(cancellationToken);

        var models = await _context.VehicleModels.AsNoTracking()
            .Where(m => m.IsDeleted != true)
            .OrderBy(m => m.ModelName)
            .Select(m => new CustomerPortalModelLookupDto
            {
                Id = m.Id,
                BrandId = m.BrandId,
                BrandName = m.Brand.Name,
                ModelName = m.ModelName
            })
            .ToListAsync(cancellationToken);

        return new CustomerPortalVehicleLookupDto { Brands = brands, Models = models };
    }

    public async Task<CustomerPortalServiceOrderLinkContextDto?> GetServiceOrderLinkContextAsync(
        long customerId,
        long serviceOrderId,
        CancellationToken cancellationToken = default)
    {
        var row = await (
            from so in _context.ServiceOrders.AsNoTracking()
            join cv in _context.CustomerVehicles.AsNoTracking() on so.VehicleId equals cv.VehicleId
            where so.Id == serviceOrderId
            where so.CustomerId == customerId
            where cv.CustomerId == customerId
            where cv.IsDeleted != true
            where cv.IsCurrentOwner != false
            select new
            {
                so.Id,
                so.VehicleId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row == null)
            return null;

        return new CustomerPortalServiceOrderLinkContextDto
        {
            ServiceOrderId = row.Id,
            VehicleId = row.VehicleId,
            SuggestedTab = "history",
            TargetPath = $"/my-vehicles/{row.VehicleId}?tab=history&serviceOrderId={row.Id}"
        };
    }

    private async Task<bool> OwnsVehicleAsync(long customerId, long vehicleId, CancellationToken cancellationToken) =>
        await _context.CustomerVehicles.AsNoTracking()
            .AnyAsync(cv =>
                cv.CustomerId == customerId &&
                cv.VehicleId == vehicleId &&
                cv.IsDeleted != true &&
                (cv.IsCurrentOwner ?? true), cancellationToken);

    private async Task<CustomerPortalVehicleDto?> GetSingleVehicleDtoAsync(long customerId, long vehicleId,
        CancellationToken cancellationToken) =>
        await _context.CustomerVehicles.AsNoTracking()
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
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<List<CustomerPortalMaintenanceHintDto>> BuildMaintenanceHintsAsync(long vehicleId,
        CancellationToken cancellationToken)
    {
        var hints = new List<CustomerPortalMaintenanceHintDto>();
        var settings = _jobOptions.Value.MaintenanceOutreach;
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        var completedStatuses = settings.CompletedServiceStatuses.Count > 0
            ? settings.CompletedServiceStatuses.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : CompletedForMaintenance;

        var lastCompleted = await _context.ServiceOrders.AsNoTracking()
            .Where(so => so.VehicleId == vehicleId)
            .Where(so => completedStatuses.Contains(so.Status))
            .OrderByDescending(so => so.ActualHandover ?? so.UpdatedAt ?? so.CreatedAt)
            .Select(so => so.ActualHandover ?? so.UpdatedAt ?? so.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (lastCompleted.HasValue)
        {
            var days = (now - lastCompleted.Value).TotalDays;
            if (days >= settings.LastCompletedServiceAgeDays)
            {
                hints.Add(new CustomerPortalMaintenanceHintDto
                {
                    Kind = "LAST_SERVICE_AGE",
                    Message =
                        $"Đã khoảng {Math.Round(days)} ngày kể từ lần bảo dưỡng/sửa chữa gần nhất (ngưỡng gợi ý: {settings.LastCompletedServiceAgeDays} ngày).",
                    PredictedDate = null
                });
            }
        }

        var vehicleOdo = await _context.Vehicles.AsNoTracking()
            .Where(v => v.Id == vehicleId)
            .Select(v => v.Odo)
            .FirstOrDefaultAsync(cancellationToken);
        var lastOdoLog = await _context.VehicleConditionLogs.AsNoTracking()
            .Where(l => l.ServiceOrder != null && l.ServiceOrder.VehicleId == vehicleId)
            .Where(l => l.OdoReading.HasValue)
            .OrderByDescending(l => l.Id)
            .Select(l => l.OdoReading)
            .FirstOrDefaultAsync(cancellationToken);
        if (vehicleOdo.HasValue && lastOdoLog.HasValue)
        {
            var delta = vehicleOdo.Value - lastOdoLog.Value;
            if (delta >= settings.OdoDeltaThreshold)
            {
                hints.Add(new CustomerPortalMaintenanceHintDto
                {
                    Kind = "ODO_DELTA",
                    Message =
                        $"Hành trình từ số ODO ghi nhận gần nhất tăng khoảng {delta} km (ngưỡng gợi ý: {settings.OdoDeltaThreshold} km).",
                    PredictedDate = null
                });
            }
        }

        var forecastTypes = settings.ForecastTypes.Count > 0
            ? settings.ForecastTypes.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MAINTENANCE", "SERVICE", "INSPECTION" };
        var forecastEnd = today.AddDays(settings.ForecastLookaheadDays);
        var fc = await _context.AiForecasts.AsNoTracking()
            .Where(f => f.VehicleId == vehicleId)
            .Where(f => f.PredictedDate <= forecastEnd && f.PredictedDate >= today)
            .Where(f => f.Type != null && forecastTypes.Contains(f.Type))
            .OrderBy(f => f.PredictedDate)
            .Select(f => new { f.Type, f.PredictedDate })
            .FirstOrDefaultAsync(cancellationToken);
        if (fc != null)
        {
            hints.Add(new CustomerPortalMaintenanceHintDto
            {
                Kind = "AI_FORECAST",
                Message = $"Dự báo ({fc.Type}): nên xếp lịch trước ngày {fc.PredictedDate:yyyy-MM-dd}.",
                PredictedDate = fc.PredictedDate.ToDateTime(TimeOnly.MinValue)
            });
        }

        return hints;
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

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        if (_context.Database.IsRelational())
            return await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        return await _context.Database.BeginTransactionAsync(cancellationToken);
    }
}
