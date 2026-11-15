using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Chat;

public interface IChatReadStateStore
{
    Task<ConversationCustomerRead?> GetCustomerReadAsync(long conversationId, CancellationToken ct = default);

    void AddCustomerRead(ConversationCustomerRead read);

    Task<ConversationStaffRead?> GetStaffReadAsync(
        long conversationId,
        long staffId,
        CancellationToken ct = default);

    void AddStaffRead(ConversationStaffRead read);

    Task<int> GetUnreadForCustomerAsync(
        long conversationId,
        long customerId,
        CancellationToken ct = default);

    Task<int> GetUnreadForStaffAsync(
        long conversationId,
        long staffId,
        CancellationToken ct = default);
}
