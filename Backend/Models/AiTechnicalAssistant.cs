using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class AiTechnicalAssistant
{
    public long Id { get; set; }

    public long MechanicId { get; set; }

    public string Question { get; set; } = null!;

    public string Answer { get; set; } = null!;

    public long? RelatedDiagnoseId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Staff Mechanic { get; set; } = null!;

    public virtual Diagnosis? RelatedDiagnose { get; set; }
}
