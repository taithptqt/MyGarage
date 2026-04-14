using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class WorkSlot
{
    public long Id { get; set; }

    public long? ParentSlotId { get; set; }

    public long BranchId { get; set; }

    public long? BayId { get; set; }

    public long? TechnicianId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string SlotType { get; set; } = null!;

    public long? CustomerId { get; set; }

    public long? VehicleId { get; set; }

    public long? AppointmentId { get; set; }

    public long? ServiceOrderId { get; set; }

    public bool IsAdditional { get; set; }

    public int SequenceOrder { get; set; }

    public long? ServiceStaffAssignmentId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? LockedUntil { get; set; }

    public DateTime? ActualStartTime { get; set; }

    public DateTime? ActualReleaseTime { get; set; }

    public string? Notes { get; set; }

    public virtual Appointment? Appointment { get; set; }

    public virtual Bay? Bay { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual Customer? Customer { get; set; }

    public virtual ICollection<WorkSlot> InverseParentSlot { get; set; } = new List<WorkSlot>();

    public virtual WorkSlot? ParentSlot { get; set; }

    public virtual ServiceOrder? ServiceOrder { get; set; }

    public virtual Staff? Technician { get; set; }

    public virtual Vehicle? Vehicle { get; set; }
}
