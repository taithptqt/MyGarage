using System;

namespace MyGarage.Models;

public partial class TechnicalProposalSnapshotDetail
{
    public long Id { get; set; }

    public long SnapshotId { get; set; }


    public string ItemType { get; set; } = null!;

    public long? ItemId { get; set; }

    public string ItemName { get; set; } = null!;

    public decimal Quantity { get; set; }

    public bool IsMandatory { get; set; }

    public string? TechnicianNotes { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual TechnicalProposalSnapshot Snapshot { get; set; } = null!;
}

