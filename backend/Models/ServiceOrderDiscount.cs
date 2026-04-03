using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class ServiceOrderDiscount
{
    public long Id { get; set; }

    public long ServiceOrderId { get; set; }

    public string DiscountType { get; set; } = null!;

    public long? CustomerTierId { get; set; }

    public long? VoucherId { get; set; }

    public decimal DiscountValue { get; set; }

    public string ValueType { get; set; } = null!;

    public decimal AppliedAmount { get; set; }

    public string? Reason { get; set; }

    public long AppliedBy { get; set; }

    public int? DiscountCategory { get; set; }

    public virtual Staff AppliedByNavigation { get; set; } = null!;

    public virtual ICollection<ApprovalRequest> ApprovalRequests { get; set; } = new List<ApprovalRequest>();

    public virtual CustomerTier? CustomerTier { get; set; }

    public virtual ServiceOrder ServiceOrder { get; set; } = null!;

    public virtual Voucher? Voucher { get; set; }
}
