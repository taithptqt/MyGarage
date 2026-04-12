using System.Collections.Generic;

namespace MyGarage.DTOs.Responses
{
    public class InvoiceBackfillResponseDto
    {
        public int CompletedPaymentsScanned { get; set; }
        public int LinkedPayments { get; set; }
        public int CreatedInvoices { get; set; }
        public List<long> PaymentIdsUpdated { get; set; } = new();
        public List<long> InvoiceIdsCreated { get; set; } = new();
    }
}
