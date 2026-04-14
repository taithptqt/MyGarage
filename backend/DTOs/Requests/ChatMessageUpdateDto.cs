using System;
namespace MyGarage.DTOs.Requests
{
    public class ChatMessageUpdateDto
    {
        public long ConversationId { get; set; }
        public Guid? ClientMessageId { get; set; }
        public long SenderId { get; set; }
        public string? SenderType { get; set; }
        public string? MessageType { get; set; }
        public string? Content { get; set; }
        public string? AttachmentUrl { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}
