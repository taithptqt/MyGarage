using System;
using System.Text.Json.Serialization;

namespace MyGarage.DTOs.Responses.Chatbot;

public class ChatbotMessageResponseDto
{
    public long Id { get; set; }

    [JsonIgnore]
    public long SessionId { get; set; }

    public string Role { get; set; } = null!;

    public string? Content { get; set; }

    public string? AttachmentUrl { get; set; }

    public string? IntentName { get; set; }

    public decimal? SentimentScore { get; set; }

    [JsonIgnore]
    public string? ToolName { get; set; }

    [JsonIgnore]
    public string? ToolArgs { get; set; }

    [JsonIgnore]
    public string? ToolResult { get; set; }

    public DateTime CreatedAt { get; set; }
}
