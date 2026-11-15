using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Common.Pagination;
using MyGarage.Application.Purchasing;
using MyGarage.Application.Purchasing.DTOs;
using MyGarage.DTOs.Requests;
using MyGarage.Extensions;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class PurchaseOrderCommandFacade : IPurchaseOrderCommandFacade
    {
        private readonly IPurchaseOrderApplicationService _poAppService;

        public PurchaseOrderCommandFacade(IPurchaseOrderApplicationService poAppService)
        {
            _poAppService = poAppService;
        }

        public async Task<PurchaseOrderCommandResult<PagedEnvelope<object>>> GetAllAsync(
            long? scopedBranchId,
            string? status,
            int? page,
            int? pageSize,
            bool envelope,
            string? statusGroup,
            UserActorContext actor)
        {
            if (!string.IsNullOrWhiteSpace(statusGroup) && !string.IsNullOrWhiteSpace(status))
            {
                return PurchaseOrderCommandResult<PagedEnvelope<object>>.BadRequest("Specify either status or statusGroup.");
            }

            if (!string.IsNullOrWhiteSpace(statusGroup))
            {
                var normalizedGroup = statusGroup.Trim().ToUpperInvariant();
                if (normalizedGroup is not ("ALL" or "PENDING" or "APPROVED" or "RECEIVED"))
                {
                    return PurchaseOrderCommandResult<PagedEnvelope<object>>.BadRequest("Unknown purchase order status group.");
                }
            }

            try
            {
                var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
                var result = await _poAppService.GetPurchaseOrdersAsync(new PurchaseOrderFilterDto(
                    scopedBranchId,
                    status,
                    statusGroup,
                    normalizedPage,
                    normalizedSize,
                    envelope
                ), actor);

                var items = result.Items.Select(MapPurchaseOrderPayload).ToList();
                return PurchaseOrderCommandResult<PagedEnvelope<object>>.Ok(
                    PagedEnvelope<object>.Create(items, result.Total, normalizedPage, normalizedSize));
            }
            catch (UnauthorizedAccessException)
            {
                return PurchaseOrderCommandResult<PagedEnvelope<object>>.Forbid();
            }
        }

        public async Task<PurchaseOrderCommandResult<object>> GetByIdAsync(
            long id,
            long? scopedBranchId,
            UserActorContext actor)
        {
            try
            {
                var order = await _poAppService.GetPurchaseOrderByIdAsync(id, actor);
                if (order == null)
                {
                    return PurchaseOrderCommandResult<object>.NotFound();
                }

                if (scopedBranchId.HasValue && order.BranchId != scopedBranchId.Value)
                {
                    return PurchaseOrderCommandResult<object>.Forbid();
                }

                return PurchaseOrderCommandResult<object>.Ok(MapPurchaseOrderPayload(order));
            }
            catch (UnauthorizedAccessException)
            {
                return PurchaseOrderCommandResult<object>.Forbid();
            }
        }

        public async Task<PurchaseOrderCommandResult<PurchaseOrderCreatedPayload>> CreateAsync(
            PurchaseOrderCreateDto itemDto,
            long targetBranchId,
            UserActorContext actor)
        {
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
                return PurchaseOrderCommandResult<PurchaseOrderCreatedPayload>.Created(
                    new PurchaseOrderCreatedPayload(created.Id, MapPurchaseOrderPayload(created)));
            }
            catch (UnauthorizedAccessException)
            {
                return PurchaseOrderCommandResult<PurchaseOrderCreatedPayload>.Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return PurchaseOrderCommandResult<PurchaseOrderCreatedPayload>.BadRequest(ex.Message);
            }
        }

        public async Task<PurchaseOrderCommandResult<object>> UpdateAsync(
            long id,
            PurchaseOrderUpdateDto? itemDto,
            UserActorContext actor)
        {
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
                    return PurchaseOrderCommandResult<object>.NotFound();
                }

                return PurchaseOrderCommandResult<object>.Ok(MapPurchaseOrderPayload(updated));
            }
            catch (UnauthorizedAccessException)
            {
                return PurchaseOrderCommandResult<object>.Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return PurchaseOrderCommandResult<object>.BadRequest(ex.Message);
            }
        }

        public async Task<PurchaseOrderCommandResult<object>> ApproveAsync(
            long id,
            long? approvedBy,
            string? note,
            UserActorContext actor)
        {
            try
            {
                var approved = await _poAppService.ApprovePurchaseOrderAsync(
                    id,
                    new PurchaseOrderApproveInput(approvedBy, note),
                    actor
                );

                return PurchaseOrderCommandResult<object>.Ok(MapPurchaseOrderPayload(approved));
            }
            catch (UnauthorizedAccessException)
            {
                return PurchaseOrderCommandResult<object>.Forbid();
            }
            catch (KeyNotFoundException)
            {
                return PurchaseOrderCommandResult<object>.NotFound("Không tìm thấy purchase order.");
            }
            catch (InvalidOperationException ex)
            {
                return PurchaseOrderCommandResult<object>.BadRequest(ex.Message);
            }
        }

        public async Task<PurchaseOrderCommandResult<object>> ReceiveAsync(
            long id,
            PurchaseOrderReceiveRequestDto? request,
            UserActorContext actor)
        {
            if (request == null || request.Items == null || request.Items.Count == 0)
            {
                return PurchaseOrderCommandResult<object>.BadRequest("Thiếu payload nhận hàng.");
            }

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

                return PurchaseOrderCommandResult<object>.Ok(new
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
                return PurchaseOrderCommandResult<object>.Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                return PurchaseOrderCommandResult<object>.NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return PurchaseOrderCommandResult<object>.BadRequest(ex.Message);
            }
        }

        public async Task<PurchaseOrderCommandResult<object>> GetReceiveHistoryAsync(
            long id,
            UserActorContext actor)
        {
            if (id <= 0)
            {
                return PurchaseOrderCommandResult<object>.BadRequest("Mã purchase order không hợp lệ.");
            }

            try
            {
                var history = await _poAppService.GetReceiveHistoryAsync(id, actor);
                var totalReceivedAmount = history.Receipts.Sum(x => x.TotalAmount);

                return PurchaseOrderCommandResult<object>.Ok(new
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
                return PurchaseOrderCommandResult<object>.Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                return PurchaseOrderCommandResult<object>.NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return PurchaseOrderCommandResult<object>.BadRequest(ex.Message);
            }
        }

        public async Task<PurchaseOrderCommandResult<object>> GetFulfillmentAsync(
            long id,
            UserActorContext actor)
        {
            if (id <= 0)
            {
                return PurchaseOrderCommandResult<object>.BadRequest("Mã purchase order không hợp lệ.");
            }

            try
            {
                var fulfillment = await _poAppService.GetFulfillmentAsync(id, actor);
                return PurchaseOrderCommandResult<object>.Ok(new
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
                return PurchaseOrderCommandResult<object>.Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                return PurchaseOrderCommandResult<object>.NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return PurchaseOrderCommandResult<object>.BadRequest(ex.Message);
            }
        }

        public async Task<PurchaseOrderCommandResult<object>> GetFulfillmentDashboardAsync(
            long? scopedBranchId,
            DateOnly? fromDate,
            DateOnly? toDate,
            int page,
            int pageSize,
            UserActorContext actor)
        {
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

                return PurchaseOrderCommandResult<object>.Ok(new
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
                return PurchaseOrderCommandResult<object>.Forbid();
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
    }
}
