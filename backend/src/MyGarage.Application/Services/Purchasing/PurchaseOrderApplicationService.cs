using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Application.Authorization;
using MyGarage.Application.Purchasing;
using MyGarage.Application.Purchasing.DTOs;
using MyGarage.Common;
using MyGarage.Domain.Rules;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class PurchaseOrderApplicationService : IPurchaseOrderApplicationService
{
    private readonly IPurchaseOrderStore _store;
    private readonly IPurchaseOrderAuthorizationService _authService;
    private readonly IStockAlertSyncService _stockAlertSyncService;
    private readonly IPurchaseOrderGoodsReceiptService _goodsReceiptService;
    private readonly IPurchaseOrderAnalyticsService _analyticsService;

    [ActivatorUtilitiesConstructor]
    public PurchaseOrderApplicationService(
        IPurchaseOrderStore store,
        IPurchaseOrderAuthorizationService authService,
        IStockAlertSyncService stockAlertSyncService,
        IPurchaseOrderGoodsReceiptService goodsReceiptService,
        IPurchaseOrderAnalyticsService analyticsService)
    {
        _store = store;
        _authService = authService;
        _stockAlertSyncService = stockAlertSyncService;
        _goodsReceiptService = goodsReceiptService;
        _analyticsService = analyticsService;
    }

    public async Task<PurchaseOrderPagedResultDto> GetPurchaseOrdersAsync(
        PurchaseOrderFilterDto filter,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        var targetBranchId = filter.BranchId;
        if (!actor.IsAdmin)
        {
            targetBranchId = actor.BranchId ?? targetBranchId;
        }

        if (!await _authService.CanViewPurchaseOrdersAsync(targetBranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem danh sách đơn mua hàng.");
        }

        string? statusGroup = null;
        string? normalizedStatus = null;
        if (!string.IsNullOrWhiteSpace(filter.StatusGroup))
        {
            statusGroup = filter.StatusGroup.Trim().ToUpperInvariant();
            if (statusGroup is not ("ALL" or "PENDING" or "APPROVED" or "RECEIVED"))
            {
                throw new ArgumentException("Unknown purchase order status group.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            normalizedStatus = PurchaseOrderLifecycleRules.NormalizeStatus(filter.Status);
        }

        var page = Math.Max(1, filter.Page.GetValueOrDefault(1));
        var pageSize = Math.Clamp(filter.PageSize.GetValueOrDefault(50), 1, 200);

        var (orders, total) = await _store.GetPurchaseOrdersPageAsync(
            targetBranchId,
            statusGroup,
            normalizedStatus,
            page,
            pageSize,
            ct);

        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        var dtos = orders.Select(PurchaseOrderApplicationMapper.MapToDetailDto).ToList();

        return new PurchaseOrderPagedResultDto(dtos, total, page, pageSize, totalPages);
    }

    public async Task<PurchaseOrderDetailDto?> GetPurchaseOrderByIdAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (id <= 0) return null;

        if (!await _authService.CanViewPurchaseOrderAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem chi tiết đơn mua hàng này.");
        }

        var order = await _store.GetPurchaseOrderDetailByIdAsync(id, ct);
        return order == null ? null : PurchaseOrderApplicationMapper.MapToDetailDto(order);
    }

    public async Task<PurchaseOrderDetailDto> CreatePurchaseOrderAsync(
        PurchaseOrderCreateInput input,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        var targetBranchId = input.BranchId;
        if (!actor.IsAdmin && actor.BranchId.HasValue)
        {
            targetBranchId = actor.BranchId.Value;
        }

        if (!await _authService.CanCreatePurchaseOrderAsync(targetBranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền tạo đơn mua hàng tại chi nhánh này.");
        }

        if (input.SupplierId <= 0 || !await _store.SupplierExistsAsync(input.SupplierId, ct))
        {
            throw new InvalidOperationException("Nhà cung cấp không hợp lệ hoặc không tồn tại.");
        }

        var destinationWarehouseId = input.DestinationWarehouseId;
        if (destinationWarehouseId <= 0)
        {
            destinationWarehouseId = await _store.GetFirstActiveWarehouseIdAsync(targetBranchId, ct);
        }
        else if (!await _store.WarehouseBelongsToBranchAsync(destinationWarehouseId, targetBranchId, ct))
        {
            throw new InvalidOperationException("Kho đích không thuộc chi nhánh của đơn mua hàng.");
        }

        if (destinationWarehouseId <= 0)
        {
            throw new InvalidOperationException($"Không tìm thấy kho hoạt động cho chi nhánh #{targetBranchId}.");
        }

        var now = VietnamTime.Now;
        var actorId = actor.UserId ?? input.CreatedBy;

        var lineItems = new List<PurchaseOrderItem>();
        if (input.Items != null)
        {
            foreach (var item in input.Items)
            {
                var qty = item.OrderedQuantity ?? item.Qty ?? 0m;
                if (qty <= 0m || item.ProductId <= 0)
                {
                    throw new InvalidOperationException("Dòng đơn mua phải có sản phẩm và số lượng lớn hơn 0.");
                }

                if (!await _store.ProductExistsAsync(item.ProductId, ct))
                {
                    throw new InvalidOperationException($"Phụ tùng/sản phẩm #{item.ProductId} không tồn tại.");
                }

                var price = item.CostPrice ?? item.UnitPrice ?? 0m;
                if (price < 0m)
                    throw new InvalidOperationException("Đơn giá mua hàng không được âm.");
                lineItems.Add(new PurchaseOrderItem
                {
                    ProductId = item.ProductId,
                    OrderedQuantity = qty,
                    ReceivedQuantity = 0m,
                    CostPrice = price
                });
            }
        }

        var calculatedTotal = PurchaseOrderLifecycleRules.CalculateOrderTotal(
            lineItems.Select(x => new PurchaseOrderLifecycleRules.PoItemTotalInput(x.OrderedQuantity, x.CostPrice)));
        if (input.TotalAmount < 0m)
            throw new InvalidOperationException("Tổng tiền đơn mua hàng không được âm.");

        var status = PurchaseOrderLifecycleRules.NormalizeStatus(input.Status);
        if (string.IsNullOrWhiteSpace(status))
        {
            status = "PENDING";
        }
        if (status != "DRAFT" && status != "PENDING")
        {
            throw new InvalidOperationException("Đơn mua hàng mới chỉ có thể ở trạng thái nháp hoặc chờ duyệt.");
        }

        var orderDate = input.OrderDate == default ? DateOnly.FromDateTime(now) : input.OrderDate;
        var expectedDeliveryDate = input.ExpectedDeliveryDate ?? input.ExpectedDate;

        var po = new PurchaseOrder
        {
            BranchId = targetBranchId,
            DestinationWarehouseId = destinationWarehouseId,
            SupplierId = input.SupplierId,
            OrderDate = orderDate,
            ExpectedDeliveryDate = expectedDeliveryDate,
            TotalAmount = input.TotalAmount ?? calculatedTotal,
            Status = status,
            ApprovedByStaffId = null,
            CreatedBy = actorId,
            CreatedAt = now,
            PurchaseOrderItems = lineItems
        };

        await _store.ExecuteInTransactionAsync(async token =>
        {
            await _store.AddPurchaseOrderAndSaveAsync(po, token);
            await _stockAlertSyncService.RebuildStockAlertsAsync(actorId);
        }, ct);

        return (await GetPurchaseOrderByIdAsync(po.Id, actor, ct))!;
    }

    public async Task<PurchaseOrderDetailDto?> UpdatePurchaseOrderAsync(
        long id,
        PurchaseOrderUpdateInput input,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (id <= 0) return null;

        if (!await _authService.CanUpdatePurchaseOrderAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền chỉnh sửa đơn mua hàng này.");
        }

        var po = await _store.GetPurchaseOrderWithItemsForUpdateAsync(id, ct);
        if (po == null) return null;

        if (input.TotalAmount < 0m)
            throw new InvalidOperationException("Tổng tiền đơn mua hàng không được âm.");

        if (PurchaseOrderLifecycleRules.IsTerminalStatus(po.Status))
        {
            throw new InvalidOperationException($"Đơn mua hàng đang ở trạng thái '{po.Status}', không thể chỉnh sửa.");
        }

        var targetBranchId = input.BranchId > 0 ? input.BranchId : po.BranchId;
        var targetWarehouseId = input.DestinationWarehouseId > 0 ? input.DestinationWarehouseId : po.DestinationWarehouseId;
        var supplierChanged = input.SupplierId > 0 && input.SupplierId != po.SupplierId;
        if ((targetBranchId != po.BranchId || targetWarehouseId != po.DestinationWarehouseId || supplierChanged) &&
            await _store.HasGoodsReceiptsForOrderAsync(id, ct))
        {
            throw new InvalidOperationException("Không thể thay đổi chi nhánh, kho hoặc nhà cung cấp khi đơn đã có phiếu nhập.");
        }
        if (supplierChanged && !await _store.SupplierExistsAsync(input.SupplierId, ct))
            throw new InvalidOperationException("Nhà cung cấp không hợp lệ hoặc không tồn tại.");
        if (!await _authService.CanCreatePurchaseOrderAsync(targetBranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền chuyển đơn mua hàng đến chi nhánh này.");
        }
        if (!await _store.WarehouseBelongsToBranchAsync(targetWarehouseId, targetBranchId, ct))
        {
            throw new InvalidOperationException("Kho đích không thuộc chi nhánh của đơn mua hàng.");
        }

        if (input.BranchId > 0) po.BranchId = targetBranchId;
        if (input.DestinationWarehouseId > 0) po.DestinationWarehouseId = targetWarehouseId;
        if (input.SupplierId > 0) po.SupplierId = input.SupplierId;
        if (input.OrderDate != default) po.OrderDate = input.OrderDate;
        if (input.ExpectedDeliveryDate.HasValue) po.ExpectedDeliveryDate = input.ExpectedDeliveryDate;
        if (input.TotalAmount.HasValue) po.TotalAmount = input.TotalAmount.Value;
        if (!string.IsNullOrWhiteSpace(input.Status))
        {
            var normalized = PurchaseOrderLifecycleRules.NormalizeStatus(input.Status);
            if (normalized != PurchaseOrderLifecycleRules.NormalizeStatus(po.Status) &&
                normalized is not ("DRAFT" or "PENDING"))
            {
                throw new InvalidOperationException("Vui lòng sử dụng chức năng duyệt, nhập hoặc hủy đơn tương ứng.");
            }
            if (!PurchaseOrderLifecycleRules.CanTransition(po.Status, normalized))
            {
                throw new InvalidOperationException($"Không thể chuyển trạng thái đơn hàng từ '{po.Status}' sang '{normalized}'.");
            }
            po.Status = normalized;
        }

        await _store.ExecuteInTransactionAsync(token => _store.SaveChangesAsync(token), ct);

        return (await GetPurchaseOrderByIdAsync(po.Id, actor, ct))!;
    }

    public async Task<PurchaseOrderDetailDto> ApprovePurchaseOrderAsync(
        long id,
        PurchaseOrderApproveInput? input,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (id <= 0) throw new KeyNotFoundException("Không tìm thấy đơn mua hàng.");

        if (!await _authService.CanApprovePurchaseOrderAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền phê duyệt đơn mua hàng này.");
        }

        var po = await _store.GetPurchaseOrderWithItemsForUpdateAsync(id, ct);
        if (po == null) throw new KeyNotFoundException("Không tìm thấy đơn mua hàng.");

        if (!PurchaseOrderLifecycleRules.CanApprove(po.Status))
        {
            throw new InvalidOperationException($"Chỉ đơn hàng ở trạng thái PENDING hoặc DRAFT mới có thể duyệt (Hiện tại: '{po.Status}').");
        }

        po.Status = "APPROVED";
        po.ApprovedByStaffId = actor.UserId ?? input?.ApprovedBy;

        await _store.ExecuteInTransactionAsync(token => _store.SaveChangesAsync(token), ct);

        return (await GetPurchaseOrderByIdAsync(po.Id, actor, ct))!;
    }

    public Task<PurchaseOrderReceiveResultDto> ReceivePurchaseOrderAsync(long id, PurchaseOrderReceiveInput input, UserActorContext actor, CancellationToken ct = default)
        => _goodsReceiptService.ReceivePurchaseOrderAsync(id, input, actor, ct);

    public Task<PurchaseOrderReceiveHistoryDto> GetReceiveHistoryAsync(long id, UserActorContext actor, CancellationToken ct = default)
        => _goodsReceiptService.GetReceiveHistoryAsync(id, actor, ct);

    public Task<PurchaseOrderFulfillmentDto> GetFulfillmentAsync(long id, UserActorContext actor, CancellationToken ct = default)
        => _analyticsService.GetFulfillmentAsync(id, actor, ct);

    public Task<PurchaseOrderFulfillmentDashboardDto> GetFulfillmentDashboardAsync(PurchaseOrderFulfillmentFilterDto filter, UserActorContext actor, CancellationToken ct = default)
        => _analyticsService.GetFulfillmentDashboardAsync(filter, actor, ct);

    public Task<GrnKpisDto> GetGrnKpisAsync(GrnKpiFilterDto filter, UserActorContext actor, CancellationToken ct = default)
        => _analyticsService.GetGrnKpisAsync(filter, actor, ct);

    public Task<object> RebuildGrnSnapshotAsync(DateOnly? snapshotDate, long? branchId, UserActorContext actor, CancellationToken ct = default)
        => _analyticsService.RebuildGrnSnapshotAsync(snapshotDate, branchId, actor, ct);

    public Task<object> GetGrnSnapshotTrendAsync(DateOnly? fromDate, DateOnly? toDate, long? branchId, UserActorContext actor, CancellationToken ct = default)
        => _analyticsService.GetGrnSnapshotTrendAsync(fromDate, toDate, branchId, actor, ct);

    public async Task<bool> DeletePurchaseOrderAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (id <= 0) return false;

        if (!await _authService.CanCancelOrDeletePurchaseOrderAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền hủy hoặc xóa đơn mua hàng này.");
        }

        var po = await _store.GetPurchaseOrderWithItemsForUpdateAsync(id, ct);
        if (po == null) return false;

        var grnCount = await _store.CountGoodsReceiptsForOrderAsync(id, ct);
        var totalReceived = po.PurchaseOrderItems.Sum(i => i.ReceivedQuantity ?? 0m);

        var cancelEval = PurchaseOrderLifecycleRules.EvaluateCancellation(
            new PurchaseOrderLifecycleRules.PoCancellationInput(po.Status, totalReceived, grnCount));

        if (!cancelEval.CanCancel)
        {
            throw new InvalidOperationException(cancelEval.BlockingReason ?? "Không thể xóa đơn mua hàng đã nhận hàng.");
        }

        await _store.ExecuteInTransactionAsync(async token =>
        {
            await _store.RemovePurchaseOrderAndItemsAsync(po, token);
            await _stockAlertSyncService.RebuildStockAlertsAsync(actor.UserId);
        }, ct);

        return true;
    }
}
