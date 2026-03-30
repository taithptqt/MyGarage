using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class LegalDocument
{
    public long Id { get; set; }

    public string DocType { get; set; } = null!;

    public string? ReferenceNo { get; set; }

    public string Title { get; set; } = null!;

    public string RelatedEntityType { get; set; } = null!;

    public long RelatedEntityId { get; set; }

    public DateOnly? IssuedDate { get; set; }

    public string FileUrl { get; set; } = null!;

    public long? UploadedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Staff? UploadedByNavigation { get; set; }
}
