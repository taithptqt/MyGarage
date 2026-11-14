using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
    private readonly IGarageContext _context;
    private readonly IPurchaseOrderAuthorizationService _authService;
    private readonly IInventoryAdminService _inventoryAdminService;
    private readonly IGrnSnapshotService _grnSnapshotService;

    public PurchaseOrderApplicationService(
        IGarageContext context,
        IPurchaseOrderAuthorizationService authService,
        IInventoryAdminService inventoryAdminService,
        IGrnSnapshotService grnSnapshotService)
    {
        _context = context;
        _authService = authService;
        _inventoryAdminService = inventoryAdminService;
        _grnSnapshotService = grnSnapshotService;
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

        var query = _context.PurchaseOrders
            .AsNoTracking()
            .AsQueryable();

        if (targetBranchId.HasValue)
        {
            query = query.Where(po => po.BranchId == targetBranchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.StatusGroup))
        {
            var normalizedGroup = filter.StatusGroup.Trim().ToUpperInvariant();
            switch (normalizedGroup)
            {
                case "ALL":
                    break;
                case "PENDING":
                    query = query.Where(po => po.Status.ToUpper() == "PENDING" || po.Status.ToUpper() == "PENDING_APPROVAL");
                    break;
                case "APPROVED":
                    query = query.Where(po => po.Status.ToUpper() == "APPROVED" || po.Status.ToUpper() == "PARTIAL_RECEIVED");
                    break;
                case "RECEIVED":
                    query = query.Where(po => po.Status.ToUpper() == "COMPLETED" || po.Status.ToUpper() == "RECEIVED");
                    break;
                default:
                    throw new ArgumentException("Unknown purchase order status group.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var normalizedStatus = PurchaseOrderLifecycleRules.NormalizeStatus(filter.Status);
            query = query.Where(po => po.Status.ToUpper() == normalizedStatus);
        }

        var total = await query.CountAsync(ct);

        var page = filter.Page.GetValueOrDefault(1);
        if (page < 1) page = 1;
        var pageSize = filter.PageSize.GetValueOrDefault(50);
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;

        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);

        var orders = await query
            .Include(po => po.Supplier)
            .Include(po => po.DestinationWarehouse)
            .Include(po => po.ApprovedByStaff)
            .Include(po => po.CreatedByNavigation)
            .Include(po => po.PurchaseOrderItems)
                .ThenInclude(i => i.Product)
            .OrderByDescending(po => po.CreatedAt ?? DateTime.MinValue)
            .ThenByDescending(po => po.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = orders.Select(MapToDetailDto).ToList();

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

        var order = await _context.PurchaseOrders
            .AsNoTracking()
            .Include(po => po.Supplier)
            .Include(po => po.DestinationWarehouse)
            .Include(po => po.ApprovedByStaff)
            .Include(po => po.CreatedByNavigation)
            .Include(po => po.PurchaseOrderItems)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(po => po.Id == id, ct);

        return order == null ? null : MapToDetailDto(order);
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

        if (input.SupplierId <= 0 || !await _context.Suppliers.AsNoTracking().AnyAsync(s => s.Id == input.SupplierId, ct))
        {
            throw new InvalidOperationException("Nhà cung cấp không hợp lệ hoặc không tồn tại.");
        }

        var destinationWarehouseId = input.DestinationWarehouseId;
        if (destinationWarehouseId <= 0)
        {
            destinationWarehouseId = await _context.Warehouses
                .AsNoTracking()
                .Where(w => w.BranchId == targetBranchId && (w.IsActive ?? true))
                .OrderBy(w => w.Id)
                .Select(w => w.Id)
                .FirstOrDefaultAsync(ct);
        }
        else
        {
            var warehouseMatchesBranch = await _context.Warehouses
                .AsNoTracking()
                .AnyAsync(w => w.Id == destinationWarehouseId && w.BranchId == targetBranchId, ct);
            if (!warehouseMatchesBranch)
            {
                throw new InvalidOperationException("Kho đích không thuộc chi nhánh của đơn mua hàng.");
            }
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
                    continue;
                }

                var productExists = await _context.Products.AsNoTracking().AnyAsync(p => p.Id == item.ProductId, ct);
                if (!productExists)
                {
                    throw new InvalidOperationException($"Phụ tùng/sản phẩm #{item.ProductId} không tồn tại.");
                }

                var price = Math.Max(0m, item.CostPrice ?? item.UnitPrice ?? 0m);
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

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                _context.PurchaseOrders.Add(po);
                await _context.SaveChangesAsync(ct);
                await _inventoryAdminService.RebuildStockAlertsAsync(actorId);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

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

        var po = await _context.PurchaseOrders
            .Include(x => x.PurchaseOrderItems)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (po == null) return null;

        if (PurchaseOrderLifecycleRules.IsTerminalStatus(po.Status))
        {
            throw new InvalidOperationException($"Đơn mua hàng đang ở trạng thái '{po.Status}', không thể chỉnh sửa.");
        }

        var targetBranchId = input.BranchId > 0 ? input.BranchId : po.BranchId;
        var targetWarehouseId = input.DestinationWarehouseId > 0 ? input.DestinationWarehouseId : po.DestinationWarehouseId;
        if ((targetBranchId != po.BranchId || targetWarehouseId != po.DestinationWarehouseId) &&
            await _context.GoodsReceiptNotes.AsNoTracking().AnyAsync(g => g.PurchaseOrderId == id, ct))
        {
            throw new InvalidOperationException("Không thể thay đổi chi nhánh hoặc kho khi đơn đã có phiếu nhập.");
        }
        if (!await _authService.CanCreatePurchaseOrderAsync(targetBranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền chuyển đơn mua hàng đến chi nhánh này.");
        }
        if (!await _context.Warehouses.AsNoTracking().AnyAsync(
            w => w.Id == targetWarehouseId && w.BranchId == targetBranchId, ct))
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

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

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

        var po = await _context.PurchaseOrders
            .Include(x => x.PurchaseOrderItems)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (po == null) throw new KeyNotFoundException("Không tìm thấy đơn mua hàng.");

        if (!PurchaseOrderLifecycleRules.CanApprove(po.Status))
        {
            throw new InvalidOperationException($"Chỉ đơn hàng ở trạng thái PENDING hoặc DRAFT mới có thể duyệt (Hiện tại: '{po.Status}').");
        }

        po.Status = "APPROVED";
        po.ApprovedByStaffId = actor.UserId ?? input?.ApprovedBy;

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

        return (await GetPurchaseOrderByIdAsync(po.Id, actor, ct))!;
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

        // The retry boundary owns the transaction and context state; joining an
        // external transaction would make clearing tracked entities unsafe.
        if (_context.Database.CurrentTransaction != null)
            throw new InvalidOperationException("Không thể nhập kho trong giao dịch đang mở.");

        var effectiveActorId = actor.UserId ?? input.ReceivedByStaffId;
        var now = input.ReceivedAt ?? VietnamTime.Now;
        var note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        // Unique for this invocation so two receipts in the same second cannot collide.
        var receiptCode = $"GRN-{Guid.NewGuid():N}";

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // Each execution-strategy retry must reload state, including after a deadlock.
            _context.ChangeTracker.Clear();
            var affectedProductIds = new HashSet<long>();
            var lineResults = new List<PurchaseOrderReceiveLineResultDto>();
            var grnItems = new List<GoodsReceiptNoteItem>();
            decimal totalReceivedAmount = 0m;

            // Read PO lines and stock under the same serializable SQL transaction.
            await using var tx = _context.Database.IsRelational()
                    ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)
                    : await TryBeginTransactionAsync();
            var committing = false;
            try
            {
                if (_context.Database.IsSqlServer())
                    await _context.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT [Id] FROM [PurchaseOrders] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}", ct);
                var purchaseOrder = await _context.PurchaseOrders.Include(x => x.PurchaseOrderItems)
                    .FirstOrDefaultAsync(x => x.Id == id, ct);
                if (purchaseOrder == null) throw new KeyNotFoundException("Không tìm thấy đơn mua hàng.");
                if (!await _authService.CanReceiveGoodsAsync(id, actor, ct))
                    throw new UnauthorizedAccessException("Không có quyền nhập kho cho đơn mua hàng này.");
                if (!PurchaseOrderLifecycleRules.CanReceive(purchaseOrder.Status))
                    throw new InvalidOperationException($"Đơn mua hàng đang ở trạng thái '{purchaseOrder.Status}', không thể nhận hàng.");
                if (!await _context.Warehouses.AsNoTracking().AnyAsync(x => x.Id == purchaseOrder.DestinationWarehouseId &&
                    x.BranchId == purchaseOrder.BranchId, ct))
                    throw new InvalidOperationException("Kho đích không thuộc chi nhánh của đơn mua hàng.");
                var itemById = purchaseOrder.PurchaseOrderItems.ToDictionary(x => x.Id, x => x);
                var itemByProductId = purchaseOrder.PurchaseOrderItems.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.First());
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
                _context.GoodsReceiptNotes.Add(grn);
                await _context.SaveChangesAsync(ct);

                var receiptInventory = new Dictionary<long, Inventory>();
                foreach (var incoming in input.Items)
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
                        throw new InvalidOperationException("Có dòng nhận hàng không khớp với chi tiết đơn mua hàng.");
                    }

                    // Pure Domain Line Evaluation & Over-receipt Capping
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

                    // Update Inventory with Genuine Moving Average Cost (MAC)
                    if (!receiptInventory.TryGetValue(targetItem.ProductId, out var inventory))
                        inventory = await _context.Inventories.FirstOrDefaultAsync(x =>
                            x.WarehouseId == purchaseOrder.DestinationWarehouseId &&
                            x.ProductId == targetItem.ProductId, ct);

                    if (inventory == null)
                    {
                        var product = await _context.Products.AsNoTracking()
                            .FirstOrDefaultAsync(x => x.Id == targetItem.ProductId, ct);

                        inventory = new Inventory
                        {
                            WarehouseId = purchaseOrder.DestinationWarehouseId,
                            ProductId = targetItem.ProductId,
                            Quantity = acceptedQty,
                            MinStockLevel = product?.MinStockLevel ?? 0m,
                            ReservedQuantity = 0m,
                            CurrentMacPrice = unitPrice,
                            TotalInventoryValue = PurchaseOrderLifecycleRules.CalculateTotalInventoryValue(acceptedQty, unitPrice)
                        };
                        _context.Inventories.Add(inventory);
                        receiptInventory[targetItem.ProductId] = inventory;
                    }
                    else
                    {
                        receiptInventory[targetItem.ProductId] = inventory;
                        // Genuine MAC formula: ((oldQty * oldMac) + (incomingQty * incomingPrice)) / (oldQty + incomingQty)
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
                    _context.GoodsReceiptNoteItems.AddRange(grnItems);
                }

                // Pure Domain PO status evaluation after batch receipt
                var statusChecks = purchaseOrder.PurchaseOrderItems.Select(x =>
                    new PurchaseOrderLifecycleRules.PoLineStatusCheck(x.OrderedQuantity, x.ReceivedQuantity ?? 0m));
                purchaseOrder.Status = PurchaseOrderLifecycleRules.DetermineStatusAfterReceipt(statusChecks);

                await _context.SaveChangesAsync(ct);

                if (affectedProductIds.Count > 0)
                {
                    var relatedAlerts = await _context.StockAlerts
                        .Where(x =>
                            x.IsResolved == false &&
                            x.WarehouseId == purchaseOrder.DestinationWarehouseId &&
                            affectedProductIds.Contains(x.ProductId))
                        .Select(x => x.Id)
                        .ToListAsync(ct);

                    foreach (var alertId in relatedAlerts)
                    {
                        await _inventoryAdminService.ResolveStockAlertAsync(alertId, purchaseOrder.Id, effectiveActorId);
                    }
                }

                await _inventoryAdminService.RebuildStockAlertsAsync(effectiveActorId);
                if (tx != null) { committing = true; await tx.CommitAsync(ct); committing = false; }

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
            }
            catch (Exception ex)
            {
                if (tx != null)
                {
                    try { await tx.RollbackAsync(CancellationToken.None); } catch { /* Preserve the original failure. */ }
                }
                _context.ChangeTracker.Clear();
                // Do not automatically replay an unknown commit; caller must reconcile first.
                if (committing) throw new InvalidOperationException("Không xác định được kết quả nhập kho; kiểm tra phiếu nhập trước khi thử lại.", ex);
                throw;
            }
        });
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

        var purchaseOrder = await _context.PurchaseOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (purchaseOrder == null)
        {
            throw new KeyNotFoundException("Không tìm thấy đơn mua hàng.");
        }

        var grnHeaders = await _context.GoodsReceiptNotes
            .AsNoTracking()
            .Include(x => x.ReceivedByStaff)
            .Include(x => x.Items)
            .Where(x => x.PurchaseOrderId == id)
            .OrderByDescending(x => x.ReceivedAt)
            .ToListAsync(ct);

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

        var purchaseOrder = await _context.PurchaseOrders
            .AsNoTracking()
            .Include(x => x.PurchaseOrderItems)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (purchaseOrder == null)
        {
            throw new KeyNotFoundException("Không tìm thấy đơn mua hàng.");
        }

        var lines = purchaseOrder.PurchaseOrderItems.Select(i =>
        {
            var ordered = i.OrderedQuantity;
            var received = i.ReceivedQuantity ?? 0m;
            var remaining = Math.Max(0m, ordered - received);
            var pct = PurchaseOrderLifecycleRules.CalculateFulfillmentPercentage(ordered, received);
            var lineStatus = remaining <= PurchaseOrderLifecycleRules.QuantityTolerance
                ? "COMPLETED"
                : (received > PurchaseOrderLifecycleRules.QuantityTolerance ? "PARTIAL_RECEIVED" : "PENDING");

            return new PurchaseOrderFulfillmentLineDto(
                i.Id,
                i.ProductId,
                i.Product?.Name,
                i.Product?.Sku,
                ordered,
                received,
                remaining,
                pct,
                lineStatus
            );
        }).ToList();

        var totalOrdered = lines.Sum(l => l.OrderedQuantity);
        var totalReceived = lines.Sum(l => l.ReceivedQuantity);
        var totalRemaining = lines.Sum(l => l.RemainingQuantity);
        var overallPct = PurchaseOrderLifecycleRules.CalculateFulfillmentPercentage(totalOrdered, totalReceived);

        return new PurchaseOrderFulfillmentDto(
            purchaseOrder.Id,
            $"PO-{purchaseOrder.Id:D5}",
            purchaseOrder.Status,
            totalOrdered,
            totalReceived,
            totalRemaining,
            overallPct,
            lines
        );
    }

    public async Task<PurchaseOrderFulfillmentDashboardDto> GetFulfillmentDashboardAsync(
        PurchaseOrderFulfillmentFilterDto filter,
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
            throw new UnauthorizedAccessException("Không có quyền xem dashboard đáp ứng đơn mua hàng.");
        }

        var query = _context.PurchaseOrders
            .AsNoTracking()
            .Include(x => x.PurchaseOrderItems)
                .ThenInclude(i => i.Product)
            .AsQueryable();

        if (targetBranchId.HasValue)
        {
            query = query.Where(x => x.BranchId == targetBranchId.Value);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(x => x.OrderDate >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(x => x.OrderDate <= filter.ToDate.Value);
        }

        var orders = await query
            .OrderByDescending(x => x.OrderDate)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);

        var fulfillmentDtos = orders.Select(po =>
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
        }).ToList();

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
        var targetBranchId = filter.BranchId;
        if (!actor.IsAdmin)
        {
            targetBranchId = actor.BranchId ?? targetBranchId;
        }

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

        var grnQuery = _context.GoodsReceiptNotes
            .AsNoTracking()
            .Include(x => x.Warehouse)
            .Where(x => x.ReceivedAt >= from && x.ReceivedAt < to);

        if (targetBranchId.HasValue)
        {
            grnQuery = grnQuery.Where(x => x.Warehouse.BranchId == targetBranchId.Value);
        }

        var grns = await grnQuery.ToListAsync(ct);
        var grnIds = grns.Select(x => x.Id).ToList();

        var itemRows = await _context.GoodsReceiptNoteItems
            .AsNoTracking()
            .Include(x => x.Product)
            .Where(x => grnIds.Contains(x.GoodsReceiptNoteId))
            .ToListAsync(ct);

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
        var targetBranchId = branchId;
        if (!actor.IsAdmin)
        {
            targetBranchId = actor.BranchId ?? targetBranchId;
        }

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
        var targetBranchId = branchId;
        if (!actor.IsAdmin)
        {
            targetBranchId = actor.BranchId ?? targetBranchId;
        }

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

        var po = await _context.PurchaseOrders
            .Include(x => x.PurchaseOrderItems)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (po == null) return false;

        var grnCount = await _context.GoodsReceiptNotes
            .AsNoTracking()
            .CountAsync(x => x.PurchaseOrderId == id, ct);

        var totalReceived = po.PurchaseOrderItems.Sum(i => i.ReceivedQuantity ?? 0m);

        // Pure Domain Cancellation Constraint Check
        var cancelEval = PurchaseOrderLifecycleRules.EvaluateCancellation(
            new PurchaseOrderLifecycleRules.PoCancellationInput(po.Status, totalReceived, grnCount));

        if (!cancelEval.CanCancel)
        {
            throw new InvalidOperationException(cancelEval.BlockingReason ?? "Không thể xóa đơn mua hàng đã nhận hàng.");
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                if (po.PurchaseOrderItems.Count > 0)
                {
                    _context.PurchaseOrderItems.RemoveRange(po.PurchaseOrderItems);
                }

                _context.PurchaseOrders.Remove(po);
                await _context.SaveChangesAsync(ct);
                await _inventoryAdminService.RebuildStockAlertsAsync(actor.UserId);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

        return true;
    }

    private static PurchaseOrderDetailDto MapToDetailDto(PurchaseOrder po)
    {
        var items = po.PurchaseOrderItems.Select(i =>
        {
            var ord = i.OrderedQuantity;
            var rec = i.ReceivedQuantity ?? 0m;
            var rem = Math.Max(0m, ord - rec);

            return new PurchaseOrderItemDetailDto(
                i.Id,
                i.PurchaseOrderId,
                i.ProductId,
                i.Product?.Sku,
                i.Product?.Name,
                i.Product?.Unit,
                ord,
                ord,
                rec,
                rem,
                i.CostPrice,
                i.CostPrice
            );
        }).ToList();

        var totalAmount = po.TotalAmount ?? items.Sum(i => i.OrderedQuantity * i.CostPrice);

        return new PurchaseOrderDetailDto(
            po.Id,
            $"PO-{po.Id:D5}",
            po.BranchId,
            po.DestinationWarehouseId,
            po.DestinationWarehouse?.Name,
            po.SupplierId,
            po.Supplier?.SupplierName,
            po.OrderDate,
            po.ExpectedDeliveryDate,
            totalAmount,
            po.Status,
            po.ApprovedByStaffId,
            po.ApprovedByStaff?.FullName,
            po.CreatedBy,
            po.CreatedByNavigation?.FullName,
            po.CreatedAt,
            items
        );
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await _context.Database.BeginTransactionAsync();
    }
}
