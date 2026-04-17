using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class QcInspection
{
    public long Id { get; set; }

    public long ServiceOrderId { get; set; }

    public long QcStaffId { get; set; }

    public long? ServiceOrderDetailId { get; set; }

    public string Status { get; set; } = null!;

    public string? FailReason { get; set; }

    public long? ReworkTechnicianId { get; set; }

    public long? TemplateId { get; set; }

    public decimal? OverallScore { get; set; }

    public DateTime? InspectedAt { get; set; }

    public int ReInspectionCount { get; set; }

    public virtual Staff QcStaff { get; set; } = null!;

    public virtual Staff? ReworkTechnician { get; set; }

    public virtual ServiceOrder ServiceOrder { get; set; } = null!;

    public virtual ServiceOrderDetail? ServiceOrderDetail { get; set; }

    public virtual QcChecklistTemplate? Template { get; set; }

    public virtual ICollection<QcChecklistItem> QcChecklistItems { get; set; } = new List<QcChecklistItem>();
}
