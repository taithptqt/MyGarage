using System;
namespace MyGarage.DTOs.Requests
{
    public class InventoryAuditUpdateDto
    {
        public long WarehouseId { get; set; }
        public string? Status { get; set; }
        public DateTime AuditDate { get; set; }
        public long? ApprovedBy { get; set; }
        public long CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
    }
}
