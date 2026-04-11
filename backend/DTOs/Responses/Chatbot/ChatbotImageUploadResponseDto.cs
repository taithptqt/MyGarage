using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses.Chatbot;

public class ChatbotImageUploadResponseDto
{
    public long SessionId { get; set; }

    public string AttachmentUrl { get; set; } = string.Empty;

    public long UserMessageId { get; set; }

    public long AssistantMessageId { get; set; }

    public ChatbotVisionAnalysisResponseDto Analysis { get; set; } = new();
}

public class ChatbotVisionAnalysisResponseDto
{
    public string ImageSummary { get; set; } = string.Empty;

    public string SuspectedCategory { get; set; } = "UNKNOWN";

    public IReadOnlyList<string> SuggestedServiceCategories { get; set; } = Array.Empty<string>();

    public string Disclaimer { get; set; } = string.Empty;
}
