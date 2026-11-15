using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class ChatbotLlmPromptBuilder : IChatbotLlmPromptBuilder
{
    private const int MaxHistoryMessages = 24;
    private readonly IPiiAnonymizerService _piiAnonymizer;

    public ChatbotLlmPromptBuilder(IPiiAnonymizerService piiAnonymizer)
    {
        ArgumentNullException.ThrowIfNull(piiAnonymizer);
        _piiAnonymizer = piiAnonymizer;
    }

    public IReadOnlyList<ChatbotLlmMessage> BuildMessages(
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<ChatbotMessageResponseDto> history,
        IReadOnlyList<ChatbotToolExecutionRecord>? toolResults = null)
    {
        var messages = new List<ChatbotLlmMessage>();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new ChatbotLlmMessage
            {
                Role = "system",
                Content = systemPrompt.Trim()
            });
        }

        var historyWindow = (history ?? Array.Empty<ChatbotMessageResponseDto>())
            .Where(x => x is not null)
            .TakeLast(MaxHistoryMessages)
            .ToList();

        foreach (var item in historyWindow)
        {
            if (string.IsNullOrWhiteSpace(item.Content))
            {
                continue;
            }

            var role = item.Role?.Trim().ToUpperInvariant();
            if (role == nameof(ChatbotMessageRole.USER))
            {
                messages.Add(new ChatbotLlmMessage
                {
                    Role = "user",
                    Content = _piiAnonymizer.AnonymizeText(item.Content.Trim())
                });
                continue;
            }

            if (role == nameof(ChatbotMessageRole.ASSISTANT))
            {
                messages.Add(new ChatbotLlmMessage
                {
                    Role = "assistant",
                    Content = _piiAnonymizer.AnonymizeText(item.Content.Trim())
                });
            }
        }

        if (toolResults is not null && toolResults.Count > 0)
        {
            var compactPayload = toolResults.Select(x => new
            {
                toolName = x.ToolName,
                toolArgs = _piiAnonymizer.AnonymizeObjectGraph(TryParseJson(x.ToolArgs)),
                toolResult = _piiAnonymizer.AnonymizeObjectGraph(TryParseJson(x.ToolResult))
            });

            messages.Add(new ChatbotLlmMessage
            {
                Role = "system",
                Content =
                    "Server-authoritative tool execution results (JSON). Use these exact values and do not invent missing data: " +
                    JsonSerializer.Serialize(compactPayload)
            });
        }

        if (!string.IsNullOrWhiteSpace(userPrompt))
        {
            messages.Add(new ChatbotLlmMessage
            {
                Role = "user",
                Content = _piiAnonymizer.AnonymizeText(userPrompt.Trim())
            });
        }

        return messages;
    }

    private static object TryParseJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new { };
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch
        {
            return json;
        }
    }
}
