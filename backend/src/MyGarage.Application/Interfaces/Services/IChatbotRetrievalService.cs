using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Enums;

namespace MyGarage.Interfaces;

public interface IChatbotRetrievalService
{
    Task<ChatbotRetrievalResult> RetrieveAsync(
        long customerId,
        long? branchId,
        string question,
        CancellationToken cancellationToken = default);
}

public class ChatbotRetrievalResult
{
    public ChatbotKnowledgeQueryType QueryType { get; set; } = ChatbotKnowledgeQueryType.UNKNOWN;

    public bool IsGrounded { get; set; }

    public bool IsAmbiguous { get; set; }

    public string? Answer { get; set; }

    public string? ClarificationPrompt { get; set; }

    public string? MissingReason { get; set; }

    public long? ResolvedBranchId { get; set; }

    public string? ResolvedBranchName { get; set; }

    public string? ResolvedBranchHotline { get; set; }

    public IReadOnlyList<ChatbotAuditSourceSnippet> Sources { get; set; } = Array.Empty<ChatbotAuditSourceSnippet>();
}

public class ChatbotAuditSourceSnippet
{
    public string Label { get; set; } = null!;

    public string Snippet { get; set; } = null!;
}
