using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Interfaces.Services.Chat;

public sealed record ChatConversationBranchRef(long Id, long? BranchId);

public interface IChatHubAccessService
{
    Task<(bool Found, bool Allowed)> CanAccessConversationAsync(
        long userId,
        string role,
        string roleCode,
        long conversationId,
        CancellationToken cancellationToken = default);

    Task<bool> CanAccessBranchInboxAsync(
        long userId,
        string role,
        string roleCode,
        long branchId,
        CancellationToken cancellationToken = default);

    Task<bool> CanAccessWorkshopBranchAsync(
        long userId,
        string role,
        string roleCode,
        long branchId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatConversationBranchRef>> GetCustomerConversationRefsAsync(
        long customerId,
        CancellationToken cancellationToken = default);
}
