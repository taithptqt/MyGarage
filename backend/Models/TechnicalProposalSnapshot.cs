using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class TechnicalProposalSnapshot
{
    public long Id { get; set; }

    public long ServiceOrderId { get; set; }

    public long DiagnosisId { get; set; }

    public int VersionNo { get; set; }

    public string Status { get; set; } = null!;

    public long LockedBy { get; set; }

    public DateTime LockedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public virtual ServiceOrder ServiceOrder { get; set; } = null!;

    public virtual Diagnosis Diagnosis { get; set; } = null!;

    public virtual ICollection<TechnicalProposalSnapshotDetail> TechnicalProposalSnapshotDetails { get; set; } = new List<TechnicalProposalSnapshotDetail>();
}
