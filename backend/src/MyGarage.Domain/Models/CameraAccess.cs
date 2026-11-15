using System.ComponentModel.DataAnnotations;

namespace MyGarage.Models;

public class CameraDevice
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    [MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(40)] public string Lane { get; set; } = "";
    [MaxLength(10)] public string Direction { get; set; } = "ENTRY";
    public string EncryptedStreamUrl { get; set; } = "";
    [MaxLength(64)] public string CredentialHash { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public double MinConfidence { get; set; } = .85;
    public int RetentionDays { get; set; } = 30;
    public DateTime? LastHeartbeatAt { get; set; }
    [MaxLength(30)] public string Health { get; set; } = "OFFLINE";
    public DateTime UpdatedAt { get; set; }
    public long UpdatedBy { get; set; }
    [Timestamp] public byte[] Version { get; set; } = [];
}

public class VehicleAccessEvent
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long CameraId { get; set; }
    public Guid EventKey { get; set; }
    [MaxLength(80)] public string WorkerInstance { get; set; } = "";
    [MaxLength(10)] public string Direction { get; set; } = "";
    [MaxLength(40)] public string RawPlate { get; set; } = "";
    [MaxLength(40)] public string Plate { get; set; } = "";
    public double Confidence { get; set; }
    public DateTime ObservedAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    [MaxLength(30)] public string Status { get; set; } = "NEEDS_REVIEW";
    [MaxLength(1000)] public string Notes { get; set; } = "";
    public long? VehicleId { get; set; }
    public long? AppointmentId { get; set; }
    public long? ServiceOrderId { get; set; }
    public long? GatePassId { get; set; }
    public long? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public byte[]? Snapshot { get; set; }
    public byte[]? PlateCrop { get; set; }
    [Timestamp] public byte[] Version { get; set; } = [];
}

public class VehicleAccessAudit
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long? EventId { get; set; }
    public long? CameraId { get; set; }
    public long? ActorId { get; set; }
    [MaxLength(40)] public string Action { get; set; } = "";
    [MaxLength(1000)] public string Before { get; set; } = "";
    [MaxLength(1000)] public string After { get; set; } = "";
    [MaxLength(1000)] public string Reason { get; set; } = "";
    public DateTime At { get; set; }
}
