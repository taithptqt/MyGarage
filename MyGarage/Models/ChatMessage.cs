using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class ChatMessage
{
    public long Id { get; set; }

    public long ConversationId { get; set; }

    public long SenderId { get; set; }

    public string SenderType { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string? AttachmentUrl { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;
}
