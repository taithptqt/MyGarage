using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using MyGarage.Application.Interfaces.Stores.Chat;
using MyGarage.DTOs.Responses.Chat;
using MyGarage.Hubs;

namespace MyGarage.Services.Realtime;

public sealed class SignalRChatRealtimeNotifier : IChatRealtimeNotifier
{
    private readonly IHubContext<ChatHub> _hubContext;

    public SignalRChatRealtimeNotifier(IHubContext<ChatHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public bool IsCustomerOnline(long customerId) => ChatHub.IsCustomerOnline(customerId);

    public async Task NotifyNewMessageAsync(
        long conversationId,
        long? branchId,
        ChatMessageDto message,
        CancellationToken ct = default)
    {
        await _hubContext.Clients.Group($"Conversation_{conversationId}").SendAsync("ReceiveMessage", message, ct);
        if (branchId.HasValue)
        {
            await _hubContext.Clients.Group($"BranchInbox_{branchId.Value}")
                .SendAsync("ConversationUpdated", new { conversationId }, ct);
        }
    }

    public async Task NotifyReadStateUpdatedAsync(
        long conversationId,
        long? branchId,
        string readerType,
        long readerId,
        long? lastReadMessageId,
        CancellationToken ct = default)
    {
        await _hubContext.Clients.Group($"Conversation_{conversationId}")
            .SendAsync(
                "ReadStateUpdated",
                new
                {
                    conversationId,
                    readerType,
                    readerId,
                    lastReadMessageId
                },
                ct);

        if (branchId.HasValue)
        {
            await _hubContext.Clients.Group($"BranchInbox_{branchId.Value}")
                .SendAsync("ConversationUpdated", new { conversationId }, ct);
        }
    }

    public async Task NotifyConversationUpdatedAsync(
        long conversationId,
        long? branchId,
        CancellationToken ct = default)
    {
        await _hubContext.Clients.Group($"Conversation_{conversationId}")
            .SendAsync("ConversationUpdated", new { conversationId }, ct);

        if (branchId.HasValue)
        {
            await _hubContext.Clients.Group($"BranchInbox_{branchId.Value}")
                .SendAsync("ConversationUpdated", new { conversationId }, ct);
        }
    }
}
