using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Purchasing;
using MyGarage.Application.Purchasing.DTOs;
using MyGarage.Common;
using MyGarage.Domain.Rules;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class PurchaseOrderAnalyticsService : IPurchaseOrderAnalyticsService
{
    private readonly IPurchaseOrderStore _store;
    private readonly IPurchaseOrderAuthorizationService _authService;
    private readonly IGrnSnapshotService _grnSnapshotService;

    public PurchaseOrderAnalyticsService(
        IPurchaseOrderStore store,
        IPurchaseOrderAuthorizationService authService,
        IGrnSnapshotService grnSnapshotService)
    {
        _store = store;
        _authService = authService;
        _grnSnapshotService = grnSnapshotService;
    }

    public async Task<PurchaseOrderFulfillmentDto> GetFulfillmentAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (id <= 0) throw new InvalidOperationException("Mã đơn mua hàng không hợp lệ.");

        if (!await _authService.CanViewPurchaseOrderAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem thông tin đáp ứng của đơn mua hàng này.");
        }

        var purchaseOrder = await _store.GetPurchaseOrderForFulfillmentAsync(id, ct);
        if (purchaseOrder == null)
        {
            throw new KeyNotFoundException("Không tìm thấy đơn mua hàng.");
        }

        return MapFulfillmentDto(purchaseOrder);
    }

    public async Task<PurchaseOrderFulfillmentDashboardDto> GetFulfillmentDashboardAsync(
        PurchaseOrderFulfillmentFilterDto filter,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        var targetBranchId = ResolveTargetBranchId(filter.BranchId, actor);

        if (!await _authService.CanViewPurchaseOrdersAsync(targetBranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem dashboard đáp ứng đơn mua hàng.");
        }

        var orders = await _store.GetPurchaseOrdersForDashboardAsync(targetBranchId, filter.FromDate, filter.ToDate, ct);
        var fulfillmentDtos = orders.Select(MapFulfillmentDto).ToList();

        var totalOrders = fulfillmentDtos.Count;
        var completedOrders = fulfillmentDtos.Count(x => x.Status == "COMPLETED");
        var partialOrders = fulfillmentDtos.Count(x => x.Status == "PARTIAL_RECEIVED");
        var pendingOrders = fulfillmentDtos.Count(x => x.Status is "PENDING" or "APPROVED");

        var sumOrdered = fulfillmentDtos.Sum(x => x.TotalOrderedQuantity);
        var sumReceived = fulfillmentDtos.Sum(x => x.TotalReceivedQuantity);
        var overallRate = PurchaseOrderLifecycleRules.CalculateFulfillmentPercentage(sumOrdered, sumReceived);

        return new PurchaseOrderFulfillmentDashboardDto(
            totalOrders,
            completedOrders,
            partialOrders,
            pendingOrders,
            overallRate,
            fulfillmentDtos
        );
    }

    public async Task<GrnKpisDto> GetGrnKpisAsync(
        GrnKpiFilterDto filter,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        var targetBranchId = ResolveTargetBranchId(filter.BranchId, actor);

        if (!await _authService.CanViewGoodsReceiptsAsync(targetBranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem KPI nhập kho.");
        }

        var from = filter.FromDate.HasValue
            ? filter.FromDate.Value.ToDateTime(TimeOnly.MinValue)
            : VietnamTime.Now.Date.AddDays(-30);
        var to = filter.ToDate.HasValue
            ? filter.ToDate.Value.ToDateTime(TimeOnly.MaxValue)
            : VietnamTime.Now.Date.AddDays(1);

        var (grns, itemRows) = await _store.GetGrnKpiDataAsync(targetBranchId, from, to, ct);

        var totalGrnCount = grns.Count;
        var totalReceivedValue = itemRows.Sum(x => x.LineAmount);
        var totalReceivedQuantity = itemRows.Sum(x => x.ReceivedQuantity);

        var dayByGrnId = grns.ToDictionary(x => x.Id, x => x.ReceivedAt.Date);
        var dailyBreakdown = itemRows
            .GroupBy(x => dayByGrnId.TryGetValue(x.GoodsReceiptNoteId, out var d) ? d : DateTime.MinValue.Date)
            .Select(g => new GrnDailyBreakdownDto(
                g.Key.ToString("yyyy-MM-dd"),
                g.Select(x => x.GoodsReceiptNoteId).Distinct().Count(),
                g.Sum(x => x.LineAmount),
                g.Sum(x => x.ReceivedQuantity)
            ))
            .OrderByDescending(x => x.Date)
            .ToList();

        var topItems = itemRows
            .GroupBy(x => x.ProductId)
            .Select(g =>
            {
                var first = g.First();
                return new GrnTopItemDto(
                    g.Key,
                    first.Product?.Name ?? $"Product #{g.Key}",
                    first.Product?.Sku ?? string.Empty,
                    g.Sum(x => x.ReceivedQuantity),
                    g.Sum(x => x.LineAmount)
                );
            })
            .OrderByDescending(x => x.TotalValue)
            .ThenByDescending(x => x.TotalQuantityReceived)
            .Take(10)
            .ToList();

        return new GrnKpisDto(
            totalGrnCount,
            totalReceivedValue,
            totalReceivedQuantity,
            dailyBreakdown,
            topItems
        );
    }

    public async Task<object> RebuildGrnSnapshotAsync(
        DateOnly? snapshotDate,
        long? branchId,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        var targetBranchId = ResolveTargetBranchId(branchId, actor);

        if (!await _authService.CanViewGoodsReceiptsAsync(targetBranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền rebuild snapshot nhập kho.");
        }

        var targetDate = snapshotDate ?? DateOnly.FromDateTime(VietnamTime.Now.Date.AddDays(-1));
        return await _grnSnapshotService.RebuildDailySnapshotAsync(targetDate, targetBranchId);
    }

    public async Task<object> GetGrnSnapshotTrendAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        long? branchId,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        var targetBranchId = ResolveTargetBranchId(branchId, actor);

        if (!await _authService.CanViewGoodsReceiptsAsync(targetBranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem snapshot trend nhập kho.");
        }

        var from = fromDate ?? DateOnly.FromDateTime(VietnamTime.Now.Date.AddDays(-30));
        var to = toDate ?? DateOnly.FromDateTime(VietnamTime.Now.Date);
        var rows = await _grnSnapshotService.GetTrendAsync(from, to, targetBranchId);
        return new
        {
            fromDate = from,
            toDate = to,
            branchId = targetBranchId,
            items = rows
        };
    }

    private static long? ResolveTargetBranchId(long? branchId, UserActorContext actor)
        => !actor.IsAdmin ? actor.BranchId ?? branchId : branchId;

    private static PurchaseOrderFulfillmentDto MapFulfillmentDto(PurchaseOrder po)
    {
        var lines = po.PurchaseOrderItems.Select(i =>
        {
            var ord = i.OrderedQuantity;
            var rec = i.ReceivedQuantity ?? 0m;
            var rem = Math.Max(0m, ord - rec);
            var pct = PurchaseOrderLifecycleRules.CalculateFulfillmentPercentage(ord, rec);
            var st = rem <= PurchaseOrderLifecycleRules.QuantityTolerance
                ? "COMPLETED"
                : (rec > PurchaseOrderLifecycleRules.QuantityTolerance ? "PARTIAL_RECEIVED" : "PENDING");

            return new PurchaseOrderFulfillmentLineDto(
                i.Id,
                i.ProductId,
                i.Product?.Name,
                i.Product?.Sku,
                ord,
                rec,
                rem,
                pct,
                st
            );
        }).ToList();

        var totOrd = lines.Sum(l => l.OrderedQuantity);
        var totRec = lines.Sum(l => l.ReceivedQuantity);
        var totRem = lines.Sum(l => l.RemainingQuantity);
        var overall = PurchaseOrderLifecycleRules.CalculateFulfillmentPercentage(totOrd, totRec);

        return new PurchaseOrderFulfillmentDto(
            po.Id,
            $"PO-{po.Id:D5}",
            po.Status,
            totOrd,
            totRec,
            totRem,
            overall,
            lines
        );
    }
}
