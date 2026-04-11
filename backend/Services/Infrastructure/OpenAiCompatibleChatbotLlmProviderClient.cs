using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class OpenAiCompatibleChatbotLlmProviderClient : IChatbotLlmProviderClient
{
    private const string DefaultAzureApiVersion = "2024-10-21";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenAiCompatibleChatbotLlmProviderClient> _logger;

    public OpenAiCompatibleChatbotLlmProviderClient(
        IHttpClientFactory httpClientFactory,
        ILogger<OpenAiCompatibleChatbotLlmProviderClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool CanHandle(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return false;
        }

        return provider.Contains("OPENAI", StringComparison.OrdinalIgnoreCase)
            || provider.Contains("GEMINI", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ChatbotLlmProviderRawResponse> GenerateAsync(
        ChatbotLlmProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = request.Provider?.Trim() ?? string.Empty;
        var model = request.Model?.Trim() ?? string.Empty;
        var isAzure = provider.Contains("AZURE_OPENAI", StringComparison.OrdinalIgnoreCase);
        var endpoint = BuildEndpoint(request, isAzure);
        var payload = BuildPayload(request);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        if (isAzure)
        {
            httpRequest.Headers.Add("api-key", request.ApiKey);
        }
        else
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
        }

        var client = _httpClientFactory.CreateClient(nameof(OpenAiCompatibleChatbotLlmProviderClient));
        var sw = Stopwatch.StartNew();

        try
        {
            using var response = await client.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                return BuildFailedFromHttp(provider, model, sw.ElapsedMilliseconds, response.StatusCode, responseBody);
            }

            return ParseSuccess(provider, model, sw.ElapsedMilliseconds, responseBody);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            _logger.LogWarning(
                "LLM provider timeout. Provider={Provider}, Model={Model}, LatencyMs={LatencyMs}",
                provider,
                model,
                sw.ElapsedMilliseconds);
            return new ChatbotLlmProviderRawResponse
            {
                Succeeded = false,
                IsTransientFailure = true,
                FailureCode = "TIMEOUT",
                FailureMessage = "Provider timeout.",
                Provider = provider,
                Model = model,
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            _logger.LogWarning(
                ex,
                "LLM provider network failure. Provider={Provider}, Model={Model}, LatencyMs={LatencyMs}",
                provider,
                model,
                sw.ElapsedMilliseconds);
            return new ChatbotLlmProviderRawResponse
            {
                Succeeded = false,
                IsTransientFailure = true,
                FailureCode = "NETWORK_ERROR",
                FailureMessage = ex.Message,
                Provider = provider,
                Model = model,
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(
                ex,
                "Unexpected LLM provider failure. Provider={Provider}, Model={Model}, LatencyMs={LatencyMs}",
                provider,
                model,
                sw.ElapsedMilliseconds);
            return new ChatbotLlmProviderRawResponse
            {
                Succeeded = false,
                IsTransientFailure = false,
                FailureCode = "PROVIDER_UNEXPECTED_ERROR",
                FailureMessage = ex.Message,
                Provider = provider,
                Model = model,
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
    }

    private static string BuildEndpoint(ChatbotLlmProviderRequest request, bool isAzure)
    {
        var baseUrl = request.BaseUrl.Trim().TrimEnd('/');
        var isGemini = request.Provider.Contains("GEMINI", StringComparison.OrdinalIgnoreCase);

        if (isGemini)
        {
            return $"{baseUrl}/chat/completions";
        }

        if (!isAzure)
        {
            return $"{baseUrl}/v1/chat/completions";
        }

        var deployment = Uri.EscapeDataString(request.Model);
        var apiVersion = string.IsNullOrWhiteSpace(request.ApiVersion)
            ? DefaultAzureApiVersion
            : request.ApiVersion!.Trim();
        return $"{baseUrl}/openai/deployments/{deployment}/chat/completions?api-version={Uri.EscapeDataString(apiVersion)}";
    }

    private static string BuildPayload(ChatbotLlmProviderRequest request)
    {
        var body = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["messages"] = BuildMessagesPayload(request.Messages)
        };

        if (!request.Provider.Contains("AZURE_OPENAI", StringComparison.OrdinalIgnoreCase))
        {
            body["model"] = request.Model;
        }

        if (request.MaxTokens.HasValue && request.MaxTokens.Value > 0)
        {
            body["max_tokens"] = request.MaxTokens.Value;
        }

        if (request.Temperature.HasValue)
        {
            body["temperature"] = Math.Round(request.Temperature.Value, 2);
        }

        if (request.AllowToolCalls && request.ToolDefinitions.Count > 0)
        {
            body["tool_choice"] = "auto";
            body["tools"] = request.ToolDefinitions.Select(tool => new
            {
                type = "function",
                function = new
                {
                    name = tool.ToolName,
                    description = tool.Description,
                    parameters = ParseJsonOrEmpty(tool.JsonSchema)
                }
            });
        }

        return JsonSerializer.Serialize(body);
    }

    private static IReadOnlyList<object> BuildMessagesPayload(IReadOnlyList<ChatbotLlmMessage> messages)
    {
        var mapped = new List<object>();
        foreach (var message in messages)
        {
            var role = (message.Role ?? string.Empty).Trim().ToLowerInvariant();
            if (role == "user" && message.ImageUrls.Count > 0)
            {
                var contentParts = new List<object>();
                if (!string.IsNullOrWhiteSpace(message.Content))
                {
                    contentParts.Add(new { type = "text", text = message.Content });
                }

                foreach (var url in message.ImageUrls.Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    contentParts.Add(new
                    {
                        type = "image_url",
                        image_url = new { url }
                    });
                }

                mapped.Add(new
                {
                    role = "user",
                    content = contentParts
                });
                continue;
            }

            mapped.Add(new
            {
                role = string.IsNullOrWhiteSpace(role) ? "user" : role,
                content = message.Content ?? string.Empty
            });
        }

        return mapped;
    }

    private static JsonElement ParseJsonOrEmpty(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return document.RootElement.Clone();
        }
        catch
        {
            using var fallback = JsonDocument.Parse("{}");
            return fallback.RootElement.Clone();
        }
    }

    private static ChatbotLlmProviderRawResponse BuildFailedFromHttp(
        string provider,
        string model,
        long latencyMs,
        HttpStatusCode statusCode,
        string responseBody)
    {
        var statusCodeNumber = (int)statusCode;
        var transient = statusCodeNumber == 408 || statusCodeNumber == 429 || statusCodeNumber >= 500;

        return new ChatbotLlmProviderRawResponse
        {
            Succeeded = false,
            IsTransientFailure = transient,
            FailureCode = $"HTTP_{statusCodeNumber}",
            FailureMessage = Truncate(responseBody, 500),
            Provider = provider,
            Model = model,
            LatencyMs = latencyMs
        };
    }

    private static ChatbotLlmProviderRawResponse ParseSuccess(
        string provider,
        string requestedModel,
        long latencyMs,
        string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var model = root.TryGetProperty("model", out var modelNode) && modelNode.ValueKind == JsonValueKind.String
                ? modelNode.GetString() ?? requestedModel
                : requestedModel;

            var assistantReply = string.Empty;
            var toolCalls = new List<ChatbotLlmToolCall>();

            if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var messageNode))
                {
                    assistantReply = ExtractTextContent(messageNode);
                    if (messageNode.TryGetProperty("tool_calls", out var toolCallsNode) &&
                        toolCallsNode.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in toolCallsNode.EnumerateArray())
                        {
                            var toolCallId = item.TryGetProperty("id", out var idNode) && idNode.ValueKind == JsonValueKind.String
                                ? idNode.GetString()
                                : null;
                            if (!item.TryGetProperty("function", out var fnNode))
                            {
                                continue;
                            }

                            var toolName = fnNode.TryGetProperty("name", out var nameNode) && nameNode.ValueKind == JsonValueKind.String
                                ? nameNode.GetString() ?? string.Empty
                                : string.Empty;
                            var toolArgs = fnNode.TryGetProperty("arguments", out var argNode) && argNode.ValueKind == JsonValueKind.String
                                ? argNode.GetString() ?? "{}"
                                : "{}";

                            if (string.IsNullOrWhiteSpace(toolName))
                            {
                                continue;
                            }

                            toolCalls.Add(new ChatbotLlmToolCall
                            {
                                ToolName = toolName,
                                ToolArgs = string.IsNullOrWhiteSpace(toolArgs) ? "{}" : toolArgs,
                                ToolCallId = toolCallId
                            });
                        }
                    }
                }
            }

            var promptTokens = 0;
            var completionTokens = 0;
            var totalTokens = 0;
            if (root.TryGetProperty("usage", out var usageNode))
            {
                promptTokens = GetIntOrZero(usageNode, "prompt_tokens");
                completionTokens = GetIntOrZero(usageNode, "completion_tokens");
                totalTokens = GetIntOrZero(usageNode, "total_tokens");
            }

            return new ChatbotLlmProviderRawResponse
            {
                Succeeded = true,
                Provider = provider,
                Model = model,
                LatencyMs = latencyMs,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = totalTokens,
                AssistantReply = assistantReply,
                ToolCalls = toolCalls
            };
        }
        catch (Exception ex)
        {
            return new ChatbotLlmProviderRawResponse
            {
                Succeeded = false,
                IsTransientFailure = false,
                FailureCode = "PROVIDER_RESPONSE_PARSE_ERROR",
                FailureMessage = ex.Message,
                Provider = provider,
                Model = requestedModel,
                LatencyMs = latencyMs
            };
        }
    }

    private static string ExtractTextContent(JsonElement messageNode)
    {
        if (!messageNode.TryGetProperty("content", out var contentNode))
        {
            return string.Empty;
        }

        if (contentNode.ValueKind == JsonValueKind.String)
        {
            return contentNode.GetString() ?? string.Empty;
        }

        if (contentNode.ValueKind == JsonValueKind.Array)
        {
            var chunks = new List<string>();
            foreach (var item in contentNode.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object &&
                    item.TryGetProperty("text", out var textNode) &&
                    textNode.ValueKind == JsonValueKind.String)
                {
                    var text = textNode.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        chunks.Add(text);
                    }
                }
            }

            return string.Join("\n", chunks);
        }

        return string.Empty;
    }

    private static int GetIntOrZero(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var node) && node.TryGetInt32(out var value)
            ? value
            : 0;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
