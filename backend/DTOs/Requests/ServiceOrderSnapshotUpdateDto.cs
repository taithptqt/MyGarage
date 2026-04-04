using System;
namespace MyGarage.DTOs.Requests
{
    public class ServiceOrderSnapshotUpdateDto
    {
        public long ServiceOrderId { get; set; }
        public int RevisionNumber { get; set; }
        public string? SnapshotData { get; set; }
        public long? CreatedBy { get; set; }
    }
}
