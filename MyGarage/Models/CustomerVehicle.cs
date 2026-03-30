using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class CustomerVehicle
{
    public long Id { get; set; }

    public long CustomerId { get; set; }

    public long VehicleId { get; set; }

    public bool? IsCurrentOwner { get; set; }

    public DateOnly? OwnershipStartDate { get; set; }

    public DateOnly? TransferDate { get; set; }

    public string? Note { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual Staff? DeletedByNavigation { get; set; }

    public virtual Staff? UpdatedByNavigation { get; set; }

    public virtual Vehicle Vehicle { get; set; } = null!;
}
