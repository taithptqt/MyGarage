using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class RetailReturn
{
    public long Id { get; set; }

    public long RetailOrderId { get; set; }

    public string ReturnCode { get; set; } = null!;

    public decimal? TotalRefundAmount { get; set; }

    public string? Reason { get; set; }

    public string? Status { get; set; }

    public long? ReceivedBy { get; set; }

    public DateTime? ReturnDate { get; set; }

    public long? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual Staff? ApprovedByNavigation { get; set; }

    public virtual ICollection<CommissionTransaction> CommissionTransactions { get; set; } = new List<CommissionTransaction>();

    public virtual Staff CreatedByNavigation { get; set; } = null!;

    public virtual Staff? ReceivedByNavigation { get; set; }

    public virtual RetailOrder RetailOrder { get; set; } = null!;

    public virtual ICollection<RetailReturnItem> RetailReturnItems { get; set; } = new List<RetailReturnItem>();

    public virtual ICollection<StockTransaction> StockTransactions { get; set; } = new List<StockTransaction>();

    public virtual Staff? UpdatedByNavigation { get; set; }
}
