using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class ServiceRefund
{
    public long Id { get; set; }

    public long ServiceOrderId { get; set; }

    public decimal RefundAmount { get; set; }

    public string? Reason { get; set; }

    public string Status { get; set; } = null!;

    public long? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public string? ApprovalNote { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual Staff? ApprovedByNavigation { get; set; }

    public virtual Staff CreatedByNavigation { get; set; } = null!;

    public virtual ServiceOrder ServiceOrder { get; set; } = null!;

    public virtual Staff? UpdatedByNavigation { get; set; }
}
