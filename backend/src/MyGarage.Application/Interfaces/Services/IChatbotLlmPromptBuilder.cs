using System.Collections.Generic;
using MyGarage.DTOs.Responses.Chatbot;

namespace MyGarage.Interfaces;

public interface IChatbotLlmPromptBuilder
{
    IReadOnlyList<ChatbotLlmMessage> BuildMessages(
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<ChatbotMessageResponseDto> history,
        IReadOnlyList<ChatbotToolExecutionRecord>? toolResults = null);
}

public sealed class ChatbotToolExecutionRecord
{
    public string ToolName { get; set; } = string.Empty;

    public string ToolArgs { get; set; } = "{}";

    public string ToolResult { get; set; } = "{}";

    public string? ToolCallId { get; set; }
}
