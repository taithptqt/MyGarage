using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class AiContentStore
{
    public long Id { get; set; }

    public string? ContextType { get; set; }

    public string? InputData { get; set; }

    public string? GeneratedContent { get; set; }

    public long? StaffId { get; set; }

    public DateTime? UsedAt { get; set; }

    public virtual Staff? Staff { get; set; }
}
