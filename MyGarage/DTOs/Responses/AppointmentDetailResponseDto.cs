using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses
{
    public class AppointmentDetailResponseDto
    {
        public long Id { get; set; }
        public long BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public long CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public long? VehicleId { get; set; }
        public string? VehicleLicensePlate { get; set; }
        public string? VehicleModelName { get; set; }
        public long? BookingGroupId { get; set; }
        public string? BookingGroupCode { get; set; }
        public string? BookingGroupStatus { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? RequestedServiceIds { get; set; }
        public List<string> ServiceNames { get; set; } = new();
        public string? CustomerNotes { get; set; }
        public bool? IsReminderSent { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? CancelReason { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
