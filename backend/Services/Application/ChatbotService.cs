using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MyGarage.DTOs.Requests.Chatbot;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public class ChatbotService : IChatbotService
{
    private const int DefaultHistoryTake = 200;
    private readonly IGarageContext _context;
    private readonly IChatbotRequestValidator _validator;
    private readonly IChatbotRateLimitHook _rateLimitHook;
    private readonly ILogger<ChatbotService> _logger;

    public ChatbotService(
        IGarageContext context,
        IChatbotRequestValidator validator,
        IChatbotRateLimitHook rateLimitHook,
        ILogger<ChatbotService> logger)
    {
        _context = context;
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
        await EnsureCustomerIsActiveAsync(customerId, cancellationToken);
        await _rateLimitHook.EnsureCanCreateSessionAsync(customerId, cancellationToken);

        var now = VietnamTime.Now;
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
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

            await using var tx = await TryBeginTransactionAsync(cancellationToken);
            await _context.ChatbotSessions.AddAsync(entity, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            if (tx is not null)
            {
                await tx.CommitAsync(cancellationToken);
            }
            return MapSession(entity, Array.Empty<ChatbotMessage>());
        });
    }

    public async Task<ChatbotSessionResponseDto?> GetSessionByIdAsync(long customerId, long sessionId, CancellationToken cancellationToken = default)
    {
        await EnsureCustomerIsActiveAsync(customerId, cancellationToken);

        var session = await _context.ChatbotSessions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == sessionId && x.CustomerId == customerId, cancellationToken);
        if (session is null)
        {
            return null;
        }

        var history = await LoadHistoryAsync(session.Id, cancellationToken);
        return MapSession(session, history);
    }

    public async Task<ChatbotSessionResponseDto?> GetSessionByTokenAsync(long customerId, string sessionToken, CancellationToken cancellationToken = default)
    {
        await EnsureCustomerIsActiveAsync(customerId, cancellationToken);

        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return null;
        }

        var normalizedToken = sessionToken.Trim();
        var session = await _context.ChatbotSessions.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.SessionToken == normalizedToken && x.CustomerId == customerId,
                cancellationToken);
        if (session is null)
        {
            return null;
        }

        var history = await LoadHistoryAsync(session.Id, cancellationToken);
        return MapSession(session, history);
    }

    public async Task<SaveChatbotMessageResponseDto> SaveMessagesAsync(
        long customerId,
        SaveChatbotMessageRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateAndThrow(_validator.ValidateSaveMessage(request));
        await EnsureCustomerIsActiveAsync(customerId, cancellationToken);
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
        var strategy = _context.Database.CreateExecutionStrategy();
        var baseRole = request.Role.Trim().ToUpperInvariant();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync(cancellationToken);
            var innerPersisted = new List<ChatbotMessage>();

            if (!string.IsNullOrWhiteSpace(request.Content))
            {
                var message = new ChatbotMessage
                {
                    SessionId = session.Id,
                    Role = baseRole,
                    Content = request.Content.Trim(),
                    IntentName = NormalizeNullable(request.IntentName),
                    SentimentScore = request.SentimentScore,
                    CreatedAt = now
                };
                await _context.ChatbotMessages.AddAsync(message, cancellationToken);
                innerPersisted.Add(message);
            }

            if (request.ToolCall is not null)
            {
                var toolCallMessage = new ChatbotMessage
                {
                    SessionId = session.Id,
                    Role = nameof(ChatbotMessageRole.TOOL_CALL),
                    ToolName = request.ToolCall.ToolName.Trim(),
                    ToolArgs = NormalizeNullable(request.ToolCall.ToolArgs),
                    CreatedAt = now
                };
                await _context.ChatbotMessages.AddAsync(toolCallMessage, cancellationToken);
                innerPersisted.Add(toolCallMessage);
            }

            if (request.ToolResult is not null)
            {
                var toolResultMessage = new ChatbotMessage
                {
                    SessionId = session.Id,
                    Role = nameof(ChatbotMessageRole.TOOL_RESULT),
                    ToolName = request.ToolResult.ToolName.Trim(),
                    ToolResult = NormalizeNullable(request.ToolResult.ToolResult),
                    CreatedAt = now
                };
                await _context.ChatbotMessages.AddAsync(toolResultMessage, cancellationToken);
                innerPersisted.Add(toolResultMessage);
            }

            session.LastActivityAt = now;
            session.UpdatedAt = now;
            if (request.SessionState is not null)
            {
                ApplySessionStateUpdate(session, request.SessionState, now);
            }
            else if (IsLockedForNewMessages(session.Status))
            {
                // If explicitly allowed to send into locked sessions, preserve status.
            }
            else if (session.Status == nameof(ChatbotSessionStatus.IDLE))
            {
                session.Status = nameof(ChatbotSessionStatus.ACTIVE);
            }

            await _context.SaveChangesAsync(cancellationToken);
            if (tx is not null)
            {
                await tx.CommitAsync(cancellationToken);
            }

            var responseMessages = innerPersisted
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .Select(MapMessage)
                .ToList();

            var history = await LoadHistoryAsync(session.Id, cancellationToken);

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
        });
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

        var session = await _context.ChatbotSessions
            .FirstOrDefaultAsync(x => x.Id == sessionId && x.CustomerId == customerId, cancellationToken)
            ?? throw new KeyNotFoundException("Session not found.");
        await EnsureCustomerIsActiveAsync(customerId, cancellationToken);

        var now = DateTime.UtcNow;
        ApplySessionStateUpdate(session, request, now);
        session.LastActivityAt = now;
        session.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);

        var history = await LoadHistoryAsync(session.Id, cancellationToken);
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

        var session = await _context.ChatbotSessions
            .FirstOrDefaultAsync(x => x.Id == sessionId && x.CustomerId == customerId, cancellationToken)
            ?? throw new KeyNotFoundException("Session not found.");
        await EnsureCustomerIsActiveAsync(customerId, cancellationToken);

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
        await _context.SaveChangesAsync(cancellationToken);

        var history = await LoadHistoryAsync(session.Id, cancellationToken);
        return MapSession(session, history);
    }

    private async Task<ChatbotSession?> ResolveOwnedSessionAsync(
        long customerId,
        SaveChatbotMessageRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.SessionId.GetValueOrDefault() > 0)
        {
            return await _context.ChatbotSessions
                .FirstOrDefaultAsync(x => x.Id == request.SessionId!.Value && x.CustomerId == customerId, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.SessionToken))
        {
            var token = request.SessionToken!.Trim();
            return await _context.ChatbotSessions
                .FirstOrDefaultAsync(x => x.SessionToken == token && x.CustomerId == customerId, cancellationToken);
        }

        return null;
    }

    private async Task<IReadOnlyList<ChatbotMessage>> LoadHistoryAsync(long sessionId, CancellationToken cancellationToken)
    {
        return await _context.ChatbotMessages.AsNoTracking()
            .Where(x => x.SessionId == sessionId)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Take(DefaultHistoryTake)
            .ToListAsync(cancellationToken);
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

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _context.Database.BeginTransactionAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private async Task EnsureCustomerIsActiveAsync(long customerId, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .AsNoTracking()
            .Where(x => x.Id == customerId)
            .Select(x => new { x.Id, x.IsActive, x.IsDeleted })
            .FirstOrDefaultAsync(cancellationToken);
        if (customer is null || customer.IsDeleted == true)
        {
            throw new KeyNotFoundException("Customer not found.");
        }

        if (customer.IsActive == false)
        {
            throw new InvalidOperationException("Customer is inactive.");
        }
    }
}
