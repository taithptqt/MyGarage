using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class ChatbotLlmResponseNormalizer : IChatbotLlmResponseNormalizer
{
    public ChatbotLlmGatewayResponse Normalize(ChatbotLlmProviderRawResponse raw)
    {
        return new ChatbotLlmGatewayResponse
        {
            Succeeded = raw.Succeeded,
            IsTransientFailure = raw.IsTransientFailure,
            FailureCode = raw.FailureCode,
            FailureMessage = raw.FailureMessage,
            Provider = raw.Provider,
            Model = raw.Model,
            LatencyMs = raw.LatencyMs,
            PromptTokens = raw.PromptTokens,
            CompletionTokens = raw.CompletionTokens,
            TotalTokens = raw.TotalTokens,
            AssistantReply = raw.AssistantReply ?? string.Empty,
            ToolCalls = raw.ToolCalls
        };
    }
}
