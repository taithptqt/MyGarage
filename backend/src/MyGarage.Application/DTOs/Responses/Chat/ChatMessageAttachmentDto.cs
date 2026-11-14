using System;

namespace MyGarage.DTOs.Responses.Chat
{
    public class ChatMessageAttachmentDto
    {
        public long Id { get; set; }
        public string AttachmentUrl { get; set; } = string.Empty;
        public string? ContentType { get; set; }
        public long? FileSize { get; set; }
        public int SortOrder { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
