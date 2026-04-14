using System;

namespace MyGarage.Models;

public partial class ConversationStaffRead
{
    public long ConversationId { get; set; }

    public long StaffId { get; set; }

    public long? LastReadMessageId { get; set; }

    public DateTime? LastReadAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual ChatMessage? LastReadMessage { get; set; }

    public virtual Staff Staff { get; set; } = null!;
}
