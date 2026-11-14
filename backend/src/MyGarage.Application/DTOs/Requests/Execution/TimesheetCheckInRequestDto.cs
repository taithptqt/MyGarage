using System;
using System.ComponentModel.DataAnnotations;

namespace MyGarage.DTOs.Requests.Execution
{
    public sealed class TimesheetCheckInRequestDto
    {
        [Required]
        public long StaffId { get; set; }

        public long? BranchId { get; set; }

        [Range(-90, 90)]
        public decimal? Latitude { get; set; }

        [Range(-180, 180)]
        public decimal? Longitude { get; set; }

        [Range(0, 100000)]
        public decimal? AccuracyMeters { get; set; }

        public DateTime? CheckInTime { get; set; }

        [MaxLength(20)]
        public string WorkType { get; set; } = "Regular";

        [MaxLength(1000)]
        public string? Notes { get; set; }
    }
}
