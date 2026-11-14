using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class ChartOfAccount
{
    public long Id { get; set; }

    public string AccountCode { get; set; } = null!;

    public string AccountName { get; set; } = null!;

    public long? ParentAccountId { get; set; }

    public string AccountType { get; set; } = null!;

    public bool AllowsPosting { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<ChartOfAccount> InverseParentAccount { get; set; } = new List<ChartOfAccount>();

    public virtual ICollection<JournalEntryLine> JournalEntryLines { get; set; } = new List<JournalEntryLine>();

    public virtual ChartOfAccount? ParentAccount { get; set; }

    public virtual ICollection<SupplierAccountingProfile> SupplierAccountingProfilePayableAccounts { get; set; } = new List<SupplierAccountingProfile>();

    public virtual ICollection<SupplierAccountingProfile> SupplierAccountingProfilePrepaidAccounts { get; set; } = new List<SupplierAccountingProfile>();

    public virtual ICollection<SupplierAccountingProfile> SupplierAccountingProfilePurchaseTaxAccounts { get; set; } = new List<SupplierAccountingProfile>();
}
