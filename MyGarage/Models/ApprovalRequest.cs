using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class ApprovalRequest
{
    public long Id { get; set; }

    public string RequestType { get; set; } = null!;

    public long? ServiceOrderDiscountId { get; set; }

    public long? GatePassId { get; set; }

    public long? MaterialReturnId { get; set; }

    public long RequestedBy { get; set; }

    public long? ApproverId { get; set; }

    public string? Status { get; set; }

    public string? Reason { get; set; }

    public string? RejectReason { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Staff? Approver { get; set; }

    public virtual GatePass? GatePass { get; set; }

    public virtual MaterialReturn? MaterialReturn { get; set; }

    public virtual Staff RequestedByNavigation { get; set; } = null!;

    public virtual ServiceOrderDiscount? ServiceOrderDiscount { get; set; }
}
