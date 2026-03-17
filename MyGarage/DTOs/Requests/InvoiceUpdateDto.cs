using System;
namespace MyGarage.DTOs.Requests
{
    public class InvoiceUpdateDto
    {
        public long? ServiceOrderId { get; set; }
        public long? RetailOrderId { get; set; }
        public long? VehicleId { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? InvoiceUrl { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? ReceiveEmail { get; set; }
        public string? TaxCode { get; set; }
        public decimal? SubTotalAmount { get; set; }
        public decimal? TaxAmount { get; set; }
        public decimal? TotalAmount { get; set; }
        public string? PayerType { get; set; }
        public long? PayerId { get; set; }
        public decimal? PaidAmount { get; set; }
        public string? PaymentStatus { get; set; }
        public long? CreditApprovedByStaffId { get; set; }
        public string? CreditNote { get; set; }
        public DateTime? IssuedAt { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
    }
}
