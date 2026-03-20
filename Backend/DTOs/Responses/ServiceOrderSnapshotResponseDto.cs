using System;
namespace MyGarage.DTOs.Responses
{
    public class ServiceOrderSnapshotResponseDto
    {
        public long Id { get; set; }
        public long ServiceOrderId { get; set; }
        public int RevisionNumber { get; set; }
        public string? SnapshotData { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
