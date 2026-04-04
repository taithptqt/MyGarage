using System;
namespace MyGarage.DTOs.Requests
{
    public class BranchExceptionCreateDto
    {
        public long BranchId { get; set; }
        public DateOnly ExceptionDate { get; set; }
        public bool? IsClosed { get; set; }
        public TimeOnly? StartTime { get; set; }
        public TimeOnly? EndTime { get; set; }
        public string? Reason { get; set; }
        public long? CreatedByStaffId { get; set; }
        public long? ApprovedByStaffId { get; set; }
    }
}
