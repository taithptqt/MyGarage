using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Interfaces;

public interface IChatbotLlmGateway
{
    Task<ChatbotLlmGatewayResponse> GenerateAsync(
        ChatbotLlmGatewayRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ChatbotLlmGatewayRequest
{
    public long CustomerId { get; set; }

    public long SessionId { get; set; }

    public long? BranchId { get; set; }

    public string Intent { get; set; } = string.Empty;

    public bool PreferVisionModel { get; set; }

    public IReadOnlyList<ChatbotLlmMessage> Messages { get; set; } = [];

    public IReadOnlyList<ChatbotToolDefinition> ToolDefinitions { get; set; } = [];

    public bool AllowToolCalls { get; set; } = true;
}

public sealed class ChatbotLlmGatewayResponse
{
    public bool Succeeded { get; set; }

    public bool IsTransientFailure { get; set; }

    public string? FailureCode { get; set; }

    public string? FailureMessage { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public long LatencyMs { get; set; }

    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public int TotalTokens { get; set; }

    public string AssistantReply { get; set; } = string.Empty;

    public IReadOnlyList<ChatbotLlmToolCall> ToolCalls { get; set; } = [];
}

public sealed class ChatbotLlmMessage
{
    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string? ToolName { get; set; }

    public string? ToolCallId { get; set; }

    public IReadOnlyList<string> ImageUrls { get; set; } = [];
}

public sealed class ChatbotToolDefinition
{
    public string ToolName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string JsonSchema { get; set; } = "{}";
}

public sealed class ChatbotLlmToolCall
{
    public string ToolName { get; set; } = string.Empty;

    public string ToolArgs { get; set; } = "{}";

    public string? ToolCallId { get; set; }
}
