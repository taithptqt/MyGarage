using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Interfaces;

public interface IChatbotLlmProviderClient
{
    bool CanHandle(string provider);

    Task<ChatbotLlmProviderRawResponse> GenerateAsync(
        ChatbotLlmProviderRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ChatbotLlmProviderRequest
{
    public string Provider { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public string? ApiVersion { get; set; }

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public int? MaxTokens { get; set; }

    public decimal? Temperature { get; set; }

    public IReadOnlyList<ChatbotLlmMessage> Messages { get; set; } = [];

    public IReadOnlyList<ChatbotToolDefinition> ToolDefinitions { get; set; } = [];

    public bool AllowToolCalls { get; set; } = true;
}

public sealed class ChatbotLlmProviderRawResponse
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
