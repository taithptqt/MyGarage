using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class Conversation
{
    public long Id { get; set; }

    public long? StaffId { get; set; }

    public long CustomerId { get; set; }

    public string? LastMessage { get; set; }

    public long? LastSenderId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? Status { get; set; }

    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();

    public virtual Customer Customer { get; set; } = null!;

    public virtual Staff? Staff { get; set; }
}
