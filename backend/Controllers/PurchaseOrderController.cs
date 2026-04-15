using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Common;

namespace MyGarage.Controllers
{
    [Route("api/purchase-orders")]
    [ApiController]
    public class PurchaseOrderController : ControllerBase
    {
        private readonly IPurchaseOrderService _purchaseOrderService;
        private readonly IInventoryAdminService _inventoryAdminService;
        private readonly IGrnSnapshotService _grnSnapshotService;
        private readonly IGarageContext _context;
        private readonly IMapper _mapper;

        public PurchaseOrderController(
            IPurchaseOrderService purchaseOrderService,
            IInventoryAdminService inventoryAdminService,
            IGrnSnapshotService grnSnapshotService,
            IGarageContext context,
            IMapper mapper)
        {
            _purchaseOrderService = purchaseOrderService;
            _inventoryAdminService = inventoryAdminService;
            _grnSnapshotService = grnSnapshotService;
            _context = context;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PurchaseOrderResponseDto>>> GetAll()
        {
            var items = await _purchaseOrderService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<PurchaseOrderResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PurchaseOrderResponseDto>> GetById(long id)
        {
            var item = await _purchaseOrderService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<PurchaseOrderResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<PurchaseOrderResponseDto>> Create([FromBody] PurchaseOrderCreateDto itemDto)
        {
            var entity = _mapper.Map<PurchaseOrder>(itemDto);
            var createdItem = await _purchaseOrderService.AddAsync(entity);
            await _inventoryAdminService.RebuildStockAlertsAsync(this.GetCurrentUserId());
            var responseDto = _mapper.Map<PurchaseOrderResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<PurchaseOrderResponseDto>> Update(long id, [FromBody] PurchaseOrderUpdateDto itemDto)
        {
            var existingItem = await _purchaseOrderService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _purchaseOrderService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            await _inventoryAdminService.RebuildStockAlertsAsync(this.GetCurrentUserId());
            var responseDto = _mapper.Map<PurchaseOrderResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpPost("{id}/receive")]
        public async Task<IActionResult> Receive(long id, [FromBody] PurchaseOrderReceiveRequestDto? request)
        {
            if (id <= 0)
            {
                return BadRequest(new { message = "Ma purchase order khong hop le." });
            }

            if (request == null || request.Items == null || request.Items.Count == 0)
            {
                return BadRequest(new { message = "Thieu danh sach san pham nhan kho." });
            }

            var purchaseOrder = await _context.PurchaseOrders
                .Include(x => x.PurchaseOrderItems)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (purchaseOrder == null)
            {
                return NotFound(new { message = "Khong tim thay purchase order." });
            }

            if (string.Equals(purchaseOrder.Status, nameof(PurchaseOrderStatusCode.CANCELED), StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Purchase order da huy, khong the nhan hang." });
            }

            var warehouse = await _context.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == purchaseOrder.DestinationWarehouseId);
            if (warehouse == null)
            {
                return BadRequest(new { message = "Kho dich khong ton tai." });
            }

            var actorId = request.ReceivedByStaffId ?? this.GetCurrentUserId();
            var now = request.ReceivedAt ?? VietnamTime.Now;
            var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

            var itemById = purchaseOrder.PurchaseOrderItems.ToDictionary(x => x.Id, x => x);
            var itemByProductId = purchaseOrder.PurchaseOrderItems
                .GroupBy(x => x.ProductId)
                .ToDictionary(x => x.Key, x => x.First());

            var affectedProductIds = new HashSet<long>();
            var lineResults = new List<object>();
            var grnItems = new List<GoodsReceiptNoteItem>();
            decimal totalReceivedAmount = 0m;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var grn = new GoodsReceiptNote
                {
                    GrnCode = $"GRN-{now:yyyyMMddHHmmss}-{purchaseOrder.Id}",
                    PurchaseOrderId = purchaseOrder.Id,
                    WarehouseId = purchaseOrder.DestinationWarehouseId,
                    ReceivedAt = now,
                    ReceivedBy = actorId,
                    Status = "POSTED",
                    Note = note,
                    CreatedAt = now
                };
                _context.GoodsReceiptNotes.Add(grn);
                await _context.SaveChangesAsync();

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
                        await transaction.RollbackAsync();
                        return BadRequest(new { message = "Co dong nhan hang khong khop voi item trong purchase order." });
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
                        CreatedBy = actorId
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

                    lineResults.Add(new
                    {
                        purchaseOrderItemId = targetItem.Id,
                        productId = targetItem.ProductId,
                        acceptedQuantity = acceptedQty,
                        orderedQuantity = targetItem.OrderedQuantity,
                        totalReceivedQuantity = targetItem.ReceivedQuantity
                    });
                }

                if (lineResults.Count == 0)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { message = "Khong co so luong hop le de nhan kho." });
                }

                if (grnItems.Count > 0)
                {
                    _context.GoodsReceiptNoteItems.AddRange(grnItems);
                }

                var allCompleted = purchaseOrder.PurchaseOrderItems.All(x => (x.ReceivedQuantity ?? 0m) >= x.OrderedQuantity);
                purchaseOrder.Status = allCompleted
                    ? nameof(PurchaseOrderStatusCode.COMPLETED)
                    : nameof(PurchaseOrderStatusCode.PARTIAL_RECEIVED);

                await _context.SaveChangesAsync();

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
                        await _inventoryAdminService.ResolveStockAlertAsync(alertId, purchaseOrder.Id, actorId);
                    }
                }

                await _inventoryAdminService.RebuildStockAlertsAsync(actorId);
                await transaction.CommitAsync();

                return Ok(new
                {
                    goodsReceiptNoteCode = grn.GrnCode,
                    goodsReceiptNoteId = grn.Id,
                    purchaseOrderId = purchaseOrder.Id,
                    status = purchaseOrder.Status,
                    receivedAt = now,
                    destinationWarehouseId = purchaseOrder.DestinationWarehouseId,
                    totalReceivedAmount,
                    lines = lineResults
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        [HttpGet("{id}/receive-history")]
        public async Task<IActionResult> GetReceiveHistory(long id)
        {
            if (id <= 0)
            {
                return BadRequest(new { message = "Ma purchase order khong hop le." });
            }

            var purchaseOrder = await _context.PurchaseOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            if (purchaseOrder == null)
            {
                return NotFound(new { message = "Khong tim thay purchase order." });
            }

            var grnHeaders = await _context.GoodsReceiptNotes
                .AsNoTracking()
                .Where(x => x.PurchaseOrderId == id)
                .OrderByDescending(x => x.ReceivedAt)
                .ToListAsync();

            var grnIds = grnHeaders.Select(x => x.Id).ToList();
            var grnItems = await _context.GoodsReceiptNoteItems
                .AsNoTracking()
                .Where(x => grnIds.Contains(x.GoodsReceiptNoteId))
                .ToListAsync();

            var productIds = grnItems
                .Select(x => x.ProductId)
                .Distinct()
                .ToList();
            var staffIds = grnHeaders
                .Where(x => x.ReceivedBy.HasValue)
                .Select(x => x.ReceivedBy!.Value)
                .Distinct()
                .ToList();

            var products = await _context.Products
                .AsNoTracking()
                .Where(x => productIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Sku, x.Name, x.Unit })
                .ToDictionaryAsync(x => x.Id, x => x);
            var staffs = await _context.Staffs
                .AsNoTracking()
                .Where(x => staffIds.Contains(x.Id))
                .Select(x => new { x.Id, x.FullName })
                .ToDictionaryAsync(x => x.Id, x => x.FullName);

            var itemByGrnId = grnItems
                .GroupBy(x => x.GoodsReceiptNoteId)
                .ToDictionary(x => x.Key, x => x.ToList());

            var history = grnHeaders
                .Select(grn =>
                {
                    var actorName = grn.ReceivedBy.HasValue && staffs.TryGetValue(grn.ReceivedBy.Value, out var name)
                        ? name
                        : null;
                    itemByGrnId.TryGetValue(grn.Id, out var lines);
                    lines ??= new List<GoodsReceiptNoteItem>();

                    return new
                    {
                        goodsReceiptNoteId = grn.Id,
                        goodsReceiptNoteCode = grn.GrnCode,
                        note = grn.Note,
                        receivedAt = grn.ReceivedAt,
                        receivedByStaffId = grn.ReceivedBy,
                        receivedByStaffName = actorName,
                        lineCount = lines.Count,
                        lineTotalAmount = lines.Sum(x => x.LineAmount),
                        items = lines.Select(line =>
                        {
                            products.TryGetValue(line.ProductId, out var product);
                            return new
                            {
                                goodsReceiptNoteItemId = line.Id,
                                productId = line.ProductId,
                                productSku = product?.Sku,
                                productName = product?.Name,
                                unit = product?.Unit,
                                orderedQuantity = line.OrderedQuantity,
                                receivedQuantity = line.ReceivedQuantity,
                                unitCost = line.UnitCost,
                                lineAmount = line.LineAmount
                            };
                        }).ToList()
                    };
                })
                .ToList();

            var totalReceivedAmount = history.Sum(x => (decimal)x.lineTotalAmount);

            return Ok(new
            {
                purchaseOrderId = purchaseOrder.Id,
                status = purchaseOrder.Status,
                receiptCount = history.Count,
                totalReceivedAmount,
                items = history
            });
        }

        [HttpGet("{id}/fulfillment")]
        public async Task<IActionResult> GetFulfillment(long id)
        {
            if (id <= 0)
            {
                return BadRequest(new { message = "MÃ£ purchase order khÃ´ng há»£p lá»‡." });
            }

            var purchaseOrder = await _context.PurchaseOrders
                .AsNoTracking()
                .Include(x => x.PurchaseOrderItems)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (purchaseOrder == null)
            {
                return NotFound(new { message = "KhÃ´ng tÃ¬m tháº¥y purchase order." });
            }

            var itemRows = purchaseOrder.PurchaseOrderItems
                .Select(x => new
                {
                    x.Id,
                    x.ProductId,
                    OrderedQuantity = x.OrderedQuantity,
                    ReceivedQuantity = x.ReceivedQuantity ?? 0m,
                    RemainingQuantity = Math.Max(0m, x.OrderedQuantity - (x.ReceivedQuantity ?? 0m)),
                    x.CostPrice
                })
                .ToList();

            var productIds = itemRows.Select(x => x.ProductId).Distinct().ToList();
            var products = await _context.Products
                .AsNoTracking()
                .Where(x => productIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Sku, x.Name, x.Unit })
                .ToDictionaryAsync(x => x.Id, x => x);

            var orderedTotalQty = itemRows.Sum(x => x.OrderedQuantity);
            var receivedTotalQty = itemRows.Sum(x => x.ReceivedQuantity);
            var remainingTotalQty = itemRows.Sum(x => x.RemainingQuantity);
            var orderedTotalAmount = itemRows.Sum(x => x.OrderedQuantity * x.CostPrice);
            var receivedTotalAmount = itemRows.Sum(x => x.ReceivedQuantity * x.CostPrice);
            var remainingTotalAmount = itemRows.Sum(x => x.RemainingQuantity * x.CostPrice);
            var fulfillmentPercent = orderedTotalQty <= 0m
                ? 0m
                : Math.Round((receivedTotalQty / orderedTotalQty) * 100m, 2, MidpointRounding.AwayFromZero);

            var items = itemRows
                .Select(x =>
                {
                    products.TryGetValue(x.ProductId, out var product);
                    var linePercent = x.OrderedQuantity <= 0m
                        ? 0m
                        : Math.Round((x.ReceivedQuantity / x.OrderedQuantity) * 100m, 2, MidpointRounding.AwayFromZero);

                    return new
                    {
                        purchaseOrderItemId = x.Id,
                        productId = x.ProductId,
                        productSku = product?.Sku,
                        productName = product?.Name,
                        unit = product?.Unit,
                        orderedQuantity = x.OrderedQuantity,
                        receivedQuantity = x.ReceivedQuantity,
                        remainingQuantity = x.RemainingQuantity,
                        costPrice = x.CostPrice,
                        orderedAmount = x.OrderedQuantity * x.CostPrice,
                        receivedAmount = x.ReceivedQuantity * x.CostPrice,
                        remainingAmount = x.RemainingQuantity * x.CostPrice,
                        lineFulfillmentPercent = linePercent,
                        lineStatus = x.RemainingQuantity <= 0m ? "COMPLETED" : (x.ReceivedQuantity > 0m ? "PARTIAL_RECEIVED" : "PENDING")
                    };
                })
                .OrderByDescending(x => x.lineFulfillmentPercent)
                .ThenBy(x => x.productName)
                .ToList();

            return Ok(new
            {
                purchaseOrderId = purchaseOrder.Id,
                status = purchaseOrder.Status,
                branchId = purchaseOrder.BranchId,
                destinationWarehouseId = purchaseOrder.DestinationWarehouseId,
                supplierId = purchaseOrder.SupplierId,
                orderDate = purchaseOrder.OrderDate,
                expectedDeliveryDate = purchaseOrder.ExpectedDeliveryDate,
                orderedTotalQty,
                receivedTotalQty,
                remainingTotalQty,
                orderedTotalAmount,
                receivedTotalAmount,
                remainingTotalAmount,
                fulfillmentPercent,
                itemCount = items.Count,
                items
            });
        }

        [HttpGet("fulfillment")]
        public async Task<IActionResult> GetFulfillmentDashboard(
            [FromQuery] long? branchId = null,
            [FromQuery] string? status = null,
            [FromQuery] long? supplierId = null,
            [FromQuery] DateOnly? fromDate = null,
            [FromQuery] DateOnly? toDate = null,
            [FromQuery] decimal? minFulfillmentPercent = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 30)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 200) pageSize = 200;

            var query = _context.PurchaseOrders
                .AsNoTracking()
                .Include(x => x.PurchaseOrderItems)
                .AsQueryable();

            if (branchId.HasValue && branchId.Value > 0)
            {
                query = query.Where(x => x.BranchId == branchId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalizedStatus = status.Trim().ToUpperInvariant();
                query = query.Where(x => x.Status == normalizedStatus);
            }

            if (supplierId.HasValue && supplierId.Value > 0)
            {
                query = query.Where(x => x.SupplierId == supplierId.Value);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(x => x.OrderDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(x => x.OrderDate <= toDate.Value);
            }

            var rows = await query
                .OrderByDescending(x => x.OrderDate)
                .ThenByDescending(x => x.CreatedAt ?? DateTime.MinValue)
                .ToListAsync();

            var summaries = rows
                .Select(x =>
                {
                    var orderedQty = x.PurchaseOrderItems.Sum(i => i.OrderedQuantity);
                    var receivedQty = x.PurchaseOrderItems.Sum(i => i.ReceivedQuantity ?? 0m);
                    var remainingQty = x.PurchaseOrderItems.Sum(i => Math.Max(0m, i.OrderedQuantity - (i.ReceivedQuantity ?? 0m)));
                    var orderedAmount = x.PurchaseOrderItems.Sum(i => i.OrderedQuantity * i.CostPrice);
                    var receivedAmount = x.PurchaseOrderItems.Sum(i => (i.ReceivedQuantity ?? 0m) * i.CostPrice);
                    var remainingAmount = x.PurchaseOrderItems.Sum(i => Math.Max(0m, i.OrderedQuantity - (i.ReceivedQuantity ?? 0m)) * i.CostPrice);
                    var percent = orderedQty <= 0m
                        ? 0m
                        : Math.Round((receivedQty / orderedQty) * 100m, 2, MidpointRounding.AwayFromZero);

                    return new
                    {
                        purchaseOrderId = x.Id,
                        branchId = x.BranchId,
                        supplierId = x.SupplierId,
                        destinationWarehouseId = x.DestinationWarehouseId,
                        orderDate = x.OrderDate,
                        expectedDeliveryDate = x.ExpectedDeliveryDate,
                        status = x.Status,
                        itemCount = x.PurchaseOrderItems.Count,
                        orderedTotalQty = orderedQty,
                        receivedTotalQty = receivedQty,
                        remainingTotalQty = remainingQty,
                        orderedTotalAmount = orderedAmount,
                        receivedTotalAmount = receivedAmount,
                        remainingTotalAmount = remainingAmount,
                        fulfillmentPercent = percent
                    };
                })
                .ToList();

            if (minFulfillmentPercent.HasValue)
            {
                summaries = summaries
                    .Where(x => x.fulfillmentPercent >= minFulfillmentPercent.Value)
                    .ToList();
            }

            var total = summaries.Count;
            var items = summaries
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(new
            {
                total,
                page,
                pageSize,
                totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize),
                items
            });
        }

        [HttpGet("grn-kpis")]
        public async Task<IActionResult> GetGrnKpis(
            [FromQuery] long? branchId = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int topN = 10)
        {
            if (topN < 1) topN = 1;
            if (topN > 50) topN = 50;

            var from = (fromDate ?? VietnamTime.Now.Date.AddDays(-30)).Date;
            var to = (toDate ?? VietnamTime.Now.Date).Date.AddDays(1);

            var grnQuery = _context.GoodsReceiptNotes
                .AsNoTracking()
                .Include(x => x.Warehouse)
                .Where(x => x.ReceivedAt >= from && x.ReceivedAt < to);

            if (branchId.HasValue && branchId.Value > 0)
            {
                grnQuery = grnQuery.Where(x => x.Warehouse.BranchId == branchId.Value);
            }

            var grns = await grnQuery
                .OrderByDescending(x => x.ReceivedAt)
                .ToListAsync();

            var grnIds = grns.Select(x => x.Id).ToList();
            var itemRows = await _context.GoodsReceiptNoteItems
                .AsNoTracking()
                .Where(x => grnIds.Contains(x.GoodsReceiptNoteId))
                .ToListAsync();

            var productIds = itemRows.Select(x => x.ProductId).Distinct().ToList();
            var products = await _context.Products
                .AsNoTracking()
                .Where(x => productIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Sku, x.Name, x.Unit })
                .ToDictionaryAsync(x => x.Id, x => x);

            var grnCount = grns.Count;
            var totalReceivedAmount = itemRows.Sum(x => x.LineAmount);
            var totalReceivedQuantity = itemRows.Sum(x => x.ReceivedQuantity);
            var distinctProducts = itemRows.Select(x => x.ProductId).Distinct().Count();

            var dayByGrnId = grns.ToDictionary(x => x.Id, x => x.ReceivedAt.Date);
            var dailyBreakdown = itemRows
                .GroupBy(x => dayByGrnId.TryGetValue(x.GoodsReceiptNoteId, out var d) ? d : DateTime.MinValue.Date)
                .Select(g => new
                {
                    date = g.Key,
                    grnCount = g.Select(x => x.GoodsReceiptNoteId).Distinct().Count(),
                    totalAmount = g.Sum(x => x.LineAmount),
                    totalQuantity = g.Sum(x => x.ReceivedQuantity),
                    distinctProducts = g.Select(x => x.ProductId).Distinct().Count()
                })
                .OrderByDescending(x => x.date)
                .ToList();

            var topProducts = itemRows
                .GroupBy(x => x.ProductId)
                .Select(g =>
                {
                    products.TryGetValue(g.Key, out var product);
                    return new
                    {
                        productId = g.Key,
                        productSku = product?.Sku,
                        productName = product?.Name,
                        unit = product?.Unit,
                        grnLineCount = g.Count(),
                        totalQuantity = g.Sum(x => x.ReceivedQuantity),
                        totalAmount = g.Sum(x => x.LineAmount)
                    };
                })
                .OrderByDescending(x => x.totalAmount)
                .ThenByDescending(x => x.totalQuantity)
                .Take(topN)
                .ToList();

            var branchSummary = grns
                .GroupBy(x => x.Warehouse.BranchId)
                .Select(g =>
                {
                    var branchGrnIds = g.Select(x => x.Id).ToHashSet();
                    var branchItems = itemRows.Where(x => branchGrnIds.Contains(x.GoodsReceiptNoteId)).ToList();
                    return new
                    {
                        branchId = g.Key,
                        grnCount = g.Count(),
                        totalAmount = branchItems.Sum(x => x.LineAmount),
                        totalQuantity = branchItems.Sum(x => x.ReceivedQuantity),
                        distinctProducts = branchItems.Select(x => x.ProductId).Distinct().Count()
                    };
                })
                .OrderByDescending(x => x.totalAmount)
                .ToList();

            return Ok(new
            {
                fromDate = from,
                toDate = to.AddDays(-1),
                branchId,
                overview = new
                {
                    grnCount,
                    totalReceivedAmount,
                    totalReceivedQuantity,
                    distinctProducts
                },
                branchSummary,
                dailyBreakdown,
                topProducts
            });
        }

        [HttpPost("grn-snapshots/rebuild")]
        public async Task<IActionResult> RebuildGrnSnapshot(
            [FromQuery] DateOnly? snapshotDate = null,
            [FromQuery] long? branchId = null)
        {
            var targetDate = snapshotDate ?? DateOnly.FromDateTime(VietnamTime.Now.Date.AddDays(-1));
            var result = await _grnSnapshotService.RebuildDailySnapshotAsync(targetDate, branchId);
            return Ok(result);
        }

        [HttpGet("grn-snapshots/trend")]
        public async Task<IActionResult> GetGrnSnapshotTrend(
            [FromQuery] DateOnly? fromDate = null,
            [FromQuery] DateOnly? toDate = null,
            [FromQuery] long? branchId = null)
        {
            var from = fromDate ?? DateOnly.FromDateTime(VietnamTime.Now.Date.AddDays(-30));
            var to = toDate ?? DateOnly.FromDateTime(VietnamTime.Now.Date);
            var rows = await _grnSnapshotService.GetTrendAsync(from, to, branchId);
            return Ok(new
            {
                fromDate = from,
                toDate = to,
                branchId,
                items = rows
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _purchaseOrderService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            await _inventoryAdminService.RebuildStockAlertsAsync(this.GetCurrentUserId());
            return NoContent();
        }
    }
}







