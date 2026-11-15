using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Interfaces;

/// <summary>
/// Provides a cohesive group of chatbot tool definitions and their execution handlers.
/// </summary>
public interface IChatbotToolDefinitionProvider
{
    IReadOnlyList<ChatbotToolDefinition> GetToolDefinitions();

    bool CanExecute(string normalizedToolName);

    Task<object> ExecutePayloadAsync(
        ChatbotToolExecutionRequest request,
        string normalizedToolName,
        JsonObject argsRoot,
        CancellationToken cancellationToken = default);
}
