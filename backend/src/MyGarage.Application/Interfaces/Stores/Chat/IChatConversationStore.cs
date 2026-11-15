using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Chat;

public interface IChatConversationStore
{
    Task<IReadOnlyList<Conversation>> GetCustomerConversationsPageAsync(
        long customerId,
        string? normalizedStatus,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<Conversation?> GetCustomerConversationWithCustomerAsync(
        long customerId,
        long conversationId,
        CancellationToken ct = default);

    Task<bool> CustomerOwnsConversationAsync(
        long customerId,
        long conversationId,
        CancellationToken ct = default);

    Task<Conversation?> FindOpenCustomerConversationAsync(
        long customerId,
        long branchId,
        long? appointmentId,
        long? serviceOrderId,
        long? vehicleId,
        CancellationToken ct = default);

    Task<Conversation?> GetLatestCustomerConversationAsync(
        long customerId,
        bool excludeClosed,
        CancellationToken ct = default);

    Task<Conversation?> GetCustomerConversationForUpdateAsync(
        long customerId,
        long conversationId,
        CancellationToken ct = default);

    Task<Conversation?> GetConversationWithCustomerByIdAsync(
        long conversationId,
        bool track = true,
        CancellationToken ct = default);

    void AddConversation(Conversation conversation);

    Task<IReadOnlyList<ReceptionConversationRow>> GetReceptionConversationsPageAsync(
        long staffId,
        long targetBranchId,
        string? normalizedStatus,
        bool? isUnread,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
