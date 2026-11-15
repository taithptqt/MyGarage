using System;
using System.Linq;
using MyGarage.Application.Purchasing.DTOs;
using MyGarage.Models;

namespace MyGarage.Services;

internal static class PurchaseOrderApplicationMapper
{
    public static PurchaseOrderDetailDto MapToDetailDto(PurchaseOrder po)
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
}
