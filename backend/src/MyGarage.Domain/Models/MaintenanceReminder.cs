using System;

namespace MyGarage.Models;

public partial class MaintenanceReminder
{
    public long Id { get; set; }

    public long CustomerId { get; set; }

    public long VehicleId { get; set; }

    public long? ServiceOrderId { get; set; }

    public string ReminderType { get; set; } = null!;

    public DateTime ScheduledAt { get; set; }

    public int? ReminderMileage { get; set; }

    public string? EstimatedServices { get; set; }

    public string Channel { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime? SentAt { get; set; }

    public string? CustomerResponse { get; set; }

    public long? ResultAppointmentId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual Vehicle Vehicle { get; set; } = null!;

    public virtual ServiceOrder? ServiceOrder { get; set; }

    public virtual Appointment? ResultAppointment { get; set; }
}
