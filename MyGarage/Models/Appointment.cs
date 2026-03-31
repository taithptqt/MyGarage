using System;
using System.Collections.Generic;

using MyGarage.Interfaces;

namespace MyGarage.Models;

public partial class Appointment : IAuditEntity
{
    public long Id { get; set; }

    public long BranchId { get; set; }

    public long CustomerId { get; set; }

    public long? VehicleId { get; set; }

    public long? BookingGroupId { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? RequestedServiceIds { get; set; }

    public string? CustomerNotes { get; set; }

    public bool? IsReminderSent { get; set; }

    public string Status { get; set; } = null!;

    public string? CancelReason { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual BookingGroup? BookingGroup { get; set; }

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual ICollection<ServiceOrder> ServiceOrders { get; set; } = new List<ServiceOrder>();

    public virtual Vehicle? Vehicle { get; set; }

    public virtual Staff? UpdatedByNavigation { get; set; }
}
