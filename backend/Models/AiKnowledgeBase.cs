using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class AiKnowledgeBase
{
    public long Id { get; set; }

    public string Topic { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string? SourceUrl { get; set; }

    public bool? IsDynamic { get; set; }

    public DateTime? LastUpdated { get; set; }
}
