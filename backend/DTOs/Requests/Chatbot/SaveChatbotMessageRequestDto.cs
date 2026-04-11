using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyGarage.DTOs.Requests.Chatbot;

public class SaveChatbotMessageRequestDto
{
    public long? SessionId { get; set; }

    [StringLength(128)]
    public string? SessionToken { get; set; }

    [Required]
    [StringLength(20)]
    public string Role { get; set; } = null!;

    public string? Content { get; set; }

    [StringLength(100)]
    public string? IntentName { get; set; }

    public decimal? SentimentScore { get; set; }

    public ChatbotToolCallRequestDto? ToolCall { get; set; }

    public ChatbotToolResultRequestDto? ToolResult { get; set; }

    public ChatbotSessionStateUpdateRequestDto? SessionState { get; set; }

    public IReadOnlyList<ChatbotMessageAttachmentRequestDto> Attachments { get; set; } = [];

    public bool AllowSendToLockedSession { get; set; }
}
