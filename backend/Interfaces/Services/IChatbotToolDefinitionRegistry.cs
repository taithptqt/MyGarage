using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Interfaces;

public interface IChatbotToolDefinitionRegistry
{
    IReadOnlyList<ChatbotToolDefinition> GetToolDefinitions();

    Task<ChatbotToolExecutionResult> ExecuteAsync(
        ChatbotToolExecutionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ChatbotToolExecutionRequest
{
    public long CustomerId { get; set; }

    public long SessionId { get; set; }

    public long? BranchId { get; set; }

    public ChatbotLlmToolCall ToolCall { get; set; } = new();
}

public sealed class ChatbotToolExecutionResult
{
    public bool Succeeded { get; set; }

    public string ToolName { get; set; } = string.Empty;

    public string ToolArgs { get; set; } = "{}";

    public string ToolResult { get; set; } = "{}";

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public string? ToolCallId { get; set; }
}
