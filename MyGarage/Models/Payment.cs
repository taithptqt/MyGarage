using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class Payment
{
    public long Id { get; set; }

    public long? ShiftClosingId { get; set; }

    public long? CashierId { get; set; }

    public long? ServiceOrderId { get; set; }

    public long? InvoiceId { get; set; }

    public long? RetailOrderId { get; set; }

    public string? PayerType { get; set; }

    public long? PayerId { get; set; }

    public long MethodId { get; set; }

    public decimal Amount { get; set; }

    public string? TransactionCode { get; set; }

    public string Type { get; set; } = null!;

    public string? Status { get; set; }

    public long? ReversedPaymentId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual Staff? Cashier { get; set; }

    public virtual ICollection<Payment> InverseReversedPayment { get; set; } = new List<Payment>();

    public virtual Invoice? Invoice { get; set; }

    public virtual PaymentMethod Method { get; set; } = null!;

    public virtual RetailOrder? RetailOrder { get; set; }

    public virtual Payment? ReversedPayment { get; set; }

    public virtual ServiceOrder? ServiceOrder { get; set; }

    public virtual ShiftClosing? ShiftClosing { get; set; }

    public virtual Staff? UpdatedByNavigation { get; set; }
}
