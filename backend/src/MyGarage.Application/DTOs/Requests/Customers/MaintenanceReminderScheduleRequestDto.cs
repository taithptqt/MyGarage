using System;

namespace MyGarage.DTOs.Requests;

public sealed class MaintenanceReminderScheduleRequestDto
{
    public long CustomerId { get; set; }

    public long VehicleId { get; set; }

    public long? ServiceOrderId { get; set; }

    public string ReminderType { get; set; } = "NextPeriodicService";

    public DateTime? ScheduledAt { get; set; }

    public int? ReminderMileage { get; set; }

    public string? EstimatedServices { get; set; }

    public string? Channel { get; set; }
}
