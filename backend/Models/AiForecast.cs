using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class AiForecast
{
    public long Id { get; set; }

    public long VehicleId { get; set; }

    public string Type { get; set; } = null!;

    public DateOnly PredictedDate { get; set; }

    public decimal? ConfidenceScore { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Vehicle Vehicle { get; set; } = null!;
}
