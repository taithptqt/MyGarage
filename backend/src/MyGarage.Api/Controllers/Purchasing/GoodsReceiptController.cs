using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Authorization;
using MyGarage.Application.Common.Pagination;
using MyGarage.Application.Purchasing;
using MyGarage.Application.Purchasing.DTOs;
using MyGarage.DTOs.Requests;
using MyGarage.Extensions;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/v1/goods-receipts")]
    [ApiController]
    [Authorize]
    public class GoodsReceiptController : ControllerBase
    {
        public sealed class GoodsReceiptCreateEndpointDto
        {
            public long PurchaseOrderId { get; set; }
            public long? SupplierId { get; set; }
            public string? Notes { get; set; }
            public string? Note { get; set; }
            public long? BranchId { get; set; }
            public long? ReceivedBy { get; set; }
            public DateTime? ReceivedAt { get; set; }
            public List<GoodsReceiptCreateItemEndpointDto> Items { get; set; } = new();
        }

        public sealed class GoodsReceiptCreateItemEndpointDto
        {
            public long? PoItemId { get; set; }
            public long? PurchaseOrderItemId { get; set; }
            public long? ProductId { get; set; }
            public decimal? ReceivedQty { get; set; }
            public decimal? ReceivedQuantity { get; set; }
            public decimal? UnitPrice { get; set; }
            public decimal? CostPrice { get; set; }
        }

        private readonly IGoodsReceiptApplicationService _grnAppService;

        public GoodsReceiptController(
            IGoodsReceiptApplicationService grnAppService)
        {
            _grnAppService = grnAppService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] long? branchId = null,
            [FromQuery] long? purchaseOrderId = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null,
            [FromQuery] bool envelope = false)
        {
            if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var actor = this.ToUserActorContext();
            try
            {
                var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
                var result = await _grnAppService.GetGoodsReceiptsAsync(new GoodsReceiptFilterDto(
                    scopedBranchId,
                    purchaseOrderId,
                    normalizedPage,
                    normalizedSize,
                    envelope
                ), actor);

                var items = result.Items.Select(MapGoodsReceiptPayload).ToList();

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
                var grn = await _grnAppService.GetGoodsReceiptByIdAsync(id, actor);
                if (grn == null)
                {
                    return this.NotFoundProblem("Không tìm thấy phiếu nhập kho.");
                }

                if (scopedBranchId.HasValue && grn.BranchId.HasValue && grn.BranchId.Value != scopedBranchId.Value)
                {
                    return Forbid();
                }

                return Ok(MapGoodsReceiptPayload(grn));
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] GoodsReceiptCreateEndpointDto? payload)
        {
            if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "WAREHOUSE", "STOREKEEPER", "STORE_KEEPER", "INVENTORY", "THU_KHO"))
            {
                return Forbid();
            }

            if (payload == null || payload.PurchaseOrderId <= 0)
            {
                return this.BadRequestProblem("PurchaseOrderId là bắt buộc để lập phiếu nhập kho.");
            }

            if (!this.TryResolveBranchScope(payload.BranchId, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var actor = this.ToUserActorContext();
            try
            {
                var input = new GoodsReceiptCreateInput(
                    payload.PurchaseOrderId,
                    payload.SupplierId,
                    payload.Notes,
                    payload.Note,
                    scopedBranchId,
                    payload.ReceivedBy,
                    payload.ReceivedAt,
                    (payload.Items ?? new List<GoodsReceiptCreateItemEndpointDto>())
                        .Where(x => x != null)
                        .Select(x => new GoodsReceiptCreateItemInput(
                            x.PoItemId,
                            x.PurchaseOrderItemId,
                            x.ProductId,
                            x.ReceivedQty,
                            x.ReceivedQuantity,
                            x.UnitPrice,
                            x.CostPrice
                        )).ToList()
                );

                var result = await _grnAppService.CreateGoodsReceiptAsync(input, actor);

                return CreatedAtAction(nameof(GetById), new { id = result.GoodsReceiptNoteId }, new
                {
                    id = result.GoodsReceiptNoteId,
                    code = result.GoodsReceiptNoteCode,
                    grnCode = result.GoodsReceiptNoteCode,
                    goodsReceiptNoteId = result.GoodsReceiptNoteId,
                    goodsReceiptNoteCode = result.GoodsReceiptNoteCode,
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
                return this.NotFoundProblem(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }

        private static object MapGoodsReceiptPayload(GoodsReceiptDetailDto g)
        {
            return new
            {
                id = g.Id,
                code = g.Code,
                grnCode = g.GrnCode,
                createdAt = g.ReceivedAt,
                receivedAt = g.ReceivedAt,
                purchaseOrderId = g.PurchaseOrderId,
                purchaseOrderCode = g.PurchaseOrderCode,
                warehouseId = g.WarehouseId,
                warehouseName = g.WarehouseName,
                branchId = g.BranchId,
                receivedBy = g.ReceivedBy,
                receiverName = g.ReceiverName,
                status = g.Status,
                note = g.Note,
                totalAmount = g.TotalAmount,
                items = g.Items.Select(i => new
                {
                    id = i.Id,
                    purchaseOrderItemId = i.PurchaseOrderItemId,
                    productId = i.ProductId,
                    productSku = i.ProductSku,
                    productName = i.ProductName,
                    unit = i.Unit,
                    orderedQuantity = i.OrderedQuantity,
                    receivedQuantity = i.ReceivedQuantity,
                    unitCost = i.UnitCost,
                    lineAmount = i.LineAmount
                }).ToList()
            };
        }
    }
}
