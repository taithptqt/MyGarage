using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class VehicleConditionLog
{
    public long Id { get; set; }

    public long ServiceOrderId { get; set; }

    public string LogType { get; set; } = null!;

    public int? OdoReading { get; set; }

    public byte? FuelLevel { get; set; }

    public string? DashboardWarningLights { get; set; }

    public string? Valuables { get; set; }

    public string? DefectsAndImages { get; set; }

    public string? CustomerSignatureUrl { get; set; }

    public long? InspectorId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual Staff? Inspector { get; set; }

    public virtual ServiceOrder ServiceOrder { get; set; } = null!;

    public virtual Staff? UpdatedByNavigation { get; set; }
}
