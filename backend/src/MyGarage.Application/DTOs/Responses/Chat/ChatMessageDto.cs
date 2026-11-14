using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses.Chat
{
    public class ChatMessageDto
    {
        public long Id { get; set; }
        public long ConversationId { get; set; }
        public long SenderId { get; set; }
        public string SenderType { get; set; } = string.Empty;
        public string MessageType { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? AttachmentUrl { get; set; }
        public IReadOnlyList<ChatMessageAttachmentDto> Attachments { get; set; } = Array.Empty<ChatMessageAttachmentDto>();
        public ChatOrderCardDto? OrderCard { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
