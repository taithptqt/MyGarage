using System;
namespace MyGarage.DTOs.Requests
{
    public class InventoryAuditCreateDto
    {
        public long WarehouseId { get; set; }
        public string? Status { get; set; }
        public DateTime AuditDate { get; set; }
        public long? ApprovedBy { get; set; }
        public long CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
    }
}
