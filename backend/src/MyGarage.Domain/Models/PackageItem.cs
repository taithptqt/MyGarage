using System;

namespace MyGarage.Models;

public partial class PackageItem
{
    public long Id { get; set; }

    public long PackageId { get; set; }

    public long StandardServiceId { get; set; }

    public decimal Quantity { get; set; }

    public int? SortOrder { get; set; }

    public bool? IsDefault { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public virtual Package Package { get; set; } = null!;

    public virtual StandardService StandardService { get; set; } = null!;
}
