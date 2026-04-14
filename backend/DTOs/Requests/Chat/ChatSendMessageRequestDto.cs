using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Requests.Chat
{
    public class ChatSendMessageRequestDto
    {
        public Guid? ClientMessageId { get; set; }
        public string? MessageType { get; set; }
        public string? Content { get; set; }
        public string? AttachmentUrl { get; set; }
        public IReadOnlyList<string>? AttachmentUrls { get; set; }
        public ChatOrderCardRequestDto? OrderCard { get; set; }
    }
}
