using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace MyGarage.Options;

public sealed class LlmGatewayOptionsValidator : IValidateOptions<LlmGatewayOptions>
{
    public ValidateOptionsResult Validate(string? name, LlmGatewayOptions options)
    {
        var errors = new List<string>();

        ValidateRange(errors, nameof(options.RequestTimeoutSeconds), options.RequestTimeoutSeconds, 5, 300);
        ValidateRange(errors, nameof(options.MaxRetries), options.MaxRetries, 0, 5);
        ValidateRange(errors, nameof(options.RetryBaseDelayMilliseconds), options.RetryBaseDelayMilliseconds, 50, 10_000);
        if (options.DefaultMaxTokens.HasValue)
        {
            ValidateRange(errors, nameof(options.DefaultMaxTokens), options.DefaultMaxTokens.Value, 1, 16_000);
        }

        if (options.DefaultTemperature.HasValue &&
            (options.DefaultTemperature.Value < 0m || options.DefaultTemperature.Value > 2m))
        {
            errors.Add($"{LlmGatewayOptions.SectionName}:{nameof(options.DefaultTemperature)} must be between 0 and 2.");
        }

        if (options.Providers is null || options.Providers.Count == 0)
        {
            errors.Add($"{LlmGatewayOptions.SectionName}:Providers must contain at least one provider endpoint.");
        }
        else
        {
            foreach (var (provider, cfg) in options.Providers)
            {
                if (string.IsNullOrWhiteSpace(provider))
                {
                    errors.Add($"{LlmGatewayOptions.SectionName}:Providers contains an empty provider key.");
                    continue;
                }

                if (cfg is null || string.IsNullOrWhiteSpace(cfg.BaseUrl))
                {
                    errors.Add($"{LlmGatewayOptions.SectionName}:Providers:{provider}:BaseUrl is required.");
                    continue;
                }

                if (!Uri.TryCreate(cfg.BaseUrl, UriKind.Absolute, out var parsed) ||
                    (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp))
                {
                    errors.Add($"{LlmGatewayOptions.SectionName}:Providers:{provider}:BaseUrl must be an absolute HTTP(S) URL.");
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(options.DefaultProvider) &&
            (options.Providers is null || !ContainsProviderKey(options.Providers, options.DefaultProvider)))
        {
            errors.Add($"{LlmGatewayOptions.SectionName}:{nameof(options.DefaultProvider)} must match a key in Providers.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }

    private static void ValidateRange(
        ICollection<string> errors,
        string key,
        int value,
        int min,
        int max)
    {
        if (value < min || value > max)
        {
            errors.Add($"{LlmGatewayOptions.SectionName}:{key} must be between {min} and {max}.");
        }
    }

    private static bool ContainsProviderKey(
        IReadOnlyDictionary<string, LlmGatewayProviderOptions> providers,
        string key)
    {
        foreach (var existing in providers.Keys)
        {
            if (string.Equals(existing, key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
