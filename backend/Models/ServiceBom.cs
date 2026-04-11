using System;

namespace MyGarage.Models;

public partial class ServiceBom
{
    public long Id { get; set; }

    public long ServiceId { get; set; }

    public long VehicleModelId { get; set; }

    public long ProductId { get; set; }

    public decimal Quantity { get; set; }

    public string Unit { get; set; } = null!;

    public bool IsMandatory { get; set; }

    public DateTime CreateAt { get; set; }

    public long? CreateBy { get; set; }

    public DateTime UpdateAt { get; set; }

    public long? UpdateBy { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual StandardService Service { get; set; } = null!;

    public virtual VehicleModel VehicleModel { get; set; } = null!;

    public virtual Staff? CreateByNavigation { get; set; }

    public virtual Staff? UpdateByNavigation { get; set; }
}

