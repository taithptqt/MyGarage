using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class QcChecklistItem
{
    public long Id { get; set; }
    public long QcInspectionId { get; set; }
    public long? TemplateItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public string? Category { get; set; }
    public bool IsRequired { get; set; }
    public string? Result { get; set; }
    public string? FailNote { get; set; }
    public string? PhotoUrl { get; set; }
    public DateTime? CheckedAt { get; set; }
    public long? CheckedBy { get; set; }

    public virtual QcInspection QcInspection { get; set; } = null!;
    public virtual QcChecklistItem? TemplateItem { get; set; }
    public virtual ICollection<QcChecklistItem> InverseTemplateItem { get; set; } = new List<QcChecklistItem>();
    public virtual Staff? CheckedByStaff { get; set; }
}
