using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Requests
{
    public class PurchaseOrderCreateDto
    {
        public long BranchId { get; set; }
        public long DestinationWarehouseId { get; set; }
        public long SupplierId { get; set; }
        public DateOnly OrderDate { get; set; }
        public DateOnly? ExpectedDeliveryDate { get; set; }
        public DateOnly? ExpectedDate { get; set; }
        public string? Notes { get; set; }
        public decimal? TotalAmount { get; set; }
        public string? Status { get; set; }
        public long? ApprovedByStaffId { get; set; }
        public long? CreatedBy { get; set; }
        public List<PurchaseOrderCreateItemDto>? Items { get; set; }
    }

    public class PurchaseOrderCreateItemDto
    {
        public long ProductId { get; set; }
        public decimal? OrderedQuantity { get; set; }
        public decimal? Qty { get; set; }
        public decimal? CostPrice { get; set; }
        public decimal? UnitPrice { get; set; }
    }
}
