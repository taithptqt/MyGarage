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
using MyGarage.Models;

namespace MyGarage.Services;

public class ChatbotService : IChatbotService
{
    private const int DefaultHistoryTake = 200;
    private readonly IChatbotStore _store;
    private readonly IChatbotRequestValidator _validator;
    private readonly IChatbotRateLimitHook _rateLimitHook;
    private readonly IAppLogger<ChatbotService> _logger;

    public ChatbotService(
        IChatbotStore store,
        IChatbotRequestValidator validator,
        IChatbotRateLimitHook rateLimitHook,
        IAppLogger<ChatbotService> logger)
    {
        _store = store;
        _validator = validator;
        _rateLimitHook = rateLimitHook;
        _logger = logger;
    }

    public async Task<ChatbotSessionResponseDto> CreateSessionAsync(
        long customerId,
        long? branchId,
        CreateChatbotSessionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateAndThrow(_validator.ValidateCreateSession(request));
        await _store.EnsureCustomerIsActiveAsync(customerId, cancellationToken);
        await _rateLimitHook.EnsureCanCreateSessionAsync(customerId, cancellationToken);

        var now = VietnamTime.Now;
        var entity = new ChatbotSession
        {
            SessionToken = CreateSessionToken(),
            CustomerId = customerId,
            BranchId = branchId,
            Status = nameof(ChatbotSessionStatus.ACTIVE),
            CurrentIntent = null,
            IntentState = null,
            LastActivityAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        var created = await _store.CreateSessionWithTransactionAsync(entity, cancellationToken);
        return MapSession(created, Array.Empty<ChatbotMessage>());
    }

    public async Task<ChatbotSessionResponseDto?> GetSessionByIdAsync(long customerId, long sessionId, CancellationToken cancellationToken = default)
    {
        await _store.EnsureCustomerIsActiveAsync(customerId, cancellationToken);

        var session = await _store.GetSessionByIdAsync(customerId, sessionId, track: false, cancellationToken);
        if (session is null)
        {
            return null;
        }

        var history = await _store.LoadSessionHistoryAsync(session.Id, DefaultHistoryTake, cancellationToken);
        return MapSession(session, history);
    }

    public async Task<ChatbotSessionResponseDto?> GetSessionByTokenAsync(long customerId, string sessionToken, CancellationToken cancellationToken = default)
    {
        await _store.EnsureCustomerIsActiveAsync(customerId, cancellationToken);

        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return null;
        }

        var normalizedToken = sessionToken.Trim();
        var session = await _store.GetSessionByTokenAsync(customerId, normalizedToken, track: false, cancellationToken);
        if (session is null)
        {
            return null;
        }

        var history = await _store.LoadSessionHistoryAsync(session.Id, DefaultHistoryTake, cancellationToken);
        return MapSession(session, history);
    }

    public async Task<SaveChatbotMessageResponseDto> SaveMessagesAsync(
        long customerId,
        SaveChatbotMessageRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateAndThrow(_validator.ValidateSaveMessage(request));
        await _store.EnsureCustomerIsActiveAsync(customerId, cancellationToken);
        var session = await ResolveOwnedSessionAsync(customerId, request, cancellationToken)
            ?? throw new KeyNotFoundException("Session not found.");

        if (IsHardLockedForNewMessages(session.Status))
        {
            throw new InvalidOperationException("Session is handed off and cannot accept new chatbot messages.");
        }

        if (IsLockedForNewMessages(session.Status) && !request.AllowSendToLockedSession)
        {
            throw new InvalidOperationException("Session is locked and cannot accept new messages.");
        }

        await _rateLimitHook.EnsureCanSendMessageAsync(customerId, session.Id, cancellationToken);

        var now = DateTime.UtcNow;
        var baseRole = request.Role.Trim().ToUpperInvariant();
        var innerPersisted = new List<ChatbotMessage>();

        if (!string.IsNullOrWhiteSpace(request.Content))
        {
            innerPersisted.Add(new ChatbotMessage
            {
                SessionId = session.Id,
                Role = baseRole,
                Content = request.Content.Trim(),
                IntentName = NormalizeNullable(request.IntentName),
                SentimentScore = request.SentimentScore,
                CreatedAt = now
            });
        }

        if (request.ToolCall is not null)
        {
            innerPersisted.Add(new ChatbotMessage
            {
                SessionId = session.Id,
                Role = nameof(ChatbotMessageRole.TOOL_CALL),
                ToolName = request.ToolCall.ToolName.Trim(),
                ToolArgs = NormalizeNullable(request.ToolCall.ToolArgs),
                CreatedAt = now
            });
        }

        if (request.ToolResult is not null)
        {
            innerPersisted.Add(new ChatbotMessage
            {
                SessionId = session.Id,
                Role = nameof(ChatbotMessageRole.TOOL_RESULT),
                ToolName = request.ToolResult.ToolName.Trim(),
                ToolResult = NormalizeNullable(request.ToolResult.ToolResult),
                CreatedAt = now
            });
        }

        await _store.SaveMessagesWithTransactionAsync(
            session,
            innerPersisted,
            s =>
            {
                s.LastActivityAt = now;
                s.UpdatedAt = now;
                if (request.SessionState is not null)
                {
                    ApplySessionStateUpdate(s, request.SessionState, now);
                }
                else if (IsLockedForNewMessages(s.Status))
                {
                    // If explicitly allowed to send into locked sessions, preserve status.
                }
                else if (s.Status == nameof(ChatbotSessionStatus.IDLE))
                {
                    s.Status = nameof(ChatbotSessionStatus.ACTIVE);
                }
            },
            cancellationToken);

        var responseMessages = innerPersisted
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Select(MapMessage)
            .ToList();

        var history = await _store.LoadSessionHistoryAsync(session.Id, DefaultHistoryTake, cancellationToken);

        _logger.LogInformation(
            "Chatbot messages persisted. CustomerId={CustomerId}, SessionId={SessionId}, Count={Count}",
            customerId,
            session.Id,
            responseMessages.Count);

        return new SaveChatbotMessageResponseDto
        {
            Session = MapSession(session, history),
            PersistedMessages = responseMessages
        };
    }

    public async Task<ChatbotSessionResponseDto> UpdateSessionStateAsync(
        long customerId,
        long sessionId,
        ChatbotSessionStateUpdateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var session = await _store.GetSessionByIdAsync(customerId, sessionId, track: true, cancellationToken)
            ?? throw new KeyNotFoundException("Session not found.");
        await _store.EnsureCustomerIsActiveAsync(customerId, cancellationToken);

        var now = DateTime.UtcNow;
        ApplySessionStateUpdate(session, request, now);
        session.LastActivityAt = now;
        session.UpdatedAt = now;
        await _store.SaveChangesAsync(cancellationToken);

        var history = await _store.LoadSessionHistoryAsync(session.Id, DefaultHistoryTake, cancellationToken);
        return MapSession(session, history);
    }

    public async Task<ChatbotSessionResponseDto> MarkSessionHandedOffAsync(
        long customerId,
        long sessionId,
        long conversationId,
        string handoffSummary,
        string? intentState = null,
        CancellationToken cancellationToken = default)
    {
        if (conversationId <= 0)
        {
            throw new InvalidOperationException("ConversationId is required for handoff.");
        }

        if (string.IsNullOrWhiteSpace(handoffSummary))
        {
            throw new InvalidOperationException("HandoffSummary is required for handoff.");
        }

        var session = await _store.GetSessionByIdAsync(customerId, sessionId, track: true, cancellationToken)
            ?? throw new KeyNotFoundException("Session not found.");
        await _store.EnsureCustomerIsActiveAsync(customerId, cancellationToken);

        var now = DateTime.UtcNow;
        session.Status = nameof(ChatbotSessionStatus.HANDED_OFF);
        session.CurrentIntent = nameof(ChatbotIntentType.HANDOFF);
        session.HandoffConversationId = conversationId;
        session.HandoffSummary = handoffSummary.Trim();
        if (intentState is not null)
        {
            session.IntentState = NormalizeNullable(intentState);
        }

        session.LastActivityAt = now;
        session.UpdatedAt = now;
        await _store.SaveChangesAsync(cancellationToken);

        var history = await _store.LoadSessionHistoryAsync(session.Id, DefaultHistoryTake, cancellationToken);
        return MapSession(session, history);
    }

    private async Task<ChatbotSession?> ResolveOwnedSessionAsync(
        long customerId,
        SaveChatbotMessageRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.SessionId.GetValueOrDefault() > 0)
        {
            return await _store.GetSessionByIdAsync(customerId, request.SessionId!.Value, track: true, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.SessionToken))
        {
            var token = request.SessionToken!.Trim();
            return await _store.GetSessionByTokenAsync(customerId, token, track: true, cancellationToken);
        }

        return null;
    }

    private static ChatbotSessionResponseDto MapSession(ChatbotSession session, IReadOnlyList<ChatbotMessage> history)
    {
        return new ChatbotSessionResponseDto
        {
            Id = session.Id,
            SessionToken = session.SessionToken,
            CustomerId = session.CustomerId,
            BranchId = session.BranchId,
            Status = session.Status,
            CurrentIntent = session.CurrentIntent,
            IntentState = session.IntentState,
            HandoffConversationId = session.HandoffConversationId,
            HandoffSummary = session.HandoffSummary,
            LastActivityAt = session.LastActivityAt,
            ClosedAt = session.ClosedAt,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt,
            Messages = history.Select(MapMessage).ToList()
        };
    }

    private static ChatbotMessageResponseDto MapMessage(ChatbotMessage entity)
    {
        return new ChatbotMessageResponseDto
        {
            Id = entity.Id,
            SessionId = entity.SessionId,
            Role = entity.Role,
            Content = entity.Content,
            AttachmentUrl = entity.AttachmentUrl,
            IntentName = entity.IntentName,
            SentimentScore = entity.SentimentScore,
            ToolName = entity.ToolName,
            ToolArgs = entity.ToolArgs,
            ToolResult = entity.ToolResult,
            CreatedAt = entity.CreatedAt
        };
    }

    private static string CreateSessionToken()
    {
        return $"cbs_{Guid.NewGuid():N}";
    }

    private static void ApplySessionStateUpdate(ChatbotSession session, ChatbotSessionStateUpdateRequestDto request, DateTime now)
    {
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToUpperInvariant();
            session.Status = status;
            if (status is nameof(ChatbotSessionStatus.CLOSED) or nameof(ChatbotSessionStatus.EXPIRED))
            {
                session.ClosedAt = now;
            }
        }

        if (request.CurrentIntent is not null)
        {
            session.CurrentIntent = NormalizeNullable(request.CurrentIntent);
        }

        if (request.IntentState is not null)
        {
            session.IntentState = NormalizeNullable(request.IntentState);
        }
    }

    private static bool IsLockedForNewMessages(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        var normalized = status.Trim().ToUpperInvariant();
        return normalized is nameof(ChatbotSessionStatus.EXPIRED)
            or nameof(ChatbotSessionStatus.CLOSED)
            or nameof(ChatbotSessionStatus.ARCHIVED);
    }

    private static bool IsHardLockedForNewMessages(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        return status.Trim().Equals(nameof(ChatbotSessionStatus.HANDED_OFF), StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static void ValidateAndThrow(IReadOnlyList<string> errors)
    {
        if (errors.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(string.Join(" ", errors));
    }
}
