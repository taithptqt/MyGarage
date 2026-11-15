using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.DTOs.Requests;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfQcInspectionDataService(IGarageContext db) : IQcInspectionDataService
{
    private const string QcResultPass = "PASS";
    private const string QcResultFail = "FAIL";
    private const string QcResultNa = "N/A";
    private const string QcStatusPass = nameof(QcInspectionStatusCode.PASS);
    private const string QcStatusFail = nameof(QcInspectionStatusCode.FAIL);
    private const string ServiceOrderQcPending = nameof(ServiceOrderQcStatusCode.PENDING);
    private const string ServiceOrderQcPass = nameof(ServiceOrderQcStatusCode.PASS);
    private const string ServiceOrderQcFail = nameof(ServiceOrderQcStatusCode.FAIL);

    public async Task<(IReadOnlyList<QcInspection> Items, int Total)> GetPagedByServiceOrderAsync(
        long serviceOrderId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = db.QcInspections.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .OrderByDescending(x => x.Id);
        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var paged = offset >= total
            ? new List<QcInspection>()
            : await query.Skip((int)offset).Take(pageSize).ToListAsync(ct);
        return (paged, total);
    }

    public async Task<long?> GetServiceOrderIdAsync(long inspectionId, CancellationToken ct = default)
    {
        return await db.QcInspections.AsNoTracking()
            .Where(x => x.Id == inspectionId)
            .Select(x => (long?)x.ServiceOrderId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> IsTechnicianWhoWorkedOnOrderAsync(long serviceOrderId, long staffId, CancellationToken ct = default)
    {
        if (serviceOrderId <= 0 || staffId <= 0) return false;

        var workedOnOrder = await db.ServiceStaffAssignments
            .AsNoTracking()
            .AnyAsync(a => a.ServiceOrderDetail.ServiceOrderId == serviceOrderId && a.TechnicianId == staffId, ct);

        if (!workedOnOrder)
        {
            workedOnOrder = await db.ServiceOrderDetails
                .AsNoTracking()
                .AnyAsync(d => d.ServiceOrderId == serviceOrderId && d.TechnicianId == staffId, ct);
        }

        return workedOnOrder;
    }

    public async Task<object?> UpsertChecklistItemsAsync(
        long inspectionId,
        bool overwriteExisting,
        IReadOnlyList<(QcInspectionChecklistItemUpsertDto Input, string ItemName, string? NormalizedResult)> validatedItems,
        CancellationToken ct = default)
    {
        var inspection = await db.QcInspections.FirstOrDefaultAsync(x => x.Id == inspectionId, ct);
        if (inspection is null)
        {
            return null;
        }

        var now = VietnamTime.Now;

        if (overwriteExisting)
        {
            var existingRows = await db.QcChecklistItems
                .Where(x => x.QcInspectionId == inspectionId)
                .ToListAsync(ct);
            if (existingRows.Count > 0)
            {
                db.QcChecklistItems.RemoveRange(existingRows);
            }

            foreach (var (dto, itemName, normalizedResult) in validatedItems)
            {
                var row = new QcChecklistItem { QcInspectionId = inspectionId };
                db.QcChecklistItems.Add(row);
                ApplyChecklistItemFields(row, dto, itemName, normalizedResult, now);
            }
        }
        else
        {
            var itemIds = validatedItems
                .Where(x => x.Input.Id.HasValue && x.Input.Id.Value > 0)
                .Select(x => x.Input.Id!.Value)
                .Distinct()
                .ToList();

            var existingItemDict = itemIds.Count > 0
                ? await db.QcChecklistItems
                    .Where(x => x.QcInspectionId == inspectionId && itemIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, ct)
                : new Dictionary<long, QcChecklistItem>();

            foreach (var (dto, itemName, normalizedResult) in validatedItems)
            {
                QcChecklistItem row;
                if (dto.Id.HasValue && dto.Id.Value > 0 && existingItemDict.TryGetValue(dto.Id.Value, out var foundRow))
                {
                    row = foundRow;
                }
                else
                {
                    row = new QcChecklistItem { QcInspectionId = inspectionId };
                    db.QcChecklistItems.Add(row);
                }

                ApplyChecklistItemFields(row, dto, itemName, normalizedResult, now);
            }
        }

        await db.SaveChangesAsync(ct);
        return await UpdateQcInspectionSummaryAsync(inspectionId, ct);
    }

    private static void ApplyChecklistItemFields(
        QcChecklistItem row,
        QcInspectionChecklistItemUpsertDto dto,
        string itemName,
        string? normalizedResult,
        DateTime now)
    {
        row.TemplateItemId = dto.TemplateItemId;
        row.ItemName = itemName;
        row.Category = string.IsNullOrWhiteSpace(dto.Category) ? null : dto.Category.Trim();
        row.IsRequired = dto.IsRequired ?? true;
        row.Result = normalizedResult;
        row.FailNote = string.IsNullOrWhiteSpace(dto.FailNote) ? null : dto.FailNote.Trim();
        row.PhotoUrl = string.IsNullOrWhiteSpace(dto.PhotoUrl) ? null : dto.PhotoUrl.Trim();
        row.CheckedBy = dto.CheckedBy;
        row.CheckedAt = dto.CheckedAt ?? (normalizedResult is null ? null : now);
    }

    private async Task<object> UpdateQcInspectionSummaryAsync(long inspectionId, CancellationToken ct)
    {
        var inspection = await db.QcInspections
            .Include(x => x.QcChecklistItems)
            .FirstOrDefaultAsync(x => x.Id == inspectionId, ct);
        if (inspection is null)
        {
            return new { inspectionId, total = 0 };
        }

        var items = inspection.QcChecklistItems.ToList();
        var requiredItems = items.Where(x => x.IsRequired).ToList();
        var passOrNaRequired = requiredItems.Count(x =>
            string.Equals(x.Result, QcResultPass, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.Result, QcResultNa, StringComparison.OrdinalIgnoreCase));
        var failRequired = requiredItems.Count(x =>
            string.Equals(x.Result, QcResultFail, StringComparison.OrdinalIgnoreCase));
        var checkedRequired = requiredItems.Count(x => !string.IsNullOrWhiteSpace(x.Result));

        decimal? score = null;
        if (requiredItems.Count > 0)
        {
            score = Math.Round((decimal)passOrNaRequired * 100m / requiredItems.Count, 2, MidpointRounding.AwayFromZero);
        }

        if (failRequired > 0)
        {
            inspection.Status = QcStatusFail;
            inspection.InspectedAt = VietnamTime.Now;
        }
        else if (requiredItems.Count > 0 && checkedRequired == requiredItems.Count)
        {
            inspection.Status = QcStatusPass;
            inspection.InspectedAt = VietnamTime.Now;
        }

        inspection.OverallScore = score;

        var serviceOrder = await db.ServiceOrders.FirstOrDefaultAsync(x => x.Id == inspection.ServiceOrderId, ct);
        if (serviceOrder != null)
        {
            if (string.Equals(inspection.Status, QcStatusFail, StringComparison.OrdinalIgnoreCase))
            {
                serviceOrder.Qcstatus = ServiceOrderQcFail;
            }
            else if (string.Equals(inspection.Status, QcStatusPass, StringComparison.OrdinalIgnoreCase))
            {
                serviceOrder.Qcstatus = ServiceOrderQcPass;
                serviceOrder.QcverifiedAt = inspection.InspectedAt ?? VietnamTime.Now;
            }
            else
            {
                serviceOrder.Qcstatus = ServiceOrderQcPending;
            }

            serviceOrder.QcinspectorId = inspection.QcStaffId;
            serviceOrder.UpdatedAt = VietnamTime.Now;
        }

        await db.SaveChangesAsync(ct);

        return new
        {
            inspectionId = inspection.Id,
            status = inspection.Status,
            overallScore = inspection.OverallScore,
            inspectedAt = inspection.InspectedAt,
            total = items.Count,
            required = requiredItems.Count,
            checkedRequired,
            failedRequired = failRequired,
            passedOrNaRequired = passOrNaRequired
        };
    }
}
