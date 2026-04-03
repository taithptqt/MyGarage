using System;
namespace MyGarage.DTOs.Requests
{
    public class RetailOrderCreateDto
    {
        public long BranchId { get; set; }
        public string? OrderCode { get; set; }
        public long? CustomerId { get; set; }
        public decimal? SubTotal { get; set; }
        public decimal? TierDiscountAmount { get; set; }
        public long? VoucherId { get; set; }
        public decimal? VoucherDiscountAmount { get; set; }
        public decimal? TotalDiscountAmount { get; set; }
        public decimal? TaxAmount { get; set; }
        public decimal? FinalAmount { get; set; }
        public string? Status { get; set; }
        public string? CustomerNotes { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
    }
}
