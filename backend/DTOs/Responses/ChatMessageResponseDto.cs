using System;
namespace MyGarage.DTOs.Responses
{
    public class ChatMessageResponseDto
    {
        public long Id { get; set; }
        public long ConversationId { get; set; }
        public long SenderId { get; set; }
        public string? SenderType { get; set; }
        public string? Content { get; set; }
        public string? AttachmentUrl { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
