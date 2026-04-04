using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class GatePass
{
    public long Id { get; set; }

    public long ServiceOrderId { get; set; }

    public string ClearanceType { get; set; } = null!;

    public string QrCode { get; set; } = null!;

    public long IssuedBy { get; set; }

    public long? ApprovedBy { get; set; }

    public string? ResolutionNotes { get; set; }

    public string? OldPartsPackingListUrl { get; set; }

    public bool? IsOldPartsChecked { get; set; }

    public string Status { get; set; } = null!;

    public long? ScannedBy { get; set; }

    public DateTime? SecurityCheckedAt { get; set; }

    public DateTime? PassedAt { get; set; }

    public virtual ICollection<ApprovalRequest> ApprovalRequests { get; set; } = new List<ApprovalRequest>();

    public virtual Staff? ApprovedByNavigation { get; set; }

    public virtual Staff IssuedByNavigation { get; set; } = null!;

    public virtual Staff? ScannedByNavigation { get; set; }

    public virtual ServiceOrder ServiceOrder { get; set; } = null!;
}
