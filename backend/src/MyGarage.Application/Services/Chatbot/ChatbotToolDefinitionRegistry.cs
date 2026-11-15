using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.Interfaces;

namespace MyGarage.Services;

/// <summary>
/// Composite registry that aggregates <see cref="IChatbotToolDefinitionProvider"/> instances
/// and enforces allowlist validation and error normalization during tool execution.
/// </summary>
public sealed class ChatbotToolDefinitionRegistry : IChatbotToolDefinitionRegistry
{
    private readonly IReadOnlyList<IChatbotToolDefinitionProvider> _providers;
    private readonly IChatbotToolAllowlist _toolAllowlist;
    private readonly IAppLogger<ChatbotToolDefinitionRegistry> _logger;

    public ChatbotToolDefinitionRegistry(
        IEnumerable<IChatbotToolDefinitionProvider> providers,
        IChatbotToolAllowlist toolAllowlist,
        IAppLogger<ChatbotToolDefinitionRegistry> logger)
    {
        _providers = providers.ToList();
        _toolAllowlist = toolAllowlist;
        _logger = logger;
    }

    public IReadOnlyList<ChatbotToolDefinition> GetToolDefinitions()
    {
        return _providers
            .SelectMany(provider => provider.GetToolDefinitions())
            .Where(definition => _toolAllowlist.IsAllowedForLlm(definition.ToolName))
            .ToList();
    }

    public async Task<ChatbotToolExecutionResult> ExecuteAsync(
        ChatbotToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var toolName = request.ToolCall.ToolName?.Trim() ?? string.Empty;
        var normalizedToolName = toolName.ToLowerInvariant();
        var toolArgs = request.ToolCall.ToolArgs?.Trim();
        var normalizedArgs = string.IsNullOrWhiteSpace(toolArgs) ? "{}" : toolArgs;

        if (!_toolAllowlist.IsAllowed(toolName))
        {
            return BuildFailure(
                toolName,
                normalizedArgs,
                request.ToolCall.ToolCallId,
                "TOOL_NOT_ALLOWED",
                "Tool is not allowlisted for chatbot execution.");
        }

        JsonObject argsRoot;
        try
        {
            argsRoot = JsonNode.Parse(normalizedArgs) as JsonObject ?? new JsonObject();
        }
        catch
        {
            return BuildFailure(
                toolName,
                normalizedArgs,
                request.ToolCall.ToolCallId,
                "INVALID_TOOL_ARGS",
                "Tool arguments must be a valid JSON object.");
        }

        try
        {
            var provider = _providers.FirstOrDefault(p => p.CanExecute(normalizedToolName))
                ?? throw new ChatbotToolExecutionException(
                    "TOOL_NOT_IMPLEMENTED",
                    "Tool is allowlisted but no executor is registered.");

            var payload = await provider.ExecutePayloadAsync(
                request,
                normalizedToolName,
                argsRoot,
                cancellationToken);

            return new ChatbotToolExecutionResult
            {
                Succeeded = true,
                ToolName = toolName,
                ToolArgs = normalizedArgs,
                ToolResult = JsonSerializer.Serialize(payload),
                ToolCallId = request.ToolCall.ToolCallId
            };
        }
        catch (ChatbotToolExecutionException ex)
        {
            return BuildFailure(
                toolName,
                normalizedArgs,
                request.ToolCall.ToolCallId,
                ex.Code,
                ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Chatbot tool execution crashed. SessionId={SessionId}, CustomerId={CustomerId}, Tool={ToolName}",
                request.SessionId,
                request.CustomerId,
                toolName);
            return BuildFailure(
                toolName,
                normalizedArgs,
                request.ToolCall.ToolCallId,
                "TOOL_EXECUTION_ERROR",
                ex.Message);
        }
    }

    private ChatbotToolExecutionResult BuildFailure(
        string toolName,
        string toolArgs,
        string? toolCallId,
        string errorCode,
        string errorMessage)
    {
        _logger.LogWarning(
            "Chatbot tool execution rejected. ToolName={ToolName}, ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}",
            toolName,
            errorCode,
            errorMessage);

        return new ChatbotToolExecutionResult
        {
            Succeeded = false,
            ToolName = toolName,
            ToolArgs = toolArgs,
            ToolResult = JsonSerializer.Serialize(new
            {
                success = false,
                errorCode,
                errorMessage
            }),
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            ToolCallId = toolCallId
        };
    }
}
