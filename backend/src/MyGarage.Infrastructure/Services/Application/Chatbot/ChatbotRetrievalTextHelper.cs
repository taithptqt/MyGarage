using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MyGarage.Enums;

namespace MyGarage.Services;

internal static partial class ChatbotRetrievalTextHelper
{
    private static readonly string[] ServicePriceSignals =
    [
        "base price",
        "service price",
        "gia",
        "bang gia",
        "baogia",
        "bao gia",
        "bao nhieu",
        "het bao nhieu",
        "bao tien",
        "quote",
        "quotation",
        "price",
        "cost"
    ];

    private static readonly string[] AllScopeSignals =
    [
        "toan bo",
        "tat ca",
        "all",
        "full list",
        "danh sach tat ca",
        "tong hop"
    ];

    private static readonly string[] WarrantySignals = ["warranty", "bao hanh", "warranty policy"];
    private static readonly string[] BranchSignals = ["branch", "chi nhanh", "address", "dia chi", "hotline", "phone"];
    private static readonly string[] HoursSignals = ["hours", "open", "close", "operating", "gio lam viec", "mo cua"];
    private static readonly string[] MembershipSignals = ["membership", "member", "tier", "benefit", "uu dai", "khach hang than thiet"];

    private static readonly HashSet<string> ServiceHintStopTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "bao", "gia", "dich", "vu", "service", "price", "cost",
        "hanh", "thong", "tin", "ve",
        "sua", "chua", "chung", "he", "thong", "bao", "duong",
        "cho", "toi", "minh", "em", "anh", "chi", "ban",
        "co", "khong", "la", "nhu", "nao", "nhieu", "het", "tien",
        "xin", "hay", "vui", "long", "cau", "lam", "on", "giup",
        "may", "tao", "thang", "con", "nhe", "voi"
    };

    public static ChatbotKnowledgeQueryType DetectQueryType(string question)
    {
        var hasWarrantySignal = ContainsAny(question, WarrantySignals);
        if (hasWarrantySignal && !HasSpecificServiceHint(question))
        {
            return ChatbotKnowledgeQueryType.WARRANTY_POLICY;
        }

        if (ContainsAny(question, ServicePriceSignals))
        {
            return ChatbotKnowledgeQueryType.SERVICE_BASE_PRICE;
        }

        if (ContainsAny(question, WarrantySignals))
        {
            return ChatbotKnowledgeQueryType.WARRANTY_POLICY;
        }

        if (ContainsAny(question, HoursSignals))
        {
            return ChatbotKnowledgeQueryType.OPERATING_HOURS;
        }

        if (ContainsAny(question, BranchSignals))
        {
            return ChatbotKnowledgeQueryType.BRANCH_ADDRESS_HOTLINE;
        }

        if (ContainsAny(question, MembershipSignals))
        {
            return ChatbotKnowledgeQueryType.MEMBERSHIP_BENEFITS;
        }

        return ChatbotKnowledgeQueryType.UNKNOWN;
    }

    public static bool ContainsAny(string input, IEnumerable<string> keywords)
    {
        return keywords.Any(k => input.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsExplicitAllPricingRequest(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        var normalized = Normalize(question);
        return ContainsAny(normalized, ServicePriceSignals) && ContainsAny(normalized, AllScopeSignals);
    }

    public static bool HasSpecificServiceHint(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        var normalized = Normalize(question);
        if (ServiceCodeHintRegex().IsMatch(normalized) || ServiceLevelHintRegex().IsMatch(normalized))
        {
            return true;
        }

        var tokens = ExtractServiceHintTokens(normalized);
        if (tokens.Count >= 2)
        {
            return true;
        }

        return tokens.Count == 1 && tokens[0].Length >= 7;
    }

    public static bool HasServiceCodeHint(string normalizedQuestion)
    {
        return ServiceCodeHintRegex().IsMatch(normalizedQuestion);
    }

    public static string Normalize(string? input)
    {
        var text = (input ?? string.Empty).Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        if (text.Length == 0)
        {
            return string.Empty;
        }

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
            .Replace('\u0111', 'd')
            .Replace('\u0110', 'd');
    }

    public static List<string> Tokenize(string input)
    {
        return WordTokenRegex()
            .Matches(input)
            .Select(x => x.Value)
            .Where(x => x.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<string> ExtractServiceHintTokens(string input)
    {
        return Tokenize(input)
            .Where(x => !ServiceHintStopTokens.Contains(x))
            .ToList();
    }

    public static string? ExtractBranchHint(string question)
    {
        var match = BranchHintRegex().Match(question);
        if (!match.Success)
        {
            return null;
        }

        var value = Normalize(match.Groups["branch"].Value);
        value = value
            .Replace("hotline", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("phone", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("address", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("dia chi", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
        return value;
    }

    public static string ToDayLabel(int dayOfWeek)
    {
        return dayOfWeek switch
        {
            0 => "CN",
            1 => "T2",
            2 => "T3",
            3 => "T4",
            4 => "T5",
            5 => "T6",
            6 => "T7",
            _ => $"Day{dayOfWeek}"
        };
    }

    public static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }

    [GeneratedRegex(@"\b[a-zA-Z0-9\u00C0-\u024F]{2,}\b", RegexOptions.Compiled)]
    private static partial Regex WordTokenRegex();

    [GeneratedRegex(@"(?:chi nhanh|branch)\s+(?<branch>[a-zA-Z0-9\u00C0-\u024F\s\-]{2,40})", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex BranchHintRegex();

    [GeneratedRegex(@"\b[a-z]{1,5}\d{2,8}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ServiceCodeHintRegex();

    [GeneratedRegex(@"\bcap\s*\d+\b|\b\d+\s*(?:k|km)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ServiceLevelHintRegex();
}
