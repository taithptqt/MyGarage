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
    public async Task<CustomerPortalDashboardDto> GetDashboardAsync(long customerId, CancellationToken ct = default)
    {
        var ownedVehicleIds = await _context.CustomerVehicles.AsNoTracking()
            .Where(cv => cv.CustomerId == customerId && cv.IsDeleted != true && (cv.IsCurrentOwner ?? true))
            .Select(cv => cv.VehicleId)
            .Distinct()
            .ToListAsync(ct);
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
            .ToListAsync(ct);

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
                .ToListAsync(ct)).ToHashSet();

        var waitingParts = orderIds.Count == 0
            ? new HashSet<long>()
            : (await _context.ServiceOrderDetails.AsNoTracking()
                .Where(d => orderIds.Contains(d.ServiceOrderId))
                .Where(d =>
                    d.Status != null &&
                    d.Status.ToUpper() == nameof(ServiceOrderDetailStatusCode.WAITING_PARTS))
                .Select(d => d.ServiceOrderId)
                .Distinct()
                .ToListAsync(ct)).ToHashSet();

        var vehicleMeta = await _context.Vehicles.AsNoTracking()
            .Where(v => vehicleIds.Contains(v.Id))
            .Select(v => new
            {
                v.Id,
                v.LicensePlate,
                ModelLabel = v.Model.Brand.Name + " " + v.Model.ModelName
            })
            .ToListAsync(ct);
        var vehicleMap = vehicleMeta.ToDictionary(x => x.Id);

        var branchIds = activeOrders.Select(o => o.ExecuteBranchId).Distinct().ToList();
        var branches = await _context.Branches.AsNoTracking()
            .Where(b => branchIds.Contains(b.Id))
            .Select(b => new { b.Id, b.Name })
            .ToListAsync(ct);
        var branchMap = branches.ToDictionary(b => b.Id);

        var advisorIds = activeOrders.Where(o => o.ServiceAdvisorId.HasValue).Select(o => o.ServiceAdvisorId!.Value).Distinct().ToList();
        var advisors = advisorIds.Count == 0
            ? new Dictionary<long, string>()
            : await _context.Staffs.AsNoTracking()
                .Where(s => advisorIds.Contains(s.Id))
                .Select(s => new { s.Id, s.FullName })
                .ToDictionaryAsync(x => x.Id, x => x.FullName ?? "", ct);

        var slotByOrder = new Dictionary<long, (string? Code, string? Name)>();
        if (orderIds.Count > 0)
        {
            var slotQ = await (
                from w in _context.WorkSlots.AsNoTracking()
                join b in _context.Bays.AsNoTracking() on w.BayId equals b.Id into bj
                from b in bj.DefaultIfEmpty()
                where w.ServiceOrderId != null && orderIds.Contains(w.ServiceOrderId.Value)
                where w.BayId != null && b != null
                where !TerminalWorkSlotStatuses.Contains(w.Status)
                orderby w.StartTime descending
                select new { w.ServiceOrderId, BayCode = b!.Code, BayName = b.Name }
            ).ToListAsync(ct);
            foreach (var row in slotQ)
            {
                if (row.ServiceOrderId == null) continue;
                var oid = row.ServiceOrderId.Value;
                if (!slotByOrder.ContainsKey(oid))
                    slotByOrder[oid] = (row.BayCode, row.BayName);
            }
        }

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
            ).Distinct().ToListAsync(ct);

            var techIds = techRows.Select(r => r.TechnicianId).Distinct().ToList();
            var techNames = techIds.Count == 0
                ? new Dictionary<long, string>()
                : await _context.Staffs.AsNoTracking()
                    .Where(s => techIds.Contains(s.Id))
                    .Select(s => new { s.Id, s.FullName })
                    .ToDictionaryAsync(x => x.Id, x => x.FullName ?? "", ct);

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
            slotByOrder.TryGetValue(o.Id, out var bay);
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
                BayCode = bay.Code,
                BayName = bay.Name
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
            .ToListAsync(ct);

        foreach (var u in upcoming)
        {
            u.BranchName = await _context.Branches.AsNoTracking()
                .Where(b => b.Id == u.BranchId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync(ct);
            if (u.VehicleId.HasValue)
            {
                u.VehiclePlate = await _context.Vehicles.AsNoTracking()
                    .Where(v => v.Id == u.VehicleId.Value)
                    .Select(v => v.LicensePlate)
                    .FirstOrDefaultAsync(ct);
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
            .FirstOrDefaultAsync(ct);
        if (lastRow != null)
        {
            var plate = await _context.Vehicles.AsNoTracking()
                .Where(v => v.Id == lastRow.VehicleId)
                .Select(v => v.LicensePlate)
                .FirstOrDefaultAsync(ct);
            var bname = await _context.Branches.AsNoTracking()
                .Where(b => b.Id == lastRow.ExecuteBranchId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync(ct);
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


    private async Task<List<CustomerPortalMaintenanceHintDto>> BuildMaintenanceHintsAsync(
        long vehicleId,
        ProactiveMaintenanceOutreachOptions settings,
        CancellationToken ct)
    {
        var hints = new List<CustomerPortalMaintenanceHintDto>();
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
            .FirstOrDefaultAsync(ct);
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
            .FirstOrDefaultAsync(ct);
        var lastOdoLog = await _context.VehicleConditionLogs.AsNoTracking()
            .Where(l => l.ServiceOrder != null && l.ServiceOrder.VehicleId == vehicleId)
            .Where(l => l.OdoReading.HasValue)
            .OrderByDescending(l => l.Id)
            .Select(l => l.OdoReading)
            .FirstOrDefaultAsync(ct);
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
            .FirstOrDefaultAsync(ct);
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
}
