using System;

namespace MyGarage.Models;

public partial class ChatMessageAttachment
{
    public long Id { get; set; }

    public long MessageId { get; set; }

    public string AttachmentUrl { get; set; } = null!;

    public string? ContentType { get; set; }

    public long? FileSize { get; set; }

    public int SortOrder { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ChatMessage Message { get; set; } = null!;
}
