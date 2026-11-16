using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Authorization;
using MyGarage.Application.Bays;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed partial class VehicleLocationService
{
    private const string RepairLogType = "REPAIR_3D";
    private static readonly HashSet<string> RepairParts = ["door-right-front", "door-left-front", "door-right-rear", "door-left-rear", "tyre-right-front", "tyre-left-front", "tyre-right-rear", "tyre-left-rear", "windshield", "rear-glass", "hood", "trunk", "bumper-front", "bumper-rear", "fender-right-front", "fender-left-front"];

    public async Task ReportRepairAsync(long serviceOrderId, VehicleRepairReportInput input, UserActorContext actor, CancellationToken ct = default)
    {
        var branchId = await RequireTechnicianAsync(actor, ct);
        if (!RepairParts.Contains(input.PartId ?? "") || string.IsNullOrWhiteSpace(input.Note) || input.Note.Length > 500)
            throw new ArgumentException("Chọn bộ phận và nhập nội dung từ 1 đến 500 ký tự.");
        if (!await AssignedOrders(actor.UserId!.Value).AnyAsync(order => order.Id == serviceOrderId && order.ExecuteBranchId == branchId, ct))
            throw new UnauthorizedAccessException("Xe không thuộc công việc được giao tại chi nhánh của bạn.");
        // Append reports to the existing vehicle condition history; latest report per part wins.
        db.VehicleConditionLogs.Add(new VehicleConditionLog
        {
            ServiceOrderId = serviceOrderId, LogType = RepairLogType,
            DefectsAndImages = JsonSerializer.Serialize(input with { Note = input.Note.Trim() }),
            InspectorId = actor.UserId, CreatedBy = actor.UserId, CreatedAt = VietnamTime.Now,
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task<Dictionary<long, IReadOnlyList<VehicleRepairDto>>> ReadRepairsAsync(List<long> orderIds, CancellationToken ct)
    {
        var logs = await db.VehicleConditionLogs.AsNoTracking()
            .Where(log => orderIds.Contains(log.ServiceOrderId) && log.LogType == RepairLogType)
            .OrderByDescending(log => log.Id)
            .Select(log => new { log.ServiceOrderId, log.DefectsAndImages, log.CreatedAt, Technician = log.Inspector == null ? "Kỹ thuật viên" : log.Inspector.FullName }).ToListAsync(ct);
        var result = new Dictionary<long, IReadOnlyList<VehicleRepairDto>>();
        foreach (var group in logs.GroupBy(log => log.ServiceOrderId))
        {
            var seen = new HashSet<string>();
            var active = new List<VehicleRepairDto>();
            foreach (var log in group)
            {
                VehicleRepairReportInput? report;
                try { report = JsonSerializer.Deserialize<VehicleRepairReportInput>(log.DefectsAndImages ?? "null"); }
                catch (JsonException) { continue; }
                if (report == null || !RepairParts.Contains(report.PartId ?? "") || !seen.Add(report.PartId!)) continue;
                if (!report.Completed) active.Add(new(report.PartId!, report.Note, log.Technician, log.CreatedAt));
            }
            result[group.Key] = active;
        }
        return result;
    }
}
