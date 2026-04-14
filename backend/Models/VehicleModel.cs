using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class VehicleModel
{
    public long Id { get; set; }

    public long BrandId { get; set; }

    public string ModelName { get; set; } = null!;

    public string? Segment { get; set; }

    public string? FuelType { get; set; }

    public string? CylinderCapacity { get; set; }

    public int? VehicleClassId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public virtual VehicleBrand Brand { get; set; } = null!;

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual Staff? DeletedByNavigation { get; set; }

    public virtual ICollection<ModelSpecificRule> ModelSpecificRules { get; set; } = new List<ModelSpecificRule>();

    public virtual Staff? UpdatedByNavigation { get; set; }

    public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
