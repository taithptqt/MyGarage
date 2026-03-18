using System;
namespace MyGarage.DTOs.Responses
{
    public class ShiftResponseDto
    {
        public long Id { get; set; }
        public long BranchId { get; set; }
        public string? ShiftName { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
    }
}
