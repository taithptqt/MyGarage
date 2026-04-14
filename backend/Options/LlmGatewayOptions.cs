using System.Collections.Generic;

namespace MyGarage.Options;

public sealed class LlmGatewayOptions
{
    public const string SectionName = "LlmGateway";

    public bool Enabled { get; set; } = true;

    public int RequestTimeoutSeconds { get; set; } = 25;

    public int MaxRetries { get; set; } = 2;

    public int RetryBaseDelayMilliseconds { get; set; } = 400;

    public string? DefaultProvider { get; set; }

    public string? DefaultModel { get; set; }

    public int? DefaultMaxTokens { get; set; }

    public decimal? DefaultTemperature { get; set; }

    public string? GeminiAI { get; set; }

    public Dictionary<string, LlmGatewayProviderOptions> Providers { get; set; } = new();
}

public sealed class LlmGatewayProviderOptions
{
    public string BaseUrl { get; set; } = string.Empty;

    public string? ApiVersion { get; set; }

    public string? DefaultApiKey { get; set; }
}
