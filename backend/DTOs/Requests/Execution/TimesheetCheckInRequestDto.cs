using System;
using System.ComponentModel.DataAnnotations;

namespace MyGarage.DTOs.Requests.Execution
{
    public sealed class TimesheetCheckInRequestDto
    {
        [Required]
        public long StaffId { get; set; }

        [Required]
        public long BranchId { get; set; }

        public DateTime? CheckInTime { get; set; }

        [MaxLength(20)]
        public string WorkType { get; set; } = "Regular";

        [MaxLength(1000)]
        public string? Notes { get; set; }
    }
}
