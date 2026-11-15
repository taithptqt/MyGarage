using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.DTOs.Requests.Chatbot;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public class ChatbotOrchestrator : IChatbotOrchestrator
{
    private readonly IChatbotService _chatbotService;
    private readonly IChatbotTurnPreparationService _turnPreparationService;
    private readonly IChatbotTurnExecutionService _turnExecutionService;
    private readonly IAppLogger<ChatbotOrchestrator> _logger;

    public ChatbotOrchestrator(
        IChatbotService chatbotService,
        IChatbotTurnPreparationService turnPreparationService,
        IChatbotTurnExecutionService turnExecutionService,
        IAppLogger<ChatbotOrchestrator> logger)
    {
        _chatbotService = chatbotService;
        _turnPreparationService = turnPreparationService;
        _turnExecutionService = turnExecutionService;
        _logger = logger;
    }

    public async Task<OrchestratedChatbotReplyDto> OrchestrateAsync(
        long customerId,
        OrchestrateChatbotRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.UserMessage))
        {
            throw new InvalidOperationException("UserMessage is required.");
        }

        var session = await ResolveSessionAsync(customerId, request, cancellationToken)
            ?? throw new KeyNotFoundException("Session not found.");
        if (session.CustomerId != customerId)
        {
            throw new InvalidOperationException("Session ownership mismatch.");
        }

        var preparedTurn = _turnPreparationService.PrepareTurn(request.UserMessage, session);
        var guardrail = preparedTurn.Guardrail;
        var intentRouting = preparedTurn.IntentRouting;
        var detection = intentRouting.Detection;
        var mergedIntent = intentRouting.MergedIntent;
        var shouldHandoff = intentRouting.ShouldHandoff;
        var fallbackTriggered = intentRouting.FallbackTriggered;

        var execution = await _turnExecutionService.ExecuteTurnAsync(
            customerId,
            session,
            request.Attachments,
            preparedTurn,
            cancellationToken);

        var updatedIntentState = execution.UpdatedIntentState;
        var route = execution.Route;
        var executedToolResults = execution.ExecutedToolResults;
        var bookingWorkflow = execution.BookingWorkflow;
        var handoffResult = execution.HandoffResult;
        var assistantReply = execution.AssistantReply;
        var retrieval = execution.Retrieval;

        await PersistTurnMessagesAsync(
            customerId,
            session.Id,
            request,
            guardrail.SanitizedInput,
            mergedIntent,
            updatedIntentState,
            assistantReply,
            route.FunctionCalls,
            executedToolResults,
            cancellationToken);

        if (shouldHandoff && handoffResult is not null)
        {
            await _chatbotService.MarkSessionHandedOffAsync(
                customerId,
                session.Id,
                handoffResult.ConversationId,
                handoffResult.SummaryJson,
                updatedIntentState,
                cancellationToken);
        }

        _logger.LogInformation(
            "Chatbot orchestration completed. CustomerId={CustomerId}, SessionId={SessionId}, Intent={Intent}, Confidence={Confidence:0.00}, Rejected={Rejected}, Handoff={Handoff}, RetrievalType={RetrievalType}, Grounded={Grounded}, Sources={SourceCount}",
            customerId,
            session.Id,
            mergedIntent,
            detection.Confidence,
            guardrail.IsRejected,
            shouldHandoff && handoffResult is not null,
            retrieval.QueryType,
            retrieval.IsGrounded,
            retrieval.Sources.Count);

        return new OrchestratedChatbotReplyDto
        {
            SessionId = session.Id,
            SessionToken = session.SessionToken,
            Intent = mergedIntent,
            Confidence = detection.Confidence,
            Strategy = fallbackTriggered ? "DETERMINISTIC_WITH_FALLBACK" : detection.Strategy,
            IsRejected = guardrail.IsRejected,
            RejectionReason = guardrail.RejectionMessage,
            ShouldHandoff = shouldHandoff && handoffResult is not null,
            AssistantReply = assistantReply,
            BookingWorkflow = bookingWorkflow,
            UpdatedIntentState = updatedIntentState,
            FunctionCalls = route.FunctionCalls.Select(x => new OrchestratedFunctionCallDto
            {
                ToolName = x.ToolName,
                ToolArgs = x.ToolArgs
            }).ToList(),
            OrchestratedAt = DateTime.UtcNow
        };
    }

    private async Task<ChatbotSessionResponseDto?> ResolveSessionAsync(
        long customerId,
        OrchestrateChatbotRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.SessionId.GetValueOrDefault() > 0)
        {
            var byId = await _chatbotService.GetSessionByIdAsync(customerId, request.SessionId!.Value, cancellationToken);
            if (byId is not null)
            {
                return byId;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.SessionToken))
        {
            return await _chatbotService.GetSessionByTokenAsync(customerId, request.SessionToken.Trim(), cancellationToken);
        }

        return null;
    }

    private async Task PersistTurnMessagesAsync(
        long customerId,
        long sessionId,
        OrchestrateChatbotRequestDto request,
        string sanitizedInput,
        string mergedIntent,
        string updatedIntentState,
        string assistantReply,
        IReadOnlyList<FunctionCallDescriptor> functionCalls,
        IReadOnlyList<ChatbotToolExecutionResult> executedToolResults,
        CancellationToken cancellationToken)
    {
        var sessionState = new ChatbotSessionStateUpdateRequestDto
        {
            CurrentIntent = mergedIntent,
            IntentState = updatedIntentState
        };

        await _chatbotService.SaveMessagesAsync(customerId, new SaveChatbotMessageRequestDto
        {
            SessionId = sessionId,
            Role = nameof(ChatbotMessageRole.USER),
            Content = sanitizedInput,
            IntentName = mergedIntent,
            Attachments = request.Attachments,
            SessionState = sessionState,
            AllowSendToLockedSession = request.AllowSendToLockedSession
        }, cancellationToken);

        for (var i = 0; i < functionCalls.Count; i++)
        {
            var call = functionCalls[i];
            await _chatbotService.SaveMessagesAsync(customerId, new SaveChatbotMessageRequestDto
            {
                SessionId = sessionId,
                Role = nameof(ChatbotMessageRole.ASSISTANT),
                ToolCall = new ChatbotToolCallRequestDto
                {
                    ToolName = call.ToolName,
                    ToolArgs = call.ToolArgs
                },
                AllowSendToLockedSession = request.AllowSendToLockedSession
            }, cancellationToken);

            if (i < executedToolResults.Count)
            {
                var toolResult = executedToolResults[i];
                await _chatbotService.SaveMessagesAsync(customerId, new SaveChatbotMessageRequestDto
                {
                    SessionId = sessionId,
                    Role = nameof(ChatbotMessageRole.ASSISTANT),
                    ToolResult = new ChatbotToolResultRequestDto
                    {
                        ToolName = toolResult.ToolName,
                        ToolResult = toolResult.ToolResult
                    },
                    AllowSendToLockedSession = request.AllowSendToLockedSession
                }, cancellationToken);
            }
        }

        await _chatbotService.SaveMessagesAsync(customerId, new SaveChatbotMessageRequestDto
        {
            SessionId = sessionId,
            Role = nameof(ChatbotMessageRole.ASSISTANT),
            Content = assistantReply,
            IntentName = mergedIntent,
            SessionState = sessionState,
            AllowSendToLockedSession = request.AllowSendToLockedSession
        }, cancellationToken);
    }
}
