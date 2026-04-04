using System;
namespace MyGarage.DTOs.Requests
{
    public class MaterialReturnCreateDto
    {
        public string? ReturnCode { get; set; }
        public long? ServiceOrderId { get; set; }
        public long MaterialRequestDetailId { get; set; }
        public long ProductId { get; set; }
        public string? ReturnUnit { get; set; }
        public decimal? ReturnedQuantity { get; set; }
        public string? ItemCondition { get; set; }
        public long ReturnedByStaffId { get; set; }
        public long? ReceivedByStaffId { get; set; }
        public DateTime? ReturnTime { get; set; }
        public string? Status { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
    }
}
