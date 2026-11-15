namespace MyGarage.DTOs.Requests
{
    public class VoucherRedemptionRequestDto
    {
        public long? VoucherId { get; set; }
        public string? VoucherCode { get; set; }
        public long? CustomerId { get; set; }
        public long? VehicleId { get; set; }
        public long? BranchId { get; set; }
        public long? ServiceOrderId { get; set; }
        public long? RetailOrderId { get; set; }
        public long? InvoiceId { get; set; }
        public long? PaymentMethodId { get; set; }
        public decimal? EligibleAmount { get; set; }
        public long? AppliedBy { get; set; }
        public string? IdempotencyKey { get; set; }
    }
}

