using System;

namespace MyGarage.DTOs.Responses.Execution
{
    public sealed class TimesheetEntryDto
    {
        public long Id { get; set; }
        public long StaffId { get; set; }
        public long BranchId { get; set; }
        public DateOnly WorkDate { get; set; }
        public DateTime CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public int BreakMinutes { get; set; }
        public string WorkType { get; set; } = "Regular";
        public string Status { get; set; } = "Pending";
        public string? Notes { get; set; }
        public decimal? NetHours { get; set; }
    }
}
