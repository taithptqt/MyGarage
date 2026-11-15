using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Chatbot;

public interface IChatbotSessionStore
{
    Task EnsureCustomerIsActiveAsync(long customerId, CancellationToken ct = default);
    Task<ChatbotSession> CreateSessionWithTransactionAsync(ChatbotSession entity, CancellationToken ct = default);
    Task<ChatbotSession?> GetSessionByIdAsync(long customerId, long sessionId, bool track = false, CancellationToken ct = default);
    Task<ChatbotSession?> GetSessionByTokenAsync(long customerId, string sessionToken, bool track = false, CancellationToken ct = default);
    Task<IReadOnlyList<ChatbotMessage>> LoadSessionHistoryAsync(long sessionId, int take, CancellationToken ct = default);
    Task SaveMessagesWithTransactionAsync(ChatbotSession session, IReadOnlyList<ChatbotMessage> newMessages, Action<ChatbotSession> applySessionUpdates, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task AddChatbotActionLogWithTransactionAsync(ChatbotActionLog log, CancellationToken ct = default);
}
