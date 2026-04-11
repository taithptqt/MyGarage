using System;

namespace MyGarage.Models;

public partial class AppointmentInsuranceUpload
{
    public long Id { get; set; }

    public string AssetType { get; set; } = null!;

    public string BlobPath { get; set; } = null!;

    public string FileUrl { get; set; } = null!;

    public string Status { get; set; } = null!;

    public long? AppointmentId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual Appointment? Appointment { get; set; }
}
