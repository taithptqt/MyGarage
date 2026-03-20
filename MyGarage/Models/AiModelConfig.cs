using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class AiModelConfig
{
    public long Id { get; set; }

    public string Provider { get; set; } = null!;

    public string? ApiKey { get; set; }

    public string ModelName { get; set; } = null!;

    public int? MaxTokens { get; set; }

    public decimal? Temperature { get; set; }

    public bool? IsActive { get; set; }
}
