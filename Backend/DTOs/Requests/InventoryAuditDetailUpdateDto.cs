using System;
namespace MyGarage.DTOs.Requests
{
    public class InventoryAuditDetailUpdateDto
    {
        public long AuditId { get; set; }
        public long ProductId { get; set; }
        public decimal? SystemQty { get; set; }
        public decimal? ActualQty { get; set; }
        public decimal? DifferenceQty { get; set; }
        public string? Reason { get; set; }
    }
}
