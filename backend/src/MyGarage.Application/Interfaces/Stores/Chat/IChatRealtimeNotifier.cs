using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses.Chat;

namespace MyGarage.Application.Interfaces.Stores.Chat;

public interface IChatRealtimeNotifier
{
    bool IsCustomerOnline(long customerId);

    Task NotifyNewMessageAsync(
        long conversationId,
        long? branchId,
        ChatMessageDto message,
        CancellationToken ct = default);

    Task NotifyReadStateUpdatedAsync(
        long conversationId,
        long? branchId,
        string readerType,
        long readerId,
        long? lastReadMessageId,
        CancellationToken ct = default);

    Task NotifyConversationUpdatedAsync(
        long conversationId,
        long? branchId,
        CancellationToken ct = default);
}
