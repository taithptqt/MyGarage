using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class ServiceOrderSnapshot
{
    public long Id { get; set; }

    public long ServiceOrderId { get; set; }

    public int RevisionNumber { get; set; }

    public string SnapshotData { get; set; } = null!;

    public long? CreatedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual ServiceOrder ServiceOrder { get; set; } = null!;
}
