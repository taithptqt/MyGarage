using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Common;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfDiagnosisRecommendationDataService(IGarageContext context) : IDiagnosisRecommendationDataService
{
    public async Task<(IReadOnlyList<DiagnosisRecommendation> Items, int Total)> GetPagedAsync(
        long? diagnosisId,
        long? diagnosisAssignmentId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = context.DiagnosisRecommendations.AsNoTracking().AsQueryable();
        if (diagnosisAssignmentId.HasValue && diagnosisAssignmentId.Value > 0)
        {
            query = query.Where(x => x.DiagnosisAssignmentId == diagnosisAssignmentId.Value);
        }
        else if (diagnosisId.HasValue && diagnosisId.Value > 0)
        {
            query = query.Where(x => x.DiagnosisId == diagnosisId.Value);
        }

        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var items = offset >= total
            ? new List<DiagnosisRecommendation>()
            : await query.OrderBy(x => x.Id).Skip((int)offset).Take(pageSize).ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<PendingSupplementaryProposalResponseDto>> GetPendingSupplementaryAsync(
        long? serviceOrderId,
        long? branchId,
        CancellationToken ct = default)
    {
        var orderQuery = context.ServiceOrders.AsNoTracking().AsQueryable();
        if (serviceOrderId.HasValue && serviceOrderId.Value > 0)
        {
            orderQuery = orderQuery.Where(x => x.Id == serviceOrderId.Value);
        }

        if (branchId.HasValue && branchId.Value > 0)
        {
            orderQuery = orderQuery.Where(x => x.ExecuteBranchId == branchId.Value || x.OriginBranchId == branchId.Value);
        }

        var candidateOrderIds = await orderQuery
            .Select(x => x.Id)
            .Distinct()
            .ToListAsync(ct);
        if (candidateOrderIds.Count == 0)
        {
            return Array.Empty<PendingSupplementaryProposalResponseDto>();
        }

        var latestDiagnosisRows = await context.Diagnoses.AsNoTracking()
            .Where(x => candidateOrderIds.Contains(x.ServiceOrderId))
            .Where(x => x.Status != nameof(DiagnosisStatus.CANCELED))
            .Select(x => new { x.ServiceOrderId, x.Id, x.UpdatedAt, x.CreatedAt })
            .ToListAsync(ct);

        var latestDiagnosisByOrder = latestDiagnosisRows
            .GroupBy(x => x.ServiceOrderId)
            .Select(g => g.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue)
                .ThenByDescending(x => x.Id).First())
            .ToList();

        var latestDiagnosisIds = latestDiagnosisByOrder
            .Select(x => x!.Id)
            .Distinct()
            .ToList();
        if (latestDiagnosisIds.Count == 0)
        {
            return Array.Empty<PendingSupplementaryProposalResponseDto>();
        }

        var orderIdByDiagnosisId = latestDiagnosisByOrder
            .ToDictionary(x => x!.Id, x => x!.ServiceOrderId);

        var recommendations = await context.DiagnosisRecommendations.AsNoTracking()
            .Where(x => latestDiagnosisIds.Contains(x.DiagnosisId))
            .Where(x => x.RecommendationStatus != nameof(RecommendationLifecycleStatusCode.SOFT_DELETED_BY_TL))
            .Select(x => new
            {
                x.Id,
                x.DiagnosisId,
                x.DiagnosisAssignmentId,
                ProposalAt = x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue
            })
            .ToListAsync(ct);
        if (recommendations.Count == 0)
        {
            return Array.Empty<PendingSupplementaryProposalResponseDto>();
        }

        var recommendationIds = recommendations.Select(x => x.Id).ToList();
        var quotedRecommendationIds = (await context.QuotationDetails.AsNoTracking()
            .Where(x => x.DiagnosisRecommendationId.HasValue && recommendationIds.Contains(x.DiagnosisRecommendationId.Value))
            .Where(x => x.Quotation.Status != nameof(QuotationStatusCode.SUPERSEDED) &&
                        x.Quotation.Status != nameof(QuotationStatusCode.CANCELED))
            .Select(x => x.DiagnosisRecommendationId!.Value)
            .Distinct().ToListAsync(ct)).ToHashSet();

        var recommendationAssignmentIds = recommendations
            .Where(x => x.DiagnosisAssignmentId.HasValue && x.DiagnosisAssignmentId.Value > 0)
            .Select(x => x.DiagnosisAssignmentId!.Value)
            .Distinct()
            .ToList();

        var supplementaryQuotations = await context.Quotations.AsNoTracking()
            .Where(q => candidateOrderIds.Contains(q.ServiceOrderId))
            .Where(q =>
                q.VersionNo > 1 &&
                q.Status != nameof(QuotationStatusCode.SUPERSEDED) &&
                q.Status != nameof(QuotationStatusCode.CANCELED))
            .Where(q =>
                (q.DiagnosisAssignmentId.HasValue && recommendationAssignmentIds.Contains(q.DiagnosisAssignmentId.Value)) ||
                (!q.DiagnosisAssignmentId.HasValue && latestDiagnosisIds.Contains(q.DiagnosisId ?? 0)))
            .Select(q => new
            {
                q.DiagnosisAssignmentId,
                q.DiagnosisId,
                QuotedAt = q.CreatedAt ?? DateTime.MinValue
            })
            .ToListAsync(ct);

        var latestSupplementaryQuotedAtByAssignmentId = supplementaryQuotations
            .Where(x => x.DiagnosisAssignmentId.HasValue && x.DiagnosisAssignmentId.Value > 0)
            .GroupBy(x => x.DiagnosisAssignmentId!.Value)
            .ToDictionary(x => x.Key, x => x.Max(y => y.QuotedAt));

        var latestSupplementaryQuotedAtByDiagnosisId = supplementaryQuotations
            .Where(x => x.DiagnosisId.HasValue && x.DiagnosisId.Value > 0)
            .GroupBy(x => x.DiagnosisId!.Value)
            .ToDictionary(x => x.Key, x => x.Max(y => y.QuotedAt));

        return recommendations
            .Where(rec =>
            {
                if (quotedRecommendationIds.Contains(rec.Id)) return false;
                DateTime quotedAt;
                var hasQuotedAt = rec.DiagnosisAssignmentId.HasValue && rec.DiagnosisAssignmentId.Value > 0
                    ? latestSupplementaryQuotedAtByAssignmentId.TryGetValue(rec.DiagnosisAssignmentId.Value, out quotedAt)
                    : latestSupplementaryQuotedAtByDiagnosisId.TryGetValue(rec.DiagnosisId, out quotedAt);
                if (!hasQuotedAt)
                {
                    return true;
                }

                return rec.ProposalAt > quotedAt;
            })
            .GroupBy(rec => orderIdByDiagnosisId.TryGetValue(rec.DiagnosisId, out var soId) ? soId : 0L)
            .Where(group => group.Key > 0)
            .Select(group => new PendingSupplementaryProposalResponseDto
            {
                ServiceOrderId = group.Key,
                PendingCount = group.Count(),
                LastRecommendationAt = group.Max(x => (DateTime?)x.ProposalAt)
            })
            .OrderByDescending(x => x.LastRecommendationAt ?? DateTime.MinValue)
            .ToList();
    }

    public async Task<long> ResolveServiceOrderIdFromDiagnosisIdAsync(long diagnosisId, CancellationToken ct = default)
    {
        if (diagnosisId <= 0)
        {
            return 0;
        }

        return await context.Diagnoses
            .AsNoTracking()
            .Where(x => x.Id == diagnosisId)
            .Select(x => x.ServiceOrderId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<long> ResolveServiceOrderIdFromAssignmentIdAsync(long diagnosisAssignmentId, CancellationToken ct = default)
    {
        if (diagnosisAssignmentId <= 0)
        {
            return 0;
        }

        return await context.DiagnosisAssignments
            .AsNoTracking()
            .Where(x => x.Id == diagnosisAssignmentId)
            .Join(
                context.Diagnoses.AsNoTracking(),
                assignment => assignment.DiagnosisId,
                diagnosis => diagnosis.Id,
                (_, diagnosis) => diagnosis.ServiceOrderId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(long DiagnosisId, long DiagnosisAssignmentId)?> ResolveDiagnosisLinkAsync(
        long? diagnosisAssignmentId,
        long? diagnosisId,
        CancellationToken ct = default)
    {
        if (diagnosisAssignmentId.HasValue && diagnosisAssignmentId.Value > 0)
        {
            var fromAssignment = await context.DiagnosisAssignments
                .AsNoTracking()
                .Where(x => x.Id == diagnosisAssignmentId.Value)
                .Select(x => new { x.DiagnosisId, x.Id })
                .FirstOrDefaultAsync(ct);
            if (fromAssignment == null)
            {
                return null;
            }

            if (diagnosisId.HasValue && diagnosisId.Value > 0 && diagnosisId.Value != fromAssignment.DiagnosisId)
            {
                return null;
            }

            return (fromAssignment.DiagnosisId, fromAssignment.Id);
        }

        if (!diagnosisId.HasValue || diagnosisId.Value <= 0)
        {
            return null;
        }

        var latestAssignmentId = await context.DiagnosisAssignments
            .AsNoTracking()
            .Where(x => x.DiagnosisId == diagnosisId.Value)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(ct);

        if (!latestAssignmentId.HasValue || latestAssignmentId.Value <= 0)
        {
            return null;
        }

        return (diagnosisId.Value, latestAssignmentId.Value);
    }

    public async Task<SupplementaryNotificationTarget?> GetSupplementaryNotificationTargetAsync(
        long diagnosisId,
        CancellationToken ct = default)
    {
        if (diagnosisId <= 0)
        {
            return null;
        }

        return await context.Diagnoses.AsNoTracking()
            .Where(x => x.Id == diagnosisId)
            .Join(
                context.ServiceOrders.AsNoTracking(),
                diagnosis => diagnosis.ServiceOrderId,
                serviceOrder => serviceOrder.Id,
                (diagnosis, serviceOrder) => new SupplementaryNotificationTarget(
                    serviceOrder.Id,
                    serviceOrder.Code,
                    serviceOrder.ExecuteBranchId,
                    serviceOrder.OriginBranchId,
                    serviceOrder.ReceptionistId,
                    serviceOrder.ServiceAdvisorId))
            .FirstOrDefaultAsync(ct);
    }

    public async Task SyncChildMaterialQuantitiesByBomAsync(
        DiagnosisRecommendation parentRecommendation,
        CancellationToken ct = default)
    {
        if (parentRecommendation == null)
        {
            return;
        }

        if (parentRecommendation.ParentRecommendationId.HasValue && parentRecommendation.ParentRecommendationId.Value > 0)
        {
            return;
        }

        if (!IsServiceRecommendation(parentRecommendation))
        {
            return;
        }

        var standardServiceId = parentRecommendation.StandardServiceId ?? 0;
        if (standardServiceId <= 0)
        {
            return;
        }

        var vehicleModelId = await context.Diagnoses
            .AsNoTracking()
            .Where(x => x.Id == parentRecommendation.DiagnosisId)
            .Join(
                context.ServiceOrders.AsNoTracking(),
                diagnosis => diagnosis.ServiceOrderId,
                serviceOrder => serviceOrder.Id,
                (diagnosis, serviceOrder) => serviceOrder.VehicleId)
            .Join(
                context.Vehicles.AsNoTracking(),
                vehicleId => vehicleId,
                vehicle => vehicle.Id,
                (_, vehicle) => vehicle.ModelId)
            .FirstOrDefaultAsync(ct);

        if (vehicleModelId <= 0)
        {
            return;
        }

        var bomByProductId = await context.ServiceBoms
            .AsNoTracking()
            .Where(x => x.ServiceId == standardServiceId && x.VehicleModelId == vehicleModelId)
            .GroupBy(x => x.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Quantity = g.OrderByDescending(x => x.UpdateAt).ThenByDescending(x => x.Id).Select(x => x.Quantity).FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.ProductId, x => x.Quantity, ct);

        if (bomByProductId.Count == 0)
        {
            return;
        }

        var children = await context.DiagnosisRecommendations
            .Where(x => x.ParentRecommendationId == parentRecommendation.Id)
            .ToListAsync(ct);

        if (children.Count == 0)
        {
            return;
        }

        var parentQty = parentRecommendation.Quantity <= 0 ? 1m : parentRecommendation.Quantity;
        var hasAnyChange = false;

        foreach (var child in children)
        {
            if (child.InventoryItemId == null || child.InventoryItemId <= 0)
            {
                continue;
            }

            if (IsSoftDeletedRecommendation(child.RecommendationStatus))
            {
                continue;
            }

            if (!bomByProductId.TryGetValue(child.InventoryItemId.Value, out var bomQuantity) || bomQuantity <= 0)
            {
                continue;
            }

            var targetQty = Math.Round(bomQuantity * parentQty, 3, MidpointRounding.AwayFromZero);
            if (targetQty <= 0)
            {
                targetQty = 1m;
            }

            if (Math.Abs(child.Quantity - targetQty) <= 0.000001m)
            {
                continue;
            }

            child.Quantity = targetQty;
            child.UpdatedAt = VietnamTime.Now;
            child.UpdatedBy = parentRecommendation.UpdatedBy;
            hasAnyChange = true;
        }

        if (hasAnyChange)
        {
            await context.SaveChangesAsync(ct);
        }
    }

    private static bool IsServiceRecommendation(DiagnosisRecommendation recommendation)
    {
        var token = string.IsNullOrWhiteSpace(recommendation?.ItemType)
            ? string.Empty
            : recommendation.ItemType.Trim().ToUpperInvariant();

        return token.Contains("SERVICE") || token.Contains("LABOR");
    }

    private static bool IsSoftDeletedRecommendation(string? recommendationStatus)
    {
        if (string.IsNullOrWhiteSpace(recommendationStatus))
        {
            return false;
        }

        return recommendationStatus.Trim().StartsWith("SOFT_DELETED", StringComparison.OrdinalIgnoreCase);
    }
}
