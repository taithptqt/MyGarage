using System;
using System.Collections.Generic;
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

public sealed class PurchaseOrderGoodsReceiptService : IPurchaseOrderGoodsReceiptService
{
    private readonly IPurchaseOrderStore _store;
    private readonly IPurchaseOrderAuthorizationService _authService;
    private readonly IStockAlertSyncService _stockAlertSyncService;

    public PurchaseOrderGoodsReceiptService(
        IPurchaseOrderStore store,
        IPurchaseOrderAuthorizationService authService,
        IStockAlertSyncService stockAlertSyncService)
    {
        _store = store;
        _authService = authService;
        _stockAlertSyncService = stockAlertSyncService;
    }

    public async Task<PurchaseOrderReceiveResultDto> ReceivePurchaseOrderAsync(
        long id,
        PurchaseOrderReceiveInput input,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (id <= 0) throw new InvalidOperationException("Mã đơn mua hàng không hợp lệ.");

        if (!await _authService.CanReceiveGoodsAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền nhập kho cho đơn mua hàng này.");
        }

        if (input.Items == null || input.Items.Count == 0)
        {
            throw new InvalidOperationException("Thiếu danh sách sản phẩm nhận kho.");
        }

        if (_store.HasActiveTransaction())
        {
            throw new InvalidOperationException("Không thể nhập kho trong giao dịch đang mở.");
        }

        var effectiveActorId = actor.UserId ?? input.ReceivedByStaffId;
        var now = input.ReceivedAt ?? VietnamTime.Now;
        var note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        var receiptCode = $"GRN-{Guid.NewGuid():N}";

        return await _store.ExecuteSerializableReceiveAsync(id, async token =>
        {
            var affectedProductIds = new HashSet<long>();
            var lineResults = new List<PurchaseOrderReceiveLineResultDto>();
            var grnItems = new List<GoodsReceiptNoteItem>();
            decimal totalReceivedAmount = 0m;

            var purchaseOrder = await _store.GetPurchaseOrderWithItemsForUpdateAsync(id, token);
            if (purchaseOrder == null) throw new KeyNotFoundException("Không tìm thấy đơn mua hàng.");
            if (!await _authService.CanReceiveGoodsAsync(id, actor, token))
                throw new UnauthorizedAccessException("Không có quyền nhập kho cho đơn mua hàng này.");
            if (!PurchaseOrderLifecycleRules.CanReceive(purchaseOrder.Status))
                throw new InvalidOperationException($"Đơn mua hàng đang ở trạng thái '{purchaseOrder.Status}', không thể nhận hàng.");
            if (!await _store.WarehouseBelongsToBranchAsync(purchaseOrder.DestinationWarehouseId, purchaseOrder.BranchId, token))
                throw new InvalidOperationException("Kho đích không thuộc chi nhánh của đơn mua hàng.");

            var itemById = purchaseOrder.PurchaseOrderItems.ToDictionary(x => x.Id, x => x);
            var productGroups = purchaseOrder.PurchaseOrderItems.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.ToList());
            ValidateIncomingItems(input.Items, itemById, productGroups);

            var itemByProductId = productGroups.Where(x => x.Value.Count == 1).ToDictionary(x => x.Key, x => x.Value[0]);
            var grn = new GoodsReceiptNote
            {
                GrnCode = receiptCode,
                PurchaseOrderId = purchaseOrder.Id,
                WarehouseId = purchaseOrder.DestinationWarehouseId,
                ReceivedAt = now,
                ReceivedBy = effectiveActorId,
                Status = "POSTED",
                Note = note,
                CreatedAt = now
            };
            await _store.AddGoodsReceiptNoteAndSaveAsync(grn, token);

            var receiptInventory = new Dictionary<long, MyGarage.Models.Inventory>();
            foreach (var incoming in input.Items)
            {
                if (incoming.ReceivedQuantity <= 0m)
                {
                    continue;
                }

                var targetItem = ResolveTargetItem(incoming, itemById, itemByProductId);
                if (targetItem == null)
                {
                    throw new InvalidOperationException("Có dòng nhận hàng không khớp với chi tiết đơn mua hàng.");
                }

                var lineEvalInput = new PurchaseOrderLifecycleRules.PoReceiptLineInput(
                    targetItem.OrderedQuantity,
                    targetItem.ReceivedQuantity ?? 0m,
                    incoming.ReceivedQuantity,
                    incoming.CostPrice,
                    targetItem.CostPrice
                );

                var lineResult = PurchaseOrderLifecycleRules.EvaluateReceiptLine(lineEvalInput);
                if (!lineResult.HasAcceptedQuantity)
                {
                    continue;
                }

                var acceptedQty = lineResult.AcceptedQuantity;
                var unitPrice = lineResult.EffectiveUnitPrice;
                targetItem.ReceivedQuantity = lineResult.NewReceivedQuantity;
                totalReceivedAmount += lineResult.LineTotalAmount;
                affectedProductIds.Add(targetItem.ProductId);

                await ApplyInventoryReceiptAsync(
                    purchaseOrder,
                    targetItem.ProductId,
                    acceptedQty,
                    unitPrice,
                    receiptInventory,
                    token);

                _store.AddStockTransaction(new StockTransaction
                {
                    ProductId = targetItem.ProductId,
                    WarehouseId = purchaseOrder.DestinationWarehouseId,
                    Type = "Purchase",
                    Quantity = acceptedQty,
                    Price = unitPrice,
                    PurchaseOrderId = purchaseOrder.Id,
                    Note = note ?? $"Receive from PO #{purchaseOrder.Id}",
                    CreatedAt = now,
                    CreatedBy = effectiveActorId
                });

                grnItems.Add(new GoodsReceiptNoteItem
                {
                    GoodsReceiptNoteId = grn.Id,
                    PurchaseOrderItemId = targetItem.Id,
                    ProductId = targetItem.ProductId,
                    OrderedQuantity = targetItem.OrderedQuantity,
                    ReceivedQuantity = acceptedQty,
                    UnitCost = unitPrice,
                    LineAmount = lineResult.LineTotalAmount,
                    CreatedAt = now
                });

                lineResults.Add(new PurchaseOrderReceiveLineResultDto(
                    targetItem.Id,
                    targetItem.ProductId,
                    acceptedQty,
                    targetItem.OrderedQuantity,
                    targetItem.ReceivedQuantity
                ));
            }

            if (lineResults.Count == 0)
            {
                throw new InvalidOperationException("Không có số lượng hợp lệ để nhận kho.");
            }

            if (grnItems.Count > 0)
            {
                _store.AddGoodsReceiptNoteItems(grnItems);
            }

            var statusChecks = purchaseOrder.PurchaseOrderItems.Select(x =>
                new PurchaseOrderLifecycleRules.PoLineStatusCheck(x.OrderedQuantity, x.ReceivedQuantity ?? 0m));
            purchaseOrder.Status = PurchaseOrderLifecycleRules.DetermineStatusAfterReceipt(statusChecks);

            await _store.SaveChangesAsync(token);

            if (affectedProductIds.Count > 0)
            {
                var relatedAlerts = await _store.GetUnresolvedAlertIdsAsync(
                    purchaseOrder.DestinationWarehouseId,
                    affectedProductIds,
                    token);

                foreach (var alertId in relatedAlerts)
                {
                    await _stockAlertSyncService.ResolveStockAlertAsync(alertId, purchaseOrder.Id, effectiveActorId);
                }
            }

            await _stockAlertSyncService.RebuildStockAlertsAsync(effectiveActorId);

            return new PurchaseOrderReceiveResultDto(
                grn.GrnCode,
                grn.Id,
                purchaseOrder.Id,
                purchaseOrder.Status,
                now,
                purchaseOrder.DestinationWarehouseId,
                totalReceivedAmount,
                lineResults
            );
        }, ct);
    }

    public async Task<PurchaseOrderReceiveHistoryDto> GetReceiveHistoryAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (id <= 0) throw new InvalidOperationException("Mã đơn mua hàng không hợp lệ.");

        if (!await _authService.CanViewPurchaseOrderAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem lịch sử nhận hàng của đơn mua hàng này.");
        }

        var purchaseOrder = await _store.GetPurchaseOrderBasicAsync(id, ct);
        if (purchaseOrder == null)
        {
            throw new KeyNotFoundException("Không tìm thấy đơn mua hàng.");
        }

        var grnHeaders = await _store.GetGoodsReceiptsForOrderAsync(id, ct);
        var receipts = grnHeaders.Select(grn => new GoodsReceiptNoteSummaryDto(
            grn.Id,
            grn.GrnCode,
            grn.ReceivedAt,
            grn.ReceivedByStaff?.FullName,
            grn.Status,
            grn.Items.Sum(i => i.LineAmount),
            grn.Note
        )).ToList();

        return new PurchaseOrderReceiveHistoryDto(
            purchaseOrder.Id,
            $"PO-{purchaseOrder.Id:D5}",
            receipts
        );
    }

    private static void ValidateIncomingItems(
        IReadOnlyList<PurchaseOrderReceiveItemInput> items,
        IReadOnlyDictionary<long, PurchaseOrderItem> itemById,
        IReadOnlyDictionary<long, List<PurchaseOrderItem>> productGroups)
    {
        foreach (var incoming in items.Where(x => x.ReceivedQuantity > 0m))
        {
            if (incoming.CostPrice < 0m)
                throw new InvalidOperationException("Đơn giá nhập kho không được âm.");
            if (incoming.PurchaseOrderItemId is > 0)
            {
                if (!itemById.TryGetValue(incoming.PurchaseOrderItemId.Value, out var line) ||
                    (incoming.ProductId is > 0 && incoming.ProductId.Value != line.ProductId))
                    throw new InvalidOperationException("Có dòng nhận hàng không khớp với chi tiết đơn mua hàng.");
            }
            else if (incoming.ProductId is not > 0 || !productGroups.TryGetValue(incoming.ProductId.Value, out var lines) || lines.Count != 1)
            {
                throw new InvalidOperationException("Không xác định được dòng đơn mua hàng; vui lòng cung cấp mã dòng.");
            }
        }
    }

    private static PurchaseOrderItem? ResolveTargetItem(
        PurchaseOrderReceiveItemInput incoming,
        IReadOnlyDictionary<long, PurchaseOrderItem> itemById,
        IReadOnlyDictionary<long, PurchaseOrderItem> itemByProductId)
    {
        if (incoming.PurchaseOrderItemId.HasValue && incoming.PurchaseOrderItemId.Value > 0)
        {
            itemById.TryGetValue(incoming.PurchaseOrderItemId.Value, out var byId);
            return byId;
        }

        if (incoming.ProductId.HasValue && incoming.ProductId.Value > 0)
        {
            itemByProductId.TryGetValue(incoming.ProductId.Value, out var byProduct);
            return byProduct;
        }

        return null;
    }

    private async Task ApplyInventoryReceiptAsync(
        PurchaseOrder purchaseOrder,
        long productId,
        decimal acceptedQty,
        decimal unitPrice,
        IDictionary<long, MyGarage.Models.Inventory> receiptInventory,
        CancellationToken token)
    {
        if (!receiptInventory.TryGetValue(productId, out var inventory))
            inventory = await _store.GetInventoryAsync(purchaseOrder.DestinationWarehouseId, productId, token);

        if (inventory == null)
        {
            var minStockLevel = await _store.GetProductMinStockLevelAsync(productId, token);
            inventory = new MyGarage.Models.Inventory
            {
                WarehouseId = purchaseOrder.DestinationWarehouseId,
                ProductId = productId,
                Quantity = acceptedQty,
                MinStockLevel = minStockLevel,
                ReservedQuantity = 0m,
                CurrentMacPrice = unitPrice,
                TotalInventoryValue = PurchaseOrderLifecycleRules.CalculateTotalInventoryValue(acceptedQty, unitPrice)
            };
            _store.AddInventory(inventory);
            receiptInventory[productId] = inventory;
            return;
        }

        receiptInventory[productId] = inventory;
        var newMac = PurchaseOrderLifecycleRules.CalculateMovingAverageCost(
            inventory.Quantity,
            inventory.CurrentMacPrice ?? 0m,
            acceptedQty,
            unitPrice
        );

        inventory.Quantity += acceptedQty;
        inventory.CurrentMacPrice = newMac;
        inventory.TotalInventoryValue = PurchaseOrderLifecycleRules.CalculateTotalInventoryValue(inventory.Quantity, newMac);
    }
}
