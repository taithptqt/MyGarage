using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class Package
{
    public long Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public long CategoryId { get; set; }

    public decimal BasePrice { get; set; }

    public int StandardMinutes { get; set; }

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public virtual ServiceCategory Category { get; set; } = null!;

    public virtual ICollection<PackageItem> PackageItems { get; set; } = new List<PackageItem>();
}
