using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses
{
    public class BookingGroupResponseDto
    {
        public long Id { get; set; }
        public string GroupCode { get; set; } = string.Empty;
        public long CustomerId { get; set; }
        public long BranchId { get; set; }
        public string Status { get; set; } = string.Empty;
        public int TotalAppointments { get; set; }
        public int ConfirmedAppointments { get; set; }
        public int CanceledAppointments { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? CanceledAt { get; set; }
        public string? CancelReason { get; set; }
        public string? CustomerNote { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<AppointmentDetailResponseDto> Appointments { get; set; } = new();
    }
}

