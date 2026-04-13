using System;
namespace MyGarage.DTOs.Requests
{
    public class PaymentUpdateDto
    {
        public long? ShiftClosingId { get; set; }
        public long? CashierId { get; set; }
        public long? ServiceOrderId { get; set; }
        public long? InvoiceId { get; set; }
        public long? RetailOrderId { get; set; }
        public string? PayerType { get; set; }
        public long? PayerId { get; set; }
        public long MethodId { get; set; }
        public decimal? Amount { get; set; }
        public string? TransactionCode { get; set; }
        public string? Type { get; set; }
        public long? ReversedPaymentId { get; set; }
    }
}
