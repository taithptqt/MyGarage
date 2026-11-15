using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.DTOs.Requests.Chatbot;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public class ChatbotTurnExecutionService : IChatbotTurnExecutionService
{
    private const string HandoffFallbackReply =
        "Mình đã ghi nhận yêu cầu gặp tư vấn viên. Hiện kết nối chuyển tiếp đang gián đoạn, bạn vui lòng thử lại sau ít phút.";

    private readonly IChatbotRetrievalService _retrievalService;
    private readonly IChatbotAnswerComposer _answerComposer;
    private readonly IChatbotBookingWorkflowService _bookingWorkflowService;
    private readonly IChatbotLlmExecutionService _llmExecutionService;
    private readonly IChatbotHandoffService _chatbotHandoffService;
    private readonly IAppLogger<ChatbotTurnExecutionService> _logger;

    public ChatbotTurnExecutionService(
        IChatbotRetrievalService retrievalService,
        IChatbotAnswerComposer answerComposer,
        IChatbotBookingWorkflowService bookingWorkflowService,
        IChatbotLlmExecutionService llmExecutionService,
        IChatbotHandoffService chatbotHandoffService,
        IAppLogger<ChatbotTurnExecutionService> logger)
    {
        _retrievalService = retrievalService;
        _answerComposer = answerComposer;
        _bookingWorkflowService = bookingWorkflowService;
        _llmExecutionService = llmExecutionService;
        _chatbotHandoffService = chatbotHandoffService;
        _logger = logger;
    }

    public async Task<ChatbotTurnExecutionResult> ExecuteTurnAsync(
        long customerId,
        ChatbotSessionResponseDto session,
        IReadOnlyList<ChatbotMessageAttachmentRequestDto> attachments,
        ChatbotTurnPreparationResult preparedTurn,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(preparedTurn);

        var guardrail = preparedTurn.Guardrail;
        var intentRouting = preparedTurn.IntentRouting;
        var detection = intentRouting.Detection;
        var mergedIntent = intentRouting.MergedIntent;
        var shouldHandoff = intentRouting.ShouldHandoff;
        var updatedIntentState = intentRouting.UpdatedIntentState;

        var retrieval = await _retrievalService.RetrieveAsync(
            customerId,
            session.BranchId,
            preparedTurn.ContextualUserInput,
            cancellationToken);
        var deterministicRetrievalReply = _llmExecutionService.ShouldUseDeterministicRetrievalReply(retrieval)
            ? await _answerComposer.ComposeAsync(retrieval, session.BranchId, cancellationToken)
            : null;

        FunctionRoutingResult route;
        string? retrievalComposedReply = null;
        ChatbotBookingWorkflowResponseDto? bookingWorkflow = null;
        IReadOnlyList<ChatbotToolExecutionResult> executedToolResults = [];

        if (shouldHandoff || guardrail.IsRejected)
        {
            route = new FunctionRoutingResult();
        }
        else if (mergedIntent == nameof(ChatbotIntentType.BOOKING))
        {
            route = new FunctionRoutingResult();
            bookingWorkflow = await _bookingWorkflowService.RunAsync(
                customerId,
                session.Id,
                session.BranchId,
                updatedIntentState,
                preparedTurn.ContextualUserInput,
                cancellationToken);
            updatedIntentState = bookingWorkflow.UpdatedIntentState;
            retrievalComposedReply = bookingWorkflow.Prompt;
        }
        else if (!string.IsNullOrWhiteSpace(preparedTurn.ScopedGreetingReply))
        {
            route = new FunctionRoutingResult { AssistantReplyOverride = preparedTurn.ScopedGreetingReply };
        }
        else if (!string.IsNullOrWhiteSpace(deterministicRetrievalReply))
        {
            route = new FunctionRoutingResult { AssistantReplyOverride = deterministicRetrievalReply };
        }
        else
        {
            var execution = await _llmExecutionService.ExecuteAsync(
                customerId,
                session.Id,
                session.BranchId,
                mergedIntent,
                updatedIntentState,
                preparedTurn.Prompt,
                session.Messages,
                attachments,
                cancellationToken);
            route = execution.Route;
            executedToolResults = execution.ExecutedToolResults;
        }

        var handoffResult = shouldHandoff
            ? await TryInitiateHandoffAsync(
                customerId,
                session,
                preparedTurn.ContextualUserInput,
                mergedIntent,
                updatedIntentState,
                detection.Confidence,
                cancellationToken)
            : null;

        var assistantReply = shouldHandoff
            ? handoffResult?.AssistantReply ?? HandoffFallbackReply
            : _llmExecutionService.BuildAssistantReply(
                guardrail,
                detection,
                mergedIntent,
                retrievalComposedReply,
                route.AssistantReplyOverride);

        return new ChatbotTurnExecutionResult
        {
            Route = route,
            ExecutedToolResults = executedToolResults,
            BookingWorkflow = bookingWorkflow,
            UpdatedIntentState = updatedIntentState,
            HandoffResult = handoffResult,
            AssistantReply = assistantReply,
            Retrieval = retrieval
        };
    }

    private async Task<ChatbotHandoffResult?> TryInitiateHandoffAsync(
        long customerId,
        ChatbotSessionResponseDto session,
        string contextualUserInput,
        string mergedIntent,
        string updatedIntentState,
        decimal confidence,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _chatbotHandoffService.InitiateAsync(
                customerId,
                session.BranchId,
                contextualUserInput,
                mergedIntent,
                updatedIntentState,
                confidence,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Chatbot handoff initiation failed. CustomerId={CustomerId}, SessionId={SessionId}",
                customerId,
                session.Id);
            return null;
        }
    }
}
