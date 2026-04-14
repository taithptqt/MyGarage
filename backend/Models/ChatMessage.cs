using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class ChatMessage
{
    public long Id { get; set; }

    public long ConversationId { get; set; }

    public Guid? ClientMessageId { get; set; }

    public long SenderId { get; set; }

    public string SenderType { get; set; } = null!;

    public string? MessageType { get; set; }

    public string Content { get; set; } = null!;

    public string? AttachmentUrl { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual ICollection<ChatMessageAttachment> ChatMessageAttachments { get; set; } = new List<ChatMessageAttachment>();

    public virtual ICollection<ConversationCustomerRead> ConversationCustomerReads { get; set; } = new List<ConversationCustomerRead>();

    public virtual ICollection<ConversationStaffRead> ConversationStaffReads { get; set; } = new List<ConversationStaffRead>();
}
