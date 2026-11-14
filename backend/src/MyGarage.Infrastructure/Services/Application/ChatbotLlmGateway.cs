using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Options;

namespace MyGarage.Services;

public sealed class ChatbotLlmGateway : IChatbotLlmGateway
{
    private const string ActionType = "LLM_GATEWAY";

    private readonly IGarageContext _context;
    private readonly IEnumerable<IChatbotLlmProviderClient> _providerClients;
    private readonly IChatbotLlmResponseNormalizer _responseNormalizer;
    private readonly IOptionsMonitor<LlmGatewayOptions> _optionsMonitor;
    private readonly IConfiguration _configuration;
    private readonly ISystemClock _clock;
    private readonly ILogger<ChatbotLlmGateway> _logger;

    public ChatbotLlmGateway(
        IGarageContext context,
        IEnumerable<IChatbotLlmProviderClient> providerClients,
        IChatbotLlmResponseNormalizer responseNormalizer,
        IOptionsMonitor<LlmGatewayOptions> optionsMonitor,
        IConfiguration configuration,
        ISystemClock clock,
        ILogger<ChatbotLlmGateway> logger)
    {
        _context = context;
        _providerClients = providerClients;
        _responseNormalizer = responseNormalizer;
        _optionsMonitor = optionsMonitor;
        _configuration = configuration;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ChatbotLlmGatewayResponse> GenerateAsync(
        ChatbotLlmGatewayRequest request,
        CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.CurrentValue;
        if (!options.Enabled)
        {
            return await BuildAndLogFailureAsync(
                request.SessionId,
                "LLM_DISABLED",
                "LLM gateway is disabled by configuration.",
                isTransient: false,
                provider: string.Empty,
                model: string.Empty,
                latencyMs: 0,
                cancellationToken);
        }

        if (request.Messages.Count == 0)
        {
            return await BuildAndLogFailureAsync(
                request.SessionId,
                "EMPTY_PROMPT",
                "No messages provided to LLM gateway.",
                isTransient: false,
                provider: string.Empty,
                model: string.Empty,
                latencyMs: 0,
                cancellationToken);
        }

        var modelConfig = await ResolveModelConfigAsync(options, request.PreferVisionModel, cancellationToken);
        if (modelConfig is null)
        {
            return await BuildAndLogFailureAsync(
                request.SessionId,
                "MODEL_CONFIG_NOT_FOUND",
                "No active AI_ModelConfigs row found for the requested capability.",
                isTransient: false,
                provider: string.Empty,
                model: string.Empty,
                latencyMs: 0,
                cancellationToken);
        }

        if (!TryResolveProviderOptions(options, modelConfig.Provider, out var providerOptions))
        {
            return await BuildAndLogFailureAsync(
                request.SessionId,
                "PROVIDER_ENDPOINT_NOT_CONFIGURED",
                $"No endpoint configuration found for provider '{modelConfig.Provider}'.",
                isTransient: false,
                provider: modelConfig.Provider,
                model: modelConfig.ModelName,
                latencyMs: 0,
                cancellationToken);
        }

        var apiKey = ResolveApiKey(modelConfig, providerOptions, options);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return await BuildAndLogFailureAsync(
                request.SessionId,
                "PROVIDER_API_KEY_MISSING",
                $"API key is missing for provider '{modelConfig.Provider}'.",
                isTransient: false,
                provider: modelConfig.Provider,
                model: modelConfig.ModelName,
                latencyMs: 0,
                cancellationToken);
        }

        var providerClient = _providerClients.FirstOrDefault(x => x.CanHandle(modelConfig.Provider));
        if (providerClient is null)
        {
            return await BuildAndLogFailureAsync(
                request.SessionId,
                "PROVIDER_CLIENT_NOT_FOUND",
                $"No provider client can handle '{modelConfig.Provider}'.",
                isTransient: false,
                provider: modelConfig.Provider,
                model: modelConfig.ModelName,
                latencyMs: 0,
                cancellationToken);
        }

        var maxRetries = Math.Max(0, options.MaxRetries);
        var baseDelayMs = Math.Max(50, options.RetryBaseDelayMilliseconds);
        var timeoutSeconds = Math.Max(5, options.RequestTimeoutSeconds);

        ChatbotLlmProviderRawResponse? lastRaw = null;
        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                var raw = await providerClient.GenerateAsync(new ChatbotLlmProviderRequest
                {
                    Provider = modelConfig.Provider,
                    BaseUrl = providerOptions.BaseUrl,
                    ApiVersion = providerOptions.ApiVersion,
                    ApiKey = apiKey,
                    Model = modelConfig.ModelName,
                    MaxTokens = modelConfig.MaxTokens,
                    Temperature = modelConfig.Temperature,
                    Messages = request.Messages,
                    ToolDefinitions = request.ToolDefinitions,
                    AllowToolCalls = request.AllowToolCalls
                }, linkedCts.Token);

                lastRaw = raw;
                if (raw.Succeeded || !raw.IsTransientFailure || attempt >= maxRetries)
                {
                    var normalized = _responseNormalizer.Normalize(raw);
                    await PersistTelemetryAsync(request, normalized, cancellationToken);
                    return normalized;
                }

                var delayMs = Math.Min(5000, baseDelayMs * (int)Math.Pow(2, attempt));
                _logger.LogWarning(
                    "Transient LLM failure. Provider={Provider}, Model={Model}, Attempt={Attempt}/{MaxAttempts}, RetryInMs={DelayMs}, FailureCode={FailureCode}",
                    modelConfig.Provider,
                    modelConfig.ModelName,
                    attempt + 1,
                    maxRetries + 1,
                    delayMs,
                    raw.FailureCode);
                await Task.Delay(delayMs, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                var timeoutResponse = new ChatbotLlmProviderRawResponse
                {
                    Succeeded = false,
                    IsTransientFailure = true,
                    FailureCode = "GATEWAY_TIMEOUT",
                    FailureMessage = "LLM gateway request timed out.",
                    Provider = modelConfig.Provider,
                    Model = modelConfig.ModelName
                };
                lastRaw = timeoutResponse;
                if (attempt >= maxRetries)
                {
                    var normalized = _responseNormalizer.Normalize(timeoutResponse);
                    await PersistTelemetryAsync(request, normalized, cancellationToken);
                    return normalized;
                }
            }
            catch (Exception ex)
            {
                var unexpected = new ChatbotLlmProviderRawResponse
                {
                    Succeeded = false,
                    IsTransientFailure = false,
                    FailureCode = "GATEWAY_UNEXPECTED_ERROR",
                    FailureMessage = ex.Message,
                    Provider = modelConfig.Provider,
                    Model = modelConfig.ModelName
                };
                _logger.LogError(
                    ex,
                    "Unexpected LLM gateway failure. Provider={Provider}, Model={Model}",
                    modelConfig.Provider,
                    modelConfig.ModelName);
                lastRaw = unexpected;
                var normalized = _responseNormalizer.Normalize(unexpected);
                await PersistTelemetryAsync(request, normalized, cancellationToken);
                return normalized;
            }
        }

        var fallbackRaw = lastRaw ?? new ChatbotLlmProviderRawResponse
        {
            Succeeded = false,
            IsTransientFailure = false,
            FailureCode = "GATEWAY_UNKNOWN_FAILURE",
            FailureMessage = "Gateway finished without a provider response.",
            Provider = modelConfig.Provider,
            Model = modelConfig.ModelName
        };
        var fallback = _responseNormalizer.Normalize(fallbackRaw);
        await PersistTelemetryAsync(request, fallback, cancellationToken);
        return fallback;
    }

    private async Task<AiModelConfig?> ResolveModelConfigAsync(
        LlmGatewayOptions options,
        bool preferVisionModel,
        CancellationToken cancellationToken)
    {
        var activeConfigs = await _context.AiModelConfigs
            .AsNoTracking()
            .Where(x => x.IsActive == true)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (activeConfigs.Count == 0)
        {
            return BuildDefaultModelConfig(options, preferVisionModel);
        }

        if (preferVisionModel)
        {
            var geminiVision = activeConfigs.FirstOrDefault(x =>
                x.Provider.Contains("GEMINI", StringComparison.OrdinalIgnoreCase) &&
                x.Provider.Contains("VISION", StringComparison.OrdinalIgnoreCase));
            if (geminiVision is not null)
            {
                return geminiVision;
            }

            var vision = activeConfigs.FirstOrDefault(x =>
                x.Provider.Contains("VISION", StringComparison.OrdinalIgnoreCase));
            if (vision is not null)
            {
                return vision;
            }
        }
        else
        {
            var geminiText = activeConfigs.FirstOrDefault(x =>
                x.Provider.Contains("GEMINI", StringComparison.OrdinalIgnoreCase) &&
                !x.Provider.Contains("VISION", StringComparison.OrdinalIgnoreCase));
            if (geminiText is not null)
            {
                return geminiText;
            }

            var text = activeConfigs.FirstOrDefault(x =>
                !x.Provider.Contains("VISION", StringComparison.OrdinalIgnoreCase));
            if (text is not null)
            {
                return text;
            }
        }

        return activeConfigs[0];
    }

    private string? ResolveApiKey(
        AiModelConfig modelConfig,
        LlmGatewayProviderOptions providerOptions,
        LlmGatewayOptions options)
    {
        var isGeminiProvider = modelConfig.Provider.Contains("GEMINI", StringComparison.OrdinalIgnoreCase);
        var geminiCandidates = new[]
        {
            options.GeminiAI,
            _configuration[$"{LlmGatewayOptions.SectionName}:geminiAI"],
            _configuration[$"{LlmGatewayOptions.SectionName}:GeminiAI"],
            _configuration["geminiAI"],
            _configuration["GeminiAI"]
        };
        var defaultCandidates = new[]
        {
            modelConfig.ApiKey,
            providerOptions.DefaultApiKey
        };

        var candidates = isGeminiProvider
            ? geminiCandidates.Concat(defaultCandidates)
            : defaultCandidates;

        return candidates.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();
    }

    private static AiModelConfig? BuildDefaultModelConfig(LlmGatewayOptions options, bool preferVisionModel)
    {
        if (string.IsNullOrWhiteSpace(options.DefaultProvider) || string.IsNullOrWhiteSpace(options.DefaultModel))
        {
            return null;
        }

        if (preferVisionModel &&
            !options.DefaultProvider.Contains("VISION", StringComparison.OrdinalIgnoreCase))
        {
            // If only one default model is configured, still allow it for vision requests.
        }

        return new AiModelConfig
        {
            Provider = options.DefaultProvider.Trim(),
            ModelName = options.DefaultModel.Trim(),
            MaxTokens = options.DefaultMaxTokens,
            Temperature = options.DefaultTemperature,
            IsActive = true
        };
    }

    private static bool TryResolveProviderOptions(
        LlmGatewayOptions options,
        string provider,
        out LlmGatewayProviderOptions providerOptions)
    {
        if (options.Providers.TryGetValue(provider, out providerOptions!))
        {
            return true;
        }

        var pair = options.Providers.FirstOrDefault(x =>
            x.Key.Equals(provider, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(pair.Key))
        {
            providerOptions = pair.Value;
            return true;
        }

        var normalizedProvider = provider.Trim();
        pair = options.Providers.FirstOrDefault(x =>
            normalizedProvider.StartsWith(x.Key, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(pair.Key))
        {
            providerOptions = pair.Value;
            return true;
        }

        providerOptions = null!;
        return false;
    }

    private async Task PersistTelemetryAsync(
        ChatbotLlmGatewayRequest request,
        ChatbotLlmGatewayResponse response,
        CancellationToken cancellationToken)
    {
        try
        {
            _context.ChatbotActionLogs.Add(new ChatbotActionLog
            {
                SessionId = request.SessionId,
                ActionType = ActionType,
                Status = response.Succeeded ? "SUCCESS" : "FAILED",
                ErrorCode = response.FailureCode,
                ErrorMessage = response.FailureMessage,
                ActionPayload = JsonSerializer.Serialize(new
                {
                    request.Intent,
                    request.PreferVisionModel,
                    request.AllowToolCalls,
                    response.Provider,
                    response.Model,
                    response.LatencyMs,
                    response.PromptTokens,
                    response.CompletionTokens,
                    response.TotalTokens,
                    toolCallCount = response.ToolCalls.Count,
                    isTransientFailure = response.IsTransientFailure
                }),
                CreatedAt = _clock.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to persist LLM telemetry log. SessionId={SessionId}, Provider={Provider}, Model={Model}",
                request.SessionId,
                response.Provider,
                response.Model);
        }
    }

    private async Task<ChatbotLlmGatewayResponse> BuildAndLogFailureAsync(
        long sessionId,
        string code,
        string message,
        bool isTransient,
        string provider,
        string model,
        long latencyMs,
        CancellationToken cancellationToken)
    {
        var failure = new ChatbotLlmGatewayResponse
        {
            Succeeded = false,
            IsTransientFailure = isTransient,
            FailureCode = code,
            FailureMessage = message,
            Provider = provider,
            Model = model,
            LatencyMs = latencyMs
        };

        await PersistTelemetryAsync(
            new ChatbotLlmGatewayRequest
            {
                SessionId = sessionId
            },
            failure,
            cancellationToken);
        return failure;
    }
}
