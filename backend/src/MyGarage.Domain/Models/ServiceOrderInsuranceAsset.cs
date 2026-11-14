using System;

namespace MyGarage.Models;

public partial class ServiceOrderInsuranceAsset
{
    public long Id { get; set; }

    public long ServiceOrderId { get; set; }

    public string AssetType { get; set; } = null!;

    public string FileUrl { get; set; } = null!;

    public int SortOrder { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual ServiceOrder ServiceOrder { get; set; } = null!;
}
