using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class ModelSpecificRule
{
    public long ModelId { get; set; }

    public long ProductId { get; set; }

    public decimal StandardCapacity { get; set; }

    public decimal? Tolerance { get; set; }

    public virtual VehicleModel Model { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;
}
