using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class ServiceStaffAssignment
{
    public long Id { get; set; }

    public long ServiceOrderDetailId { get; set; }

    public long? AssignedBy { get; set; }

    public long TechnicianId { get; set; }

    public string RoleInTask { get; set; } = null!;

    public decimal SplitRatio { get; set; }

    public decimal? EstimatedCommission { get; set; }

    public DateTime? ActualStartTime { get; set; }

    public string? PauseLog { get; set; }

    public DateTime? ActualEndTime { get; set; }

    public string? Status { get; set; }

    public bool? IsRework { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual Staff? AssignedByNavigation { get; set; }

    public virtual ICollection<CommissionTransaction> CommissionTransactions { get; set; } = new List<CommissionTransaction>();

    public virtual ServiceOrderDetail ServiceOrderDetail { get; set; } = null!;

    public virtual Staff Technician { get; set; } = null!;

    public virtual Staff? UpdatedByNavigation { get; set; }
}
