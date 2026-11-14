using System;

namespace MyGarage.Models;

public partial class QcChecklistTemplateItem
{
    public long Id { get; set; }
    public long TemplateId { get; set; }
    public string? Category { get; set; }
    public string ItemName { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsRequired { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime? CreatedAt { get; set; }

    public virtual QcChecklistTemplate Template { get; set; } = null!;
}
