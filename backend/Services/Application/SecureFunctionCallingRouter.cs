using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public partial class SecureFunctionCallingRouter : IFunctionCallingRouter
{
    private const string PricingInventoryLookupTool = "product_pricing_inventory_lookup";
    private static readonly string[] PricingSignals = ["price", "cost", "gia", "bao gia", "bao nhieu", "het bao nhieu"];
    private static readonly string[] StockSignals = ["stock", "inventory", "in stock", "ton kho", "con hang"];
    private static readonly string[] HintNoiseTokens =
    [
        "cho", "cua", "mua", "can", "toi", "minh", "em", "anh", "chi", "ban",
        "vui", "long", "nhe", "voi", "oi", "a", "ha", "la", "giup", "xe", "toi"
    ];

    private readonly IChatbotToolAllowlist _toolAllowlist;
    private readonly IChatbotPricingInventoryGuard _pricingInventoryGuard;
    private readonly ILogger<SecureFunctionCallingRouter> _logger;

    public SecureFunctionCallingRouter(
        IChatbotToolAllowlist toolAllowlist,
        IChatbotPricingInventoryGuard pricingInventoryGuard,
        ILogger<SecureFunctionCallingRouter> logger)
    {
        _toolAllowlist = toolAllowlist;
        _pricingInventoryGuard = pricingInventoryGuard;
        _logger = logger;
    }

    public async Task<FunctionRoutingResult> RouteAsync(
        long customerId,
        long? branchId,
        string intent,
        string intentStateJson,
        PromptBuildResult prompt,
        CancellationToken cancellationToken = default)
    {
        if (intent is not nameof(ChatbotIntentType.PURCHASE) and not nameof(ChatbotIntentType.INQUIRY))
        {
            return new FunctionRoutingResult();
        }

        if (!NeedsPricingOrStock(prompt.UserPrompt))
        {
            return new FunctionRoutingResult();
        }

        var hint = ExtractProductHint(prompt.UserPrompt);
        if (string.IsNullOrWhiteSpace(hint))
        {
            return new FunctionRoutingResult
            {
                AssistantReplyOverride = "Mình chỉ trả giá/tồn kho theo dữ liệu hệ thống. Vui lòng cung cấp SKU hoặc tên sản phẩm cụ thể."
            };
        }

        var lookup = await _pricingInventoryGuard.ResolveAsync(customerId, branchId, hint, cancellationToken);
        if (!_toolAllowlist.IsAllowed(PricingInventoryLookupTool))
        {
            _logger.LogWarning(
                "Function router blocked tool outside allowlist. Tool={ToolName}, CustomerId={CustomerId}",
                PricingInventoryLookupTool,
                customerId);
            return new FunctionRoutingResult
            {
                AssistantReplyOverride = "Tính năng tra cứu giá/tồn kho tạm thời không khả dụng."
            };
        }

        var toolArgs = JsonSerializer.Serialize(new
        {
            customerId,
            branchId,
            productHint = hint
        });

        return new FunctionRoutingResult
        {
            AssistantReplyOverride = BuildReply(lookup),
            FunctionCalls =
            [
                new FunctionCallDescriptor
                {
                    ToolName = PricingInventoryLookupTool,
                    ToolArgs = toolArgs
                }
            ]
        };
    }

    private static bool NeedsPricingOrStock(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }
        var normalizedInput = NormalizeForMatching(input);

        foreach (var signal in PricingSignals)
        {
            if (normalizedInput.Contains(signal, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var signal in StockSignals)
        {
            if (normalizedInput.Contains(signal, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ExtractProductHint(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var skuMatch = SkuPattern().Match(input);
        if (skuMatch.Success && !string.IsNullOrWhiteSpace(skuMatch.Groups["sku"].Value))
        {
            return skuMatch.Groups["sku"].Value.Trim();
        }

        var quotedMatch = QuotedPattern().Match(input);
        if (quotedMatch.Success && !string.IsNullOrWhiteSpace(quotedMatch.Groups["quoted"].Value))
        {
            return quotedMatch.Groups["quoted"].Value.Trim();
        }

        var hintedMatch = PricingHintPattern().Match(input);
        if (hintedMatch.Success && !string.IsNullOrWhiteSpace(hintedMatch.Groups["hint"].Value))
        {
            var hinted = NormalizeHintCandidate(hintedMatch.Groups["hint"].Value);
            if (!string.IsNullOrWhiteSpace(hinted))
            {
                return hinted;
            }
        }

        var fallback = NormalizeHintCandidate(input);
        return string.IsNullOrWhiteSpace(fallback) ? null : fallback;
    }

    private static string BuildReply(ChatbotPricingInventoryResult lookup)
    {
        if (!lookup.ProductFound)
        {
            return "Sản phẩm bạn yêu cầu hiện không có trong dữ liệu MyGarage. Giá và tồn kho: unavailable.";
        }

        var productLabel = !string.IsNullOrWhiteSpace(lookup.ProductSku)
            ? $"{lookup.ProductName} ({lookup.ProductSku})"
            : lookup.ProductName ?? lookup.ProductHint;
        var priceText = lookup.PriceAvailable && lookup.Price.HasValue
            ? $"{lookup.Price.Value:0.##}"
            : "unavailable";
        var stockText = lookup.StockAvailable && lookup.StockQuantity.HasValue
            ? $"{lookup.StockQuantity.Value:0.##}"
            : "unavailable";

        return $"Thông tin từ hệ thống cho {productLabel}: giá={priceText}, tồn kho={stockText}.";
    }

    private static string NormalizeForMatching(string input)
    {
        var text = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace('đ', 'd')
            .Replace('Đ', 'd');
    }

    private static string NormalizeHintCandidate(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return string.Empty;
        }

        var hint = candidate.Trim();
        hint = SeparatorRegex().Replace(hint, " ");
        hint = WhitespaceRegex().Replace(hint, " ");

        var tokens = hint
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(x =>
            {
                var normalized = NormalizeForMatching(x);
                return !HintNoiseTokens.Contains(normalized, StringComparer.OrdinalIgnoreCase);
            })
            .ToArray();

        if (tokens.Length == 0)
        {
            return string.Empty;
        }

        var compact = string.Join(' ', tokens).Trim();
        if (compact.Length > 120)
        {
            compact = compact[..120].Trim();
        }

        return compact;
    }

    [GeneratedRegex(@"(?:\bsku\b|ma)\s*[:=\-]?\s*(?<sku>[A-Za-z0-9._\-]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SkuPattern();

    [GeneratedRegex("\"(?<quoted>[^\"]{2,120})\"", RegexOptions.Compiled)]
    private static partial Regex QuotedPattern();

    [GeneratedRegex(@"(?:\bgia\b|\bgiá\b|\bbao gia\b|\bbáo giá\b|\bprice\b|\bcost\b|\bton kho\b|\btồn kho\b|\bstock\b)\s*(?:cho|cua|của)?\s*(?<hint>[A-Za-z0-9À-ỹà-ỹđĐ\-\._/\s]{2,120})", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PricingHintPattern();

    [GeneratedRegex(@"[,\.;:\?\!\(\)\[\]\{\}]", RegexOptions.Compiled)]
    private static partial Regex SeparatorRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}
