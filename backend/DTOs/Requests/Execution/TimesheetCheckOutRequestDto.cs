using System;
using System.ComponentModel.DataAnnotations;

namespace MyGarage.DTOs.Requests.Execution
{
    public sealed class TimesheetCheckOutRequestDto
    {
        public DateTime? CheckOutTime { get; set; }

        [Range(0, 24 * 60)]
        public int BreakMinutes { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }
    }
}
