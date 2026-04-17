using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class QcChecklistTemplate
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public string? ServiceCategory { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<QcChecklistItem> QcChecklistItems { get; set; } = new List<QcChecklistItem>();
    public virtual ICollection<QcInspection> QcInspections { get; set; } = new List<QcInspection>();
}
