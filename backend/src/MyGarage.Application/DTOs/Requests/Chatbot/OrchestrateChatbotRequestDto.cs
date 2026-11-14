using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyGarage.DTOs.Requests.Chatbot;

public class OrchestrateChatbotRequestDto
{
    public long? SessionId { get; set; }

    [StringLength(128)]
    public string? SessionToken { get; set; }

    [Required]
    [StringLength(8000)]
    public string UserMessage { get; set; } = null!;

    public IReadOnlyList<ChatbotMessageAttachmentRequestDto> Attachments { get; set; } = [];

    public bool AllowSendToLockedSession { get; set; }
}
