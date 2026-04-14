using System;

namespace MyGarage.Models;

public partial class ChatbotMessage
{
    public long Id { get; set; }

    public long SessionId { get; set; }

    public string Role { get; set; } = null!;

    public string? Content { get; set; }

    public string? AttachmentUrl { get; set; }

    public string? IntentName { get; set; }

    public decimal? SentimentScore { get; set; }

    public string? ToolName { get; set; }

    public string? ToolArgs { get; set; }

    public string? ToolResult { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ChatbotSession Session { get; set; } = null!;
}
