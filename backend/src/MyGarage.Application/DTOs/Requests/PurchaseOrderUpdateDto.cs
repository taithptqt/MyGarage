using System;
namespace MyGarage.DTOs.Requests
{
    public class PurchaseOrderUpdateDto
    {
        public long BranchId { get; set; }
        public long DestinationWarehouseId { get; set; }
        public long SupplierId { get; set; }
        public DateOnly OrderDate { get; set; }
        public DateOnly? ExpectedDeliveryDate { get; set; }
        public decimal? TotalAmount { get; set; }
        public string? Status { get; set; }
        public long? ApprovedByStaffId { get; set; }
        public long? CreatedBy { get; set; }
    }
}
