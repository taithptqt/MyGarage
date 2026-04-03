using System;
namespace MyGarage.DTOs.Requests
{
    public class RetailReturnUpdateDto
    {
        public long RetailOrderId { get; set; }
        public string? ReturnCode { get; set; }
        public decimal? TotalRefundAmount { get; set; }
        public string? Reason { get; set; }
        public string? Status { get; set; }
        public long? ReceivedBy { get; set; }
        public DateTime? ReturnDate { get; set; }
        public long? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public long CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
    }
}
