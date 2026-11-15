using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Chat;

public sealed class ChatBranchCandidate
{
    public long Id { get; init; }
    public string? Name { get; init; }
    public string? Address { get; init; }
    public string? Status { get; init; }
}

public sealed class ReceptionConversationRow
{
    public required Conversation Conversation { get; init; }
    public string? CustomerFullName { get; init; }
    public int UnreadCount { get; init; }
}

public interface IChatStore :
    IChatConversationStore,
    IChatMessageStore,
    IChatReadStateStore,
    IChatContextStore
{
}
