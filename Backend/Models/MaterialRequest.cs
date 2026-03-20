using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class MaterialRequest
{
    public long Id { get; set; }

    public long? ServiceOrderId { get; set; }

    public long? TechnicianId { get; set; }

    public long? StorekeeperId { get; set; }

    public DateTime? RequestDate { get; set; }

    public string? Status { get; set; }

    public bool? IsRework { get; set; }

    public string? Note { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ServiceOrder? ServiceOrder { get; set; }

    public virtual Staff? Storekeeper { get; set; }

    public virtual Staff? Technician { get; set; }
}
