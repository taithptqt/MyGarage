using System;
namespace MyGarage.DTOs.Requests
{
    public class StaffScheduleUpdateDto
    {
        public long StaffId { get; set; }
        public DateOnly Date { get; set; }
        public long ShiftId { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public string? Status { get; set; }
    }
}
