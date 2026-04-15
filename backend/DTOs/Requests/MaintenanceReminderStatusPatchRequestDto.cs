namespace MyGarage.DTOs.Requests;

public sealed class MaintenanceReminderStatusPatchRequestDto
{
    public string Status { get; set; } = string.Empty;

    public string? CustomerResponse { get; set; }

    public long? ResultAppointmentId { get; set; }
}
