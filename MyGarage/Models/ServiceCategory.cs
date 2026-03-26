using MyGarage.Interfaces;

using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class ServiceCategory : IAuditEntity
{
    public long Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public long? ParentCategoryId { get; set; }

    public string? Description { get; set; }

    public bool? IsActive { get; set; }

    public int? DisplayOrder { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual Staff? DeletedByNavigation { get; set; }

    public virtual ICollection<ServiceCategory> InverseParentCategory { get; set; } = new List<ServiceCategory>();

    public virtual ServiceCategory? ParentCategory { get; set; }

    public virtual ICollection<StandardService> StandardServices { get; set; } = new List<StandardService>();

    public virtual Staff? UpdatedByNavigation { get; set; }
}
