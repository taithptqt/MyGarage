using System;
namespace MyGarage.DTOs.Responses
{
    public class RetailReturnResponseDto
    {
        public long Id { get; set; }
        public long RetailOrderId { get; set; }
        public string? ReturnCode { get; set; }
        public decimal? TotalRefundAmount { get; set; }
        public string? Reason { get; set; }
        public string? Status { get; set; }
        public long? ReceivedBy { get; set; }
        public DateTime? ReturnDate { get; set; }
        public long? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public long? UpdatedBy { get; set; }
    }
}
