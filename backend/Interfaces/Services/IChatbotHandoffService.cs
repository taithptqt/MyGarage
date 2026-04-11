using System.Threading;
using System.Threading.Tasks;
using MyGarage.Enums;

namespace MyGarage.Interfaces;

public interface IChatbotHandoffService
{
    Task<ChatbotHandoffResult> InitiateAsync(
        long customerId,
        long? preferredBranchId,
        string userMessage,
        string intent,
        string intentStateJson,
        decimal confidence,
        CancellationToken cancellationToken = default);
}

public sealed class ChatbotHandoffResult
{
    public long ConversationId { get; set; }

    public string SummaryJson { get; set; } = "{}";

    public string AssistantReply { get; set; } = string.Empty;

    public ChatbotHandoffTriggerType TriggerType { get; set; }
}
