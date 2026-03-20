using System;
namespace MyGarage.DTOs.Requests
{
    public class ShiftUpdateDto
    {
        public long BranchId { get; set; }
        public string? ShiftName { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
    }
}
