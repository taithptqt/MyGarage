using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class DocumentAttachment
{
    public long Id { get; set; }

    public string EntityType { get; set; } = null!;

    public long EntityId { get; set; }

    public string FileName { get; set; } = null!;

    public string FileUrl { get; set; } = null!;

    public long? UploadedBy { get; set; }

    public DateTime UploadedAt { get; set; }

    public virtual Staff? UploadedByNavigation { get; set; }
}
