using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Authorization;
using MyGarage.Application.Common.Pagination;
using MyGarage.Application.Purchasing;
using MyGarage.Application.Purchasing.DTOs;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.Extensions;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/purchase-orders")]
    [Route("api/v1/purchase-orders")]
    [ApiController]
    [Authorize]
    public class PurchaseOrderController : ControllerBase
    {
        public sealed record PurchaseOrderApproveRequestDto(long? ApprovedBy, string? Note);

        private readonly IPurchaseOrderApplicationService _poAppService;
        private readonly IPurchaseOrderAuthorizationService _authService;
        private readonly IInventoryAdminService _inventoryAdminService;
        private readonly IGrnSnapshotService _grnSnapshotService;
        private readonly IGarageContext _context;

        public PurchaseOrderController(
            IPurchaseOrderApplicationService poAppService,
            IPurchaseOrderAuthorizationService authService,
            IInventoryAdminService inventoryAdminService,
            IGrnSnapshotService grnSnapshotService,
            IGarageContext context)
        {
            _poAppService = poAppService;
            _authService = authService;
            _inventoryAdminService = inventoryAdminService;
            _grnSnapshotService = grnSnapshotService;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] long? branchId = null,
            [FromQuery] string? status = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null,
            [FromQuery] bool envelope = false,
            [FromQuery] string? statusGroup = null)
        {
            if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            if (!string.IsNullOrWhiteSpace(statusGroup) && !string.IsNullOrWhiteSpace(status))
                return BadRequest(new { message = "Specify either status or statusGroup." });

            if (!string.IsNullOrWhiteSpace(statusGroup))
            {
                var normalizedGroup = statusGroup.Trim().ToUpperInvariant();
                if (normalizedGroup is not ("ALL" or "PENDING" or "APPROVED" or "RECEIVED"))
                {
                    return BadRequest(new { message = "Unknown purchase order status group." });
                }
            }

            var actor = this.ToUserActorContext();
            try
            {
                var result = await _poAppService.GetPurchaseOrdersAsync(new PurchaseOrderFilterDto(
                    scopedBranchId,
                    status,
                    statusGroup,
                    page,
                    pageSize,
                    envelope
                ), actor);

                var items = result.Items.Select(MapPurchaseOrderPayload).ToList();

                var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
                return Ok(PagedEnvelope<object>.Create(items, result.Total, normalizedPage, normalizedSize));
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById(long id)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var actor = this.ToUserActorContext();
            try
            {
                var order = await _poAppService.GetPurchaseOrderByIdAsync(id, actor);
                if (order == null)
                {
                    return NotFound();
                }

                if (scopedBranchId.HasValue && order.BranchId != scopedBranchId.Value)
                {
                    return Forbid();
                }

                return Ok(MapPurchaseOrderPayload(order));
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PurchaseOrderCreateDto itemDto)
        {
            if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "SERVICE_MANAGER", "STAFF", "OWNER", "DIRECTOR", "QUAN_DOC", "STOREKEEPER", "STORE_KEEPER"))
            {
                return Forbid();
            }

            if (itemDto == null)
            {
                return BadRequest(new { message = "Dữ liệu đơn mua hàng không hợp lệ." });
            }

            if (!this.TryResolveRequiredBranchId(
                    itemDto.BranchId > 0 ? itemDto.BranchId : null,
                    out var targetBranchId,
                    out var denied))
            {
                return denied!;
            }

            var actor = this.ToUserActorContext();
            try
            {
                var itemsInput = itemDto.Items?.Select(i => new PurchaseOrderCreateItemInput(
                    i.ProductId,
                    i.OrderedQuantity,
                    i.Qty,
                    i.CostPrice,
                    i.UnitPrice
                )).ToList();

                var input = new PurchaseOrderCreateInput(
                    targetBranchId,
                    itemDto.DestinationWarehouseId,
                    itemDto.SupplierId,
                    itemDto.OrderDate,
                    itemDto.ExpectedDeliveryDate,
                    itemDto.ExpectedDate,
                    itemDto.Notes,
                    itemDto.TotalAmount,
                    itemDto.Status,
                    itemDto.ApprovedByStaffId,
                    itemDto.CreatedBy,
                    itemsInput
                );

                var created = await _poAppService.CreatePurchaseOrderAsync(input, actor);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapPurchaseOrderPayload(created));
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id:long}")]
        public async Task<IActionResult> Update(long id, [FromBody] PurchaseOrderUpdateDto itemDto)
        {
            if (!this.TryResolveBranchScope(itemDto?.BranchId > 0 ? itemDto.BranchId : null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var actor = this.ToUserActorContext();
            try
            {
                var input = new PurchaseOrderUpdateInput(
                    itemDto?.BranchId ?? 0,
                    itemDto?.DestinationWarehouseId ?? 0,
                    itemDto?.SupplierId ?? 0,
                    itemDto?.OrderDate ?? default,
                    itemDto?.ExpectedDeliveryDate,
                    itemDto?.TotalAmount,
                    itemDto?.Status,
                    itemDto?.ApprovedByStaffId,
                    itemDto?.CreatedBy
                );

                var updated = await _poAppService.UpdatePurchaseOrderAsync(id, input, actor);
                if (updated == null)
                {
                    return NotFound();
                }

                return Ok(MapPurchaseOrderPayload(updated));
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id:long}/approve")]
        public async Task<IActionResult> Approve(long id, [FromBody] PurchaseOrderApproveRequestDto? payload = null)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "SERVICE_MANAGER", "OWNER", "DIRECTOR", "QUAN_DOC"))
            {
                return Forbid();
            }

            var actor = this.ToUserActorContext();
            try
            {
                var approved = await _poAppService.ApprovePurchaseOrderAsync(
                    id,
                    new PurchaseOrderApproveInput(payload?.ApprovedBy, payload?.Note),
                    actor
                );

                return Ok(MapPurchaseOrderPayload(approved));
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Không tìm thấy purchase order." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id:long}/receive")]
        public async Task<IActionResult> Receive(long id, [FromBody] PurchaseOrderReceiveRequestDto? request)
        {
            if (request == null || request.Items == null || request.Items.Count == 0)
            {
                return BadRequest(new { message = "Thiếu payload nhận hàng." });
            }

            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "WAREHOUSE", "STOREKEEPER", "STORE_KEEPER", "INVENTORY", "THU_KHO"))
            {
                return Forbid();
            }

            var actor = this.ToUserActorContext();
            try
            {
                var input = new PurchaseOrderReceiveInput(
                    request.ReceivedAt,
                    request.Note,
                    actor.UserId ?? request.ReceivedByStaffId,
                    request.Items.Select(x => new PurchaseOrderReceiveItemInput(
                        x.PurchaseOrderItemId,
                        x.ProductId,
                        x.ReceivedQuantity,
                        x.CostPrice
                    )).ToList()
                );

                var result = await _poAppService.ReceivePurchaseOrderAsync(id, input, actor);

                return Ok(new
                {
                    goodsReceiptNoteCode = result.GoodsReceiptNoteCode,
                    goodsReceiptNoteId = result.GoodsReceiptNoteId,
                    purchaseOrderId = result.PurchaseOrderId,
                    status = result.Status,
                    receivedAt = result.ReceivedAt,
                    destinationWarehouseId = result.DestinationWarehouseId,
                    totalReceivedAmount = result.TotalReceivedAmount,
                    lines = result.Lines.Select(l => new
                    {
                        purchaseOrderItemId = l.PurchaseOrderItemId,
                        productId = l.ProductId,
                        acceptedQuantity = l.AcceptedQuantity,
                        orderedQuantity = l.OrderedQuantity,
                        totalReceivedQuantity = l.TotalReceivedQuantity
                    }).ToList()
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { message = "Tồn kho vừa được cập nhật bởi giao dịch khác, vui lòng tải lại và thử lại." });
            }
        }

        [HttpGet("{id}/receive-history")]
        public async Task<IActionResult> GetReceiveHistory(long id)
        {
            if (id <= 0)
            {
                return BadRequest(new { message = "Mã purchase order không hợp lệ." });
            }

            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var actor = this.ToUserActorContext();
            try
            {
                var history = await _poAppService.GetReceiveHistoryAsync(id, actor);
                var totalReceivedAmount = history.Receipts.Sum(x => x.TotalAmount);

                return Ok(new
                {
                    purchaseOrderId = history.PurchaseOrderId,
                    purchaseOrderCode = history.PurchaseOrderCode,
                    receiptCount = history.Receipts.Count,
                    totalReceivedAmount,
                    items = history.Receipts.Select(r => new
                    {
                        goodsReceiptNoteId = r.Id,
                        goodsReceiptNoteCode = r.Code,
                        receivedAt = r.ReceivedAt,
                        receivedByStaffName = r.ReceivedByName,
                        status = r.Status,
                        lineTotalAmount = r.TotalAmount,
                        note = r.Note
                    }).ToList()
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}/fulfillment")]
        public async Task<IActionResult> GetFulfillment(long id)
        {
            if (id <= 0)
            {
                return BadRequest(new { message = "Mã purchase order không hợp lệ." });
            }

            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var actor = this.ToUserActorContext();
            try
            {
                var fulfillment = await _poAppService.GetFulfillmentAsync(id, actor);
                return Ok(new
                {
                    purchaseOrderId = fulfillment.PurchaseOrderId,
                    purchaseOrderCode = fulfillment.PurchaseOrderCode,
                    status = fulfillment.Status,
                    orderedTotalQty = fulfillment.TotalOrderedQuantity,
                    receivedTotalQty = fulfillment.TotalReceivedQuantity,
                    remainingTotalQty = fulfillment.TotalRemainingQuantity,
                    fulfillmentPercent = fulfillment.FulfillmentPercentage,
                    itemCount = fulfillment.Lines.Count,
                    items = fulfillment.Lines.Select(l => new
                    {
                        purchaseOrderItemId = l.ItemId,
                        productId = l.ProductId,
                        productName = l.ProductName,
                        productSku = l.ProductSku,
                        orderedQuantity = l.OrderedQuantity,
                        receivedQuantity = l.ReceivedQuantity,
                        remainingQuantity = l.RemainingQuantity,
                        lineFulfillmentPercent = l.FulfillmentPercentage,
                        lineStatus = l.LineStatus
                    }).ToList()
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("fulfillment")]
        public async Task<IActionResult> GetFulfillmentDashboard(
            [FromQuery] long? branchId = null,
            [FromQuery] DateOnly? fromDate = null,
            [FromQuery] DateOnly? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 30)
        {
            if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var actor = this.ToUserActorContext();
            try
            {
                var dashboard = await _poAppService.GetFulfillmentDashboardAsync(new PurchaseOrderFulfillmentFilterDto(
                    scopedBranchId,
                    fromDate,
                    toDate
                ), actor);

                var pagedItems = dashboard.Orders
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return Ok(new
                {
                    total = dashboard.TotalOrders,
                    page,
                    pageSize,
                    totalPages = dashboard.TotalOrders == 0 ? 0 : (int)Math.Ceiling(dashboard.TotalOrders / (double)pageSize),
                    completedOrders = dashboard.CompletedOrders,
                    partialOrders = dashboard.PartialOrders,
                    pendingOrders = dashboard.PendingOrders,
                    overallFulfillmentRate = dashboard.OverallFulfillmentRate,
                    items = pagedItems
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpGet("grn-kpis")]
        public async Task<IActionResult> GetGrnKpis(
            [FromQuery] long? branchId = null,
            [FromQuery] DateOnly? fromDate = null,
            [FromQuery] DateOnly? toDate = null)
        {
            if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var actor = this.ToUserActorContext();
            try
            {
                var kpis = await _poAppService.GetGrnKpisAsync(new GrnKpiFilterDto(
                    scopedBranchId,
                    fromDate,
                    toDate
                ), actor);

                return Ok(kpis);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpPost("grn-snapshots/rebuild")]
        public async Task<IActionResult> RebuildGrnSnapshot(
            [FromQuery] DateOnly? snapshotDate = null,
            [FromQuery] long? branchId = null)
        {
            if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var actor = this.ToUserActorContext();
            try
            {
                var result = await _poAppService.RebuildGrnSnapshotAsync(snapshotDate, scopedBranchId, actor);
                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpGet("grn-snapshots/trend")]
        public async Task<IActionResult> GetGrnSnapshotTrend(
            [FromQuery] DateOnly? fromDate = null,
            [FromQuery] DateOnly? toDate = null,
            [FromQuery] long? branchId = null)
        {
            if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var actor = this.ToUserActorContext();
            try
            {
                var result = await _poAppService.GetGrnSnapshotTrendAsync(fromDate, toDate, scopedBranchId, actor);
                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "DIRECTOR", "OWNER"))
            {
                return Forbid();
            }

            var actor = this.ToUserActorContext();
            try
            {
                var result = await _poAppService.DeletePurchaseOrderAsync(id, actor);
                if (!result)
                {
                    return NotFound();
                }
                return NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private static object MapPurchaseOrderPayload(PurchaseOrderDetailDto po)
        {
            return new
            {
                id = po.Id,
                code = po.Code,
                branchId = po.BranchId,
                destinationWarehouseId = po.DestinationWarehouseId,
                warehouseName = po.WarehouseName,
                supplierId = po.SupplierId,
                supplierName = po.SupplierName,
                orderDate = po.OrderDate,
                expectedDeliveryDate = po.ExpectedDeliveryDate,
                totalAmount = po.TotalAmount,
                status = po.Status,
                approvedByStaffId = po.ApprovedByStaffId,
                approvedByName = po.ApprovedByName,
                createdBy = po.CreatedBy,
                createdByName = po.CreatedByName,
                createdAt = po.CreatedAt,
                items = po.Items.Select(i => new
                {
                    id = i.Id,
                    purchaseOrderId = i.PurchaseOrderId,
                    productId = i.ProductId,
                    qty = i.Qty,
                    orderedQuantity = i.OrderedQuantity,
                    receivedQuantity = i.ReceivedQuantity,
                    remainingQuantity = i.RemainingQuantity,
                    unitPrice = i.UnitPrice,
                    costPrice = i.CostPrice,
                    product = i.Sku == null && i.ProductName == null ? null : new
                    {
                        id = i.ProductId,
                        sku = i.Sku,
                        name = i.ProductName,
                        unit = i.Unit
                    }
                }).ToList()
            };
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
}
