using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class SupplierAccountingProfile
{
    public long Id { get; set; }

    public long SupplierId { get; set; }

    public long? PayableAccountId { get; set; }

    public long? PurchaseTaxAccountId { get; set; }

    public long? PrepaidAccountId { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ChartOfAccount? PayableAccount { get; set; }

    public virtual ChartOfAccount? PrepaidAccount { get; set; }

    public virtual ChartOfAccount? PurchaseTaxAccount { get; set; }

    public virtual Supplier Supplier { get; set; } = null!;
}
