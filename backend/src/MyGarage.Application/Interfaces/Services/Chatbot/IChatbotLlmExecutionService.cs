using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests.Chatbot;
using MyGarage.DTOs.Responses.Chatbot;

namespace MyGarage.Interfaces;

public interface IChatbotLlmExecutionService
{
    bool ShouldUseDeterministicRetrievalReply(ChatbotRetrievalResult retrieval);

    Task<ChatbotLlmExecutionResult> ExecuteAsync(
        long customerId,
        long sessionId,
        long? branchId,
        string mergedIntent,
        string updatedIntentState,
        PromptBuildResult prompt,
        IReadOnlyList<ChatbotMessageResponseDto> sessionMessages,
        IReadOnlyList<ChatbotMessageAttachmentRequestDto> attachments,
        CancellationToken cancellationToken = default);

    string BuildAssistantReply(
        GuardrailValidationResult guardrail,
        IntentDetectionResult detection,
        string intent,
        string? retrievalReply,
        string? overrideReply);
}

public sealed class ChatbotLlmExecutionResult
{
    public FunctionRoutingResult Route { get; set; } = new();

    public IReadOnlyList<ChatbotToolExecutionResult> ExecutedToolResults { get; set; } = [];
}
