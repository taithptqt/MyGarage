using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class QcChecklistTemplate
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? ServiceCategory { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<QcChecklistTemplateItem> TemplateItems { get; set; } = new List<QcChecklistTemplateItem>();
    public virtual ICollection<QcChecklistItem> QcChecklistItems { get; set; } = new List<QcChecklistItem>();
    public virtual ICollection<QcInspection> QcInspections { get; set; } = new List<QcInspection>();
}
