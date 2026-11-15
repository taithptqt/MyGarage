using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Purchasing;
using MyGarage.Application.Purchasing.DTOs;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class GoodsReceiptApplicationService : IGoodsReceiptApplicationService
{
    private readonly IGoodsReceiptStore _store;
    private readonly IPurchaseOrderApplicationService _poAppService;
    private readonly IPurchaseOrderAuthorizationService _authService;

    public GoodsReceiptApplicationService(
        IGoodsReceiptStore store,
        IPurchaseOrderApplicationService poAppService,
        IPurchaseOrderAuthorizationService authService)
    {
        _store = store;
        _poAppService = poAppService;
        _authService = authService;
    }

    public async Task<GoodsReceiptPagedResultDto> GetGoodsReceiptsAsync(
        GoodsReceiptFilterDto filter,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        var targetBranchId = filter.BranchId;
        if (!actor.IsAdmin)
        {
            targetBranchId = actor.BranchId ?? targetBranchId;
        }

        if (!await _authService.CanViewGoodsReceiptsAsync(targetBranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem danh sách phiếu nhập kho.");
        }

        var page = Math.Max(1, filter.Page.GetValueOrDefault(1));
        var pageSize = Math.Clamp(filter.PageSize.GetValueOrDefault(50), 1, 200);

        var (pagedRows, total) = await _store.GetGoodsReceiptsPageAsync(
            targetBranchId,
            filter.PurchaseOrderId,
            page,
            pageSize,
            ct);

        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        var dtos = pagedRows.Select(MapToDetailDto).ToList();

        return new GoodsReceiptPagedResultDto(dtos, total, page, pageSize, totalPages);
    }

    public async Task<GoodsReceiptDetailDto?> GetGoodsReceiptByIdAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (id <= 0) return null;

        if (!await _authService.CanViewGoodsReceiptAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem chi tiết phiếu nhập kho này.");
        }

        var grn = await _store.GetGoodsReceiptByIdAsync(id, ct);
        return grn == null ? null : MapToDetailDto(grn);
    }

    public async Task<PurchaseOrderReceiveResultDto> CreateGoodsReceiptAsync(
        GoodsReceiptCreateInput input,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (input.PurchaseOrderId <= 0)
        {
            throw new InvalidOperationException("PurchaseOrderId là bắt buộc để lập phiếu nhập kho.");
        }

        if (!await _store.PurchaseOrderExistsAsync(input.PurchaseOrderId, ct))
        {
            throw new KeyNotFoundException("Không tìm thấy đơn mua hàng liên kết.");
        }

        if (!await _authService.CanReceiveGoodsAsync(input.PurchaseOrderId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền tạo phiếu nhập kho cho đơn mua hàng này.");
        }

        var receiveItems = (input.Items ?? new List<GoodsReceiptCreateItemInput>())
            .Where(x => x != null)
            .Select(x => new PurchaseOrderReceiveItemInput(
                x.PurchaseOrderItemId ?? x.PoItemId,
                x.ProductId,
                x.ReceivedQuantity ?? x.ReceivedQty ?? 0m,
                x.CostPrice ?? x.UnitPrice
            ))
            .ToList();

        var receiveInput = new PurchaseOrderReceiveInput(
            input.ReceivedAt,
            input.Note ?? input.Notes,
            actor.UserId ?? input.ReceivedBy,
            receiveItems
        );

        return await _poAppService.ReceivePurchaseOrderAsync(input.PurchaseOrderId, receiveInput, actor, ct);
    }

    private static GoodsReceiptDetailDto MapToDetailDto(GoodsReceiptNote g)
    {
        var branchId = g.Warehouse?.BranchId ?? g.PurchaseOrder?.BranchId;
        var items = g.Items.Select(i => new GoodsReceiptItemDetailDto(
            i.Id,
            i.PurchaseOrderItemId,
            i.ProductId,
            i.Product?.Sku,
            i.Product?.Name,
            i.Product?.Unit,
            i.OrderedQuantity,
            i.ReceivedQuantity,
            i.UnitCost,
            i.LineAmount
        )).ToList();

        var totalAmount = items.Sum(i => i.LineAmount);

        return new GoodsReceiptDetailDto(
            g.Id,
            g.GrnCode,
            g.GrnCode,
            g.ReceivedAt,
            g.CreatedAt,
            g.PurchaseOrderId,
            $"PO-{g.PurchaseOrderId:D5}",
            g.WarehouseId,
            g.Warehouse?.Name,
            branchId,
            g.ReceivedBy,
            g.ReceivedByStaff?.FullName,
            g.Status,
            g.Note,
            totalAmount,
            items
        );
    }
}
