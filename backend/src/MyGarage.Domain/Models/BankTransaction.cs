using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class BankTransaction
{
    public long Id { get; set; }

    public long BankAccountId { get; set; }

    public DateTime TxnDate { get; set; }

    public string? TxnRef { get; set; }

    public string? Description { get; set; }

    public decimal InAmount { get; set; }

    public decimal OutAmount { get; set; }

    public decimal? BalanceAfter { get; set; }

    public long? PaymentId { get; set; }

    public long? SupplierPaymentId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual BankAccount BankAccount { get; set; } = null!;

    public virtual Payment? Payment { get; set; }

    public virtual SupplierPayment? SupplierPayment { get; set; }
}
