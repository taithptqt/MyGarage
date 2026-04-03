using System;
namespace MyGarage.DTOs.Requests
{
    public class AuditLogUpdateDto
    {
        public long UserId { get; set; }
        public string? UserType { get; set; }
        public long? BranchId { get; set; }
        public string? Action { get; set; }
        public string? TableName { get; set; }
        public long RecordId { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? SessionId { get; set; }
        public DateTime? Timestamp { get; set; }
    }
}
