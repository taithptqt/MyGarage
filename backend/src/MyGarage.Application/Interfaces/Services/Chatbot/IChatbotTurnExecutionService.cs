using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.DTOs.Requests.Chatbot;
using MyGarage.DTOs.Responses.Chatbot;

namespace MyGarage.Interfaces;

public interface IChatbotTurnExecutionService
{
    Task<ChatbotTurnExecutionResult> ExecuteTurnAsync(
        long customerId,
        ChatbotSessionResponseDto session,
        IReadOnlyList<ChatbotMessageAttachmentRequestDto> attachments,
        ChatbotTurnPreparationResult preparedTurn,
        CancellationToken cancellationToken = default);
}

public sealed class ChatbotTurnExecutionResult
{
    public FunctionRoutingResult Route { get; set; } = new();

    public IReadOnlyList<ChatbotToolExecutionResult> ExecutedToolResults { get; set; } = [];

    public ChatbotBookingWorkflowResponseDto? BookingWorkflow { get; set; }

    public string UpdatedIntentState { get; set; } = "{}";

    public ChatbotHandoffResult? HandoffResult { get; set; }

    public string AssistantReply { get; set; } = string.Empty;

    public ChatbotRetrievalResult Retrieval { get; set; } = new();
}
