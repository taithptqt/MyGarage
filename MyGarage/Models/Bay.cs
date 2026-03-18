using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class Bay
{
    public long Id { get; set; }

    public long BranchId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string BayType { get; set; } = null!;

    public decimal? MaxTonnage { get; set; }

    public string? OperationalStatus { get; set; }

    public long? CurrentOccupiedVehicleId { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual Vehicle? CurrentOccupiedVehicle { get; set; }

    public virtual Staff? DeletedByNavigation { get; set; }

    public virtual Staff? UpdatedByNavigation { get; set; }

    public virtual ICollection<WorkSlot> WorkSlots { get; set; } = new List<WorkSlot>();
}
