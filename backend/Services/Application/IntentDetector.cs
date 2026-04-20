using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public class IntentDetector : IIntentDetector
{
    private static readonly IReadOnlyDictionary<string, string[]> KeywordsByIntent = new Dictionary<string, string[]>
    {
        [nameof(ChatbotIntentType.CONSULTATION)] =
        [
            "tư vấn",
            "consult",
            "advice",
            "nên",
            "gợi ý",
            "recommend",
            "chẩn đoán sơ bộ",
            "tư vấn bảo dưỡng",
            "tư vấn sửa chữa"
        ],
        [nameof(ChatbotIntentType.INQUIRY)] =
        [
            "hỏi",
            "thông tin",
            "báo giá",
            "giá bao nhiêu",
            "voucher",
            "mã giảm",
            "giảm giá",
            "khuyến mãi",
            "ưu đãi",
            "quyền lợi",
            "điểm tích lũy",
            "tích điểm",
            "where",
            "what",
            "how",
            "khi nào",
            "ở đâu",
            "chi tiết"
        ],
        [nameof(ChatbotIntentType.PURCHASE)] =
        [
            "mua",
            "buy",
            "đặt mua",
            "phụ tùng",
            "sản phẩm",
            "thanh toán",
            "checkout"
        ],
        [nameof(ChatbotIntentType.SERVICE_ORDER)] =
        [
            "lệnh sửa",
            "service order",
            "invoice",
            "hóa đơn",
            "trạng thái sửa chữa",
            "đơn dịch vụ",
            "số đơn"
        ],
        [nameof(ChatbotIntentType.BOOKING)] =
        [
            "đặt lịch",
            "đặt hẹn",
            "lịch hẹn",
            "booking",
            "appointment",
            "hẹn",
            "slot",
            "khung giờ",
            "book",
            "đăng ký lịch",
            "đặt lịch giúp tôi",
            "giúp tôi đặt lịch",
            "book hộ",
            "book giúp",
            "xếp lịch",
            "lên lịch"
        ],
        [nameof(ChatbotIntentType.HANDOFF)] =
        [
            "nhân viên",
            "người thật",
            "reception",
            "hỗ trợ trực tiếp",
            "handoff",
            "agent"
        ]
    };

    private static readonly IReadOnlyDictionary<string, string[]> NormalizedKeywordsByIntent =
        KeywordsByIntent.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Select(Normalize).Distinct(StringComparer.Ordinal).ToArray(),
            StringComparer.Ordinal);
    private static readonly string[] ContinuationHints =
    [
        "ok",
        "oke",
        "vâng",
        "được",
        "đồng ý",
        "xác nhận",
        "yes",
        "rồi",
        "tiếp",
        "tiếp tục"
    ];

    private static readonly Regex NumberRegex = new(@"\d+", RegexOptions.Compiled);
    private const decimal LowConfidenceThreshold = 0.45m;

    public IntentDetectionResult Detect(string input, string? currentIntentStateJson)
    {
        var normalized = Normalize(input);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new IntentDetectionResult
            {
                Intent = nameof(ChatbotIntentType.UNKNOWN),
                Confidence = 0.0m,
                IsLowConfidence = true,
                Strategy = "DETERMINISTIC_EMPTY_INPUT",
                Explanation = "Input is empty after normalization."
            };
        }

        var scores = NormalizedKeywordsByIntent.ToDictionary(kv => kv.Key, _ => 0m, StringComparer.Ordinal);
        foreach (var (intentKey, keywords) in NormalizedKeywordsByIntent)
        {
            var hits = keywords.Count(keyword => normalized.Contains(keyword, StringComparison.Ordinal));
            if (hits > 0)
            {
                scores[intentKey] += Math.Min(0.85m, hits * 0.50m);
            }
        }

        var explicitHandoff = NormalizedKeywordsByIntent[nameof(ChatbotIntentType.HANDOFF)]
            .Any(keyword => normalized.Contains(keyword, StringComparison.Ordinal));
        if (explicitHandoff)
        {
            scores[nameof(ChatbotIntentType.HANDOFF)] = Math.Max(scores[nameof(ChatbotIntentType.HANDOFF)], 0.90m);
        }

        if (NumberRegex.IsMatch(normalized))
        {
            scores[nameof(ChatbotIntentType.SERVICE_ORDER)] += 0.10m;
            scores[nameof(ChatbotIntentType.BOOKING)] += 0.07m;
        }

        var priorIntent = ExtractIntentFromState(currentIntentStateJson);
        if (!IsConversationSwitchInput(normalized)
            && !string.IsNullOrWhiteSpace(priorIntent)
            && scores.ContainsKey(priorIntent))
        {
            scores[priorIntent] += IsLikelyContinuation(normalized) ? 0.45m : 0.15m;
        }

        var ordered = scores.OrderByDescending(kv => kv.Value).ToList();
        var top = ordered[0];
        var second = ordered.Count > 1
            ? ordered[1]
            : new KeyValuePair<string, decimal>(nameof(ChatbotIntentType.UNKNOWN), 0m);

        var confidence = Math.Clamp(top.Value - (second.Value * 0.15m), 0m, 0.99m);
        var lowConfidence = confidence < LowConfidenceThreshold;
        var intent = lowConfidence ? nameof(ChatbotIntentType.UNKNOWN) : top.Key;
        if (explicitHandoff)
        {
            intent = nameof(ChatbotIntentType.HANDOFF);
            lowConfidence = false;
            confidence = Math.Max(confidence, 0.90m);
        }

        return new IntentDetectionResult
        {
            Intent = intent,
            Confidence = lowConfidence ? confidence : Math.Max(confidence, LowConfidenceThreshold),
            IsLowConfidence = lowConfidence,
            Strategy = "DETERMINISTIC_KEYWORD_SCORE",
            Explanation = $"Top={top.Key}:{top.Value:0.00}, Second={second.Key}:{second.Value:0.00}"
        };
    }

    private static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var lowered = input.Trim().ToLowerInvariant();
        lowered = lowered.Replace('\u0111', 'd').Replace('\u0110', 'd');

        var decomposed = lowered.Normalize(NormalizationForm.FormD);
        var buffer = new char[decomposed.Length];
        var idx = 0;
        foreach (var ch in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            // Keep letters/digits/spaces only to reduce punctuation noise.
            if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
            {
                buffer[idx++] = ch;
            }
            else
            {
                buffer[idx++] = ' ';
            }
        }

        var cleaned = new string(buffer, 0, idx);
        return string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string? ExtractIntentFromState(string? stateJson)
    {
        if (string.IsNullOrWhiteSpace(stateJson))
        {
            return null;
        }

        try
        {
            var node = JsonNode.Parse(stateJson);
            return node?["flow"]?["intent"]?.GetValue<string>()?.Trim().ToUpperInvariant();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsLikelyContinuation(string normalizedInput)
    {
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return false;
        }

        var tokenCount = normalizedInput.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (tokenCount <= 2)
        {
            return true;
        }

        return ContinuationHints.Any(hint => normalizedInput.Contains(hint, StringComparison.Ordinal));
    }

    private static bool IsConversationSwitchInput(string normalizedInput)
    {
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return false;
        }

        return normalizedInput.StartsWith("hi", StringComparison.Ordinal)
            || normalizedInput.StartsWith("hello", StringComparison.Ordinal)
            || normalizedInput.StartsWith("xin chao", StringComparison.Ordinal)
            || normalizedInput.StartsWith("chao", StringComparison.Ordinal)
            || normalizedInput.Contains("tu van", StringComparison.Ordinal)
            || normalizedInput.Contains("consult", StringComparison.Ordinal)
            || normalizedInput.Contains("advice", StringComparison.Ordinal)
            || normalizedInput.Contains("hoi", StringComparison.Ordinal)
            || normalizedInput.Contains("thong tin", StringComparison.Ordinal)
            || normalizedInput.Contains("bao gia", StringComparison.Ordinal)
            || normalizedInput.Contains("nhan vien", StringComparison.Ordinal)
            || normalizedInput.Contains("nguoi that", StringComparison.Ordinal);
    }
}
