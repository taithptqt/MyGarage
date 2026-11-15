using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.DTOs.Requests;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class PurchaseOrderService : GenericService<PurchaseOrder>, IPurchaseOrderService
    {
        private readonly IGarageContext _context;
        private readonly IInventoryAdminService _inventoryAdminService;

        public PurchaseOrderService(
            IPurchaseOrderRepository repository,
            IGarageContext context,
            IInventoryAdminService inventoryAdminService) : base(repository)
        {
            _context = context;
            _inventoryAdminService = inventoryAdminService;
        }

        public async Task<PurchaseOrderReceiveResult> ReceivePurchaseOrderAsync(
            long purchaseOrderId,
            PurchaseOrderReceiveRequestDto request,
            long? actorStaffId)
        {
            if (purchaseOrderId <= 0)
            {
                throw new InvalidOperationException("Mã purchase order không hợp lệ.");
            }

            if (request == null || request.Items == null || request.Items.Count == 0)
            {
                throw new InvalidOperationException("Thiếu danh sách sản phẩm nhận kho.");
            }

            var purchaseOrder = await _context.PurchaseOrders
                .Include(x => x.PurchaseOrderItems)
                .FirstOrDefaultAsync(x => x.Id == purchaseOrderId);
            if (purchaseOrder == null)
            {
                throw new KeyNotFoundException("Không tìm thấy purchase order.");
            }

            if (string.Equals(purchaseOrder.Status, nameof(PurchaseOrderStatusCode.CANCELED), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Purchase order đã hủy, không thể nhận hàng.");
            }

            var warehouseExists = await _context.Warehouses
                .AsNoTracking()
                .AnyAsync(x => x.Id == purchaseOrder.DestinationWarehouseId);
            if (!warehouseExists)
            {
                throw new InvalidOperationException("Kho đích không tồn tại.");
            }

            var effectiveActorId = actorStaffId ?? request.ReceivedByStaffId;
            var now = request.ReceivedAt ?? VietnamTime.Now;
            var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

            var itemById = purchaseOrder.PurchaseOrderItems.ToDictionary(x => x.Id, x => x);
            var itemByProductId = purchaseOrder.PurchaseOrderItems
                .GroupBy(x => x.ProductId)
                .ToDictionary(x => x.Key, x => x.First());

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                var affectedProductIds = new HashSet<long>();
                var lineResults = new List<PurchaseOrderReceiveLineResult>();
                var grnItems = new List<GoodsReceiptNoteItem>();
                decimal totalReceivedAmount = 0m;

                await using var transaction = await TryBeginTransactionAsync();
                try
                {
                    var grn = new GoodsReceiptNote
                    {
                        GrnCode = $"GRN-{now:yyyyMMddHHmmss}-{purchaseOrder.Id}",
                        PurchaseOrderId = purchaseOrder.Id,
                        WarehouseId = purchaseOrder.DestinationWarehouseId,
                        ReceivedAt = now,
                        ReceivedBy = effectiveActorId,
                        Status = "POSTED",
                        Note = note,
                        CreatedAt = now
                    };
                    _context.GoodsReceiptNotes.Add(grn);
                    await SaveChangesInTransactionAsync();

                    foreach (var incoming in request.Items.Where(x => x != null))
                    {
                        if (incoming.ReceivedQuantity <= 0m)
                        {
                            continue;
                        }

                        PurchaseOrderItem? targetItem = null;
                        if (incoming.PurchaseOrderItemId.HasValue && incoming.PurchaseOrderItemId.Value > 0)
                        {
                            itemById.TryGetValue(incoming.PurchaseOrderItemId.Value, out targetItem);
                        }
                        else if (incoming.ProductId.HasValue && incoming.ProductId.Value > 0)
                        {
                            itemByProductId.TryGetValue(incoming.ProductId.Value, out targetItem);
                        }

                        if (targetItem == null)
                        {
                            if (transaction != null) await transaction.RollbackAsync();
                            throw new InvalidOperationException("Có dòng nhận hàng không khớp với item trong purchase order.");
                        }

                        var currentReceived = targetItem.ReceivedQuantity ?? 0m;
                        var remaining = Math.Max(0m, targetItem.OrderedQuantity - currentReceived);
                        if (remaining <= 0m)
                        {
                            continue;
                        }

                        var acceptedQty = Math.Min(remaining, incoming.ReceivedQuantity);
                        if (acceptedQty <= 0m)
                        {
                            continue;
                        }

                        targetItem.ReceivedQuantity = currentReceived + acceptedQty;
                        var unitPrice = incoming.CostPrice ?? targetItem.CostPrice;
                        totalReceivedAmount += acceptedQty * unitPrice;
                        affectedProductIds.Add(targetItem.ProductId);

                        var inventory = await _context.Inventories
                            .FirstOrDefaultAsync(x =>
                                x.WarehouseId == purchaseOrder.DestinationWarehouseId &&
                                x.ProductId == targetItem.ProductId);

                        if (inventory == null)
                        {
                            var product = await _context.Products.AsNoTracking()
                                .FirstOrDefaultAsync(x => x.Id == targetItem.ProductId);
                            inventory = new Inventory
                            {
                                WarehouseId = purchaseOrder.DestinationWarehouseId,
                                ProductId = targetItem.ProductId,
                                Quantity = acceptedQty,
                                MinStockLevel = product?.MinStockLevel ?? 0m,
                                ReservedQuantity = 0m,
                                CurrentMacPrice = unitPrice,
                                TotalInventoryValue = acceptedQty * unitPrice
                            };
                            _context.Inventories.Add(inventory);
                        }
                        else
                        {
                            inventory.Quantity += acceptedQty;
                            inventory.CurrentMacPrice = unitPrice;
                            inventory.TotalInventoryValue = (inventory.TotalInventoryValue ?? 0m) + (acceptedQty * unitPrice);
                        }

                        _context.StockTransactions.Add(new StockTransaction
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
                            LineAmount = acceptedQty * unitPrice,
                            CreatedAt = now
                        });

                        lineResults.Add(new PurchaseOrderReceiveLineResult(
                            targetItem.Id,
                            targetItem.ProductId,
                            acceptedQty,
                            targetItem.OrderedQuantity,
                            targetItem.ReceivedQuantity));
                    }

                    if (lineResults.Count == 0)
                    {
                        if (transaction != null) await transaction.RollbackAsync();
                        throw new InvalidOperationException("Không có số lượng hợp lệ để nhận kho.");
                    }

                    if (grnItems.Count > 0)
                    {
                        _context.GoodsReceiptNoteItems.AddRange(grnItems);
                    }

                    var allCompleted = purchaseOrder.PurchaseOrderItems.All(x => (x.ReceivedQuantity ?? 0m) >= x.OrderedQuantity);
                    purchaseOrder.Status = allCompleted
                        ? nameof(PurchaseOrderStatusCode.COMPLETED)
                        : nameof(PurchaseOrderStatusCode.PARTIAL_RECEIVED);

                    await SaveChangesInTransactionAsync();

                    if (affectedProductIds.Count > 0)
                    {
                        var relatedAlerts = await _context.StockAlerts
                            .Where(x =>
                                x.IsResolved == false &&
                                x.WarehouseId == purchaseOrder.DestinationWarehouseId &&
                                affectedProductIds.Contains(x.ProductId))
                            .Select(x => x.Id)
                            .ToListAsync();

                        foreach (var alertId in relatedAlerts)
                        {
                            await _inventoryAdminService.ResolveStockAlertAsync(alertId, purchaseOrder.Id, effectiveActorId);
                        }
                    }

                    await _inventoryAdminService.RebuildStockAlertsAsync(effectiveActorId);
                    if (transaction != null) await transaction.CommitAsync();

                    return new PurchaseOrderReceiveResult(
                        grn.GrnCode,
                        grn.Id,
                        purchaseOrder.Id,
                        purchaseOrder.Status,
                        now,
                        purchaseOrder.DestinationWarehouseId,
                        totalReceivedAmount,
                        lineResults);
                }
                catch
                {
                    if (transaction != null) await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
        {
            if (_context.Database.CurrentTransaction != null)
            {
                return null;
            }

            return await _context.Database.BeginTransactionAsync();
        }

        private async Task SaveChangesInTransactionAsync()
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await TryBeginTransactionAsync();
                await _context.SaveChangesAsync();
                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }
            });
        }
    }
}
