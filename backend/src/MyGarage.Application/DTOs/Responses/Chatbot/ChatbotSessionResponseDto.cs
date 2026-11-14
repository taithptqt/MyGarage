using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MyGarage.DTOs.Responses.Chatbot;

public class ChatbotSessionResponseDto
{
    public long Id { get; set; }

    public string SessionToken { get; set; } = null!;

    [JsonIgnore]
    public long CustomerId { get; set; }

    public long? BranchId { get; set; }

    public string Status { get; set; } = null!;

    public string? CurrentIntent { get; set; }

    [JsonIgnore]
    public string? IntentState { get; set; }

    [JsonIgnore]
    public long? HandoffConversationId { get; set; }

    [JsonIgnore]
    public string? HandoffSummary { get; set; }

    public DateTime LastActivityAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public IReadOnlyList<ChatbotMessageResponseDto> Messages { get; set; } = Array.Empty<ChatbotMessageResponseDto>();
}
