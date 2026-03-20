using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class AiLearningFeedback
{
    public long Id { get; set; }

    public string AiFeature { get; set; } = null!;

    public string? InputData { get; set; }

    public string? AiOutput { get; set; }

    public string? UserCorrectedOutput { get; set; }

    public bool? IsUseful { get; set; }

    public DateTime? CreatedAt { get; set; }
}
