using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class ConsumableReturnBundleItem
{
    public long Id { get; set; }
    public long BundleId { get; set; }
    public long ProductId { get; set; }
    public long? MaterialReqDetailId { get; set; }
    public decimal IssuedQuantity { get; set; }
    public decimal UsedQuantity { get; set; }
    public string Unit { get; set; } = null!;

    public virtual ConsumableReturnBundle Bundle { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
    public virtual MaterialRequestDetail? MaterialReqDetail { get; set; }
}
