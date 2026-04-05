using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class BookingGroup
{
    public long Id { get; set; }

    public string GroupCode { get; set; } = null!;

    public long CustomerId { get; set; }

    public long BranchId { get; set; }

    public string Status { get; set; } = null!;

    public int TotalAppointments { get; set; }

    public int ConfirmedAppointments { get; set; }

    public int CanceledAppointments { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public DateTime? CanceledAt { get; set; }

    public string? CancelReason { get; set; }

    public string? CustomerNote { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<ServiceOrder> ServiceOrders { get; set; } = new List<ServiceOrder>();

    public virtual Branch Branch { get; set; } = null!;

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual Staff? UpdatedByNavigation { get; set; }
}

