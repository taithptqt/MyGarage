using System;

namespace MyGarage.Models;

public partial class ConversationCustomerRead
{
    public long ConversationId { get; set; }

    public long CustomerId { get; set; }

    public long? LastReadMessageId { get; set; }

    public DateTime? LastReadAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual Customer Customer { get; set; } = null!;

    public virtual ChatMessage? LastReadMessage { get; set; }
}
