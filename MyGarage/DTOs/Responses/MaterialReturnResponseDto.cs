using System;
namespace MyGarage.DTOs.Responses
{
    public class MaterialReturnResponseDto
    {
        public long Id { get; set; }
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
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public long? UpdatedBy { get; set; }
    }
}
