using MyGarage.Interfaces;

using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class Role : IAuditEntity
{
    public long Id { get; set; }

    public string RoleName { get; set; } = null!;

    public string RoleCode { get; set; } = null!;

    public string Scope { get; set; } = null!;

    public string? Description { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual Staff? DeletedByNavigation { get; set; }

    public virtual ICollection<Staff> Staff { get; set; } = new List<Staff>();

    public virtual Staff? UpdatedByNavigation { get; set; }
}
