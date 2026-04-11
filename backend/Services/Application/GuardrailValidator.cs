using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public class GuardrailValidator : IGuardrailValidator
{
    private static readonly string[] PromptInjectionSignals =
    [
        "prompt injection",
        "ignore previous instructions",
        "ignore all previous instructions",
        "reveal system prompt",
        "show system prompt",
        "print system prompt",
        "leak system prompt",
        "disregard all previous",
        "bypass safety",
        "developer mode",
        "jailbreak",
        "ignore all above",
        "act as system",
        "you are now system",
        "you are chatgpt now",
        "sudo",
        "escalate privilege",
        "override policy"
    ];

    private static readonly string[] PromptControlSignals =
    [
        "ignore",
        "bypass",
        "override",
        "disregard",
        "disable",
        "break"
    ];

    private static readonly string[] PromptTargetSignals =
    [
        "instruction",
        "system prompt",
        "policy",
        "guardrail",
        "safety",
        "tool",
        "function call",
        "tool call"
    ];

    private static readonly string[] OffTopicSignals =
    [
        "bitcoin price",
        "politics",
        "football score",
        "write malware",
        "hack account",
        "adult content",
        "casino"
    ];

    private static readonly string[] InScopeSignals =
    [
        "car",
        "xe",
        "oto",
        "garage",
        "gara",
        "vehicle",
        "service",
        "dich vu",
        "booking",
        "dat lich",
        "appointment",
        "repair",
        "sua chua",
        "maintenance",
        "bao duong",
        "oil",
        "brake",
        "battery",
        "tire",
        "lop",
        "gia",
        "bao gia",
        "phu tung",
        "ton kho",
        "don dich vu",
        "voucher",
        "khuyen mai",
        "chi nhanh",
        "warranty",
        "bao hanh",
        "service order",
        "invoice",
        "hoa don",
        "payment",
        "thanh toan"
    ];

    private static readonly string[] HandoffSignals =
    [
        "nhan vien",
        "nguoi that",
        "tu van vien",
        "support agent",
        "human agent",
        "tong dai",
        "goi tong dai",
        "handoff"
    ];

    private static readonly string[] GreetingSignals =
    [
        "hi",
        "hello",
        "xin chao",
        "chao",
        "alo",
        "ok",
        "oke",
        "cam on",
        "thanks"
    ];

    private static readonly Regex ControlCharacterRegex = new(@"[\u0000-\u001F\u007F]", RegexOptions.Compiled);
    private static readonly Regex MultiSpaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex PromptDelimiterRegex = new(@"[`$<>]", RegexOptions.Compiled);
    private static readonly Regex PunctuationRegex = new(@"[^\p{L}\p{N}\s]", RegexOptions.Compiled);

    public GuardrailValidationResult Validate(string input)
    {
        var sanitized = Sanitize((input ?? string.Empty).Trim());
        var normalized = NormalizeForDetection(sanitized);
        var shouldHandoff = ContainsAny(normalized, HandoffSignals);

        if (IsPromptInjectionAttempt(normalized))
        {
            return new GuardrailValidationResult
            {
                IsAllowed = false,
                IsRejected = true,
                ShouldHandoff = false,
                ViolationCode = "PROMPT_INJECTION_ATTEMPT",
                RejectionMessage = "Yêu cầu không an toàn. Vui lòng mô tả vấn đề dịch vụ/sửa chữa xe để được hỗ trợ.",
                SanitizedInput = sanitized
            };
        }

        if (ContainsAny(normalized, OffTopicSignals) || LooksOffTopic(normalized))
        {
            return new GuardrailValidationResult
            {
                IsAllowed = false,
                IsRejected = true,
                ShouldHandoff = false,
                ViolationCode = "OFF_TOPIC",
                RejectionMessage = "Nội dung hiện tại không thuộc phạm vi hỗ trợ MyGarage. Vui lòng hỏi về đặt lịch, sửa chữa, báo giá hoặc đơn hàng dịch vụ.",
                SanitizedInput = sanitized
            };
        }

        return new GuardrailValidationResult
        {
            IsAllowed = true,
            IsRejected = false,
            ShouldHandoff = shouldHandoff,
            SanitizedInput = sanitized
        };
    }

    private static bool IsPromptInjectionAttempt(string normalizedInput)
    {
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return false;
        }

        if (ContainsAny(normalizedInput, PromptInjectionSignals))
        {
            return true;
        }

        var hasControlVerb = ContainsAny(normalizedInput, PromptControlSignals);
        var hasTarget = ContainsAny(normalizedInput, PromptTargetSignals);
        if (hasControlVerb && hasTarget)
        {
            return true;
        }

        var hasToolHijackPattern =
            normalizedInput.Contains("return json", StringComparison.Ordinal)
            || normalizedInput.Contains("call function", StringComparison.Ordinal)
            || normalizedInput.Contains("execute tool", StringComparison.Ordinal)
            || normalizedInput.Contains("run tool", StringComparison.Ordinal);

        return hasToolHijackPattern;
    }

    private static bool LooksOffTopic(string normalizedInput)
    {
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return false;
        }

        if (ContainsAny(normalizedInput, GreetingSignals))
        {
            return false;
        }

        var hasInScopeSignal = ContainsAny(normalizedInput, InScopeSignals);
        if (hasInScopeSignal)
        {
            return false;
        }

        var tokenCount = normalizedInput.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (tokenCount <= 4)
        {
            return false;
        }

        return normalizedInput.Length > 45;
    }

    private static string Sanitize(string input)
    {
        var sanitized = ControlCharacterRegex.Replace(input, " ");
        sanitized = PromptDelimiterRegex.Replace(sanitized, " ");
        sanitized = MultiSpaceRegex.Replace(sanitized, " ");
        return sanitized.Trim();
    }

    private static string NormalizeForDetection(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var lowered = input.Trim().ToLowerInvariant();
        lowered = lowered.Replace('\u0111', 'd').Replace('\u0110', 'd');

        var decomposed = lowered.Normalize(NormalizationForm.FormD);
        var buffer = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            buffer.Append(ch);
        }

        var noDiacritics = buffer.ToString().Normalize(NormalizationForm.FormC);
        var noPunctuation = PunctuationRegex.Replace(noDiacritics, " ");
        return string.Join(" ", noPunctuation.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool ContainsAny(string input, string[] signals)
    {
        for (var i = 0; i < signals.Length; i++)
        {
            if (input.Contains(signals[i], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
