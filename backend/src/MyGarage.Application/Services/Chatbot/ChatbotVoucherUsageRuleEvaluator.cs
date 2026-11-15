using System.Globalization;
using System.Text.Json;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

internal static class ChatbotVoucherUsageRuleEvaluator
{
    internal static VoucherValidationResult ValidateVoucherUsageRules(
        Voucher voucher,
        ChatbotVoucherResolveRequest request,
        long? branchId,
        long? customerTierId,
        VoucherVehicleContext? vehicleContext,
        HashSet<string> orderTypeTokens,
        DateTime now)
    {
        var activeRules = voucher.VoucherUsageRules
            .Where(rule => rule.IsActive)
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.Id)
            .ToList();

        foreach (var rule in activeRules)
        {
            var evaluation = EvaluateUsageRule(rule, request, branchId, customerTierId, vehicleContext, orderTypeTokens, now);
            if (!evaluation.IsSupported)
            {
                if (rule.HardBlock)
                {
                    return VoucherValidationResult.Fail("VCH_RULE_UNSUPPORTED", $"Unsupported hard-block rule: {rule.RuleType}.");
                }

                continue;
            }

            if (rule.IsExclude)
            {
                if (evaluation.IsMatched)
                {
                    return VoucherValidationResult.Fail(
                        $"VCH_RULE_EXCLUDE_{ChatbotVoucherEvaluationHelper.NormalizeToken(rule.RuleType)}",
                        $"Voucher excluded by rule: {rule.RuleType}.");
                }

                continue;
            }

            if (evaluation.IsMatched)
            {
                continue;
            }

            if (rule.HardBlock)
            {
                return VoucherValidationResult.Fail(
                    $"VCH_RULE_BLOCK_{ChatbotVoucherEvaluationHelper.NormalizeToken(rule.RuleType)}",
                    $"Voucher blocked by rule: {rule.RuleType}.");
            }
        }

        return VoucherValidationResult.Success();
    }

    private static UsageRuleEvaluation EvaluateUsageRule(
        VoucherUsageRule rule,
        ChatbotVoucherResolveRequest request,
        long? branchId,
        long? customerTierId,
        VoucherVehicleContext? vehicleContext,
        HashSet<string> orderTypeTokens,
        DateTime now)
    {
        var ruleType = ChatbotVoucherEvaluationHelper.NormalizeToken(rule.RuleType);
        var tokens = ParseRuleTokens(rule.RuleValue);

        return ruleType switch
        {
            "PRODUCT" => EvaluateIdRule(rule.Operator, request.ProductIds, tokens),
            "SERVICE" => EvaluateIdRule(rule.Operator, request.ServiceIds, tokens),
            "BRANCH" => branchId.HasValue
                ? EvaluateScalarRule(rule.Operator, branchId.Value.ToString(CultureInfo.InvariantCulture), tokens)
                : UsageRuleEvaluation.Supported(false),
            "CUSTOMER_TIER" => customerTierId.HasValue
                ? EvaluateScalarRule(rule.Operator, customerTierId.Value.ToString(CultureInfo.InvariantCulture), tokens)
                : UsageRuleEvaluation.Supported(false),
            "VEHICLE_MODEL" => vehicleContext?.VehicleModelId > 0
                ? EvaluateScalarRule(rule.Operator, vehicleContext.VehicleModelId.ToString(CultureInfo.InvariantCulture), tokens)
                : UsageRuleEvaluation.Supported(false),
            "VEHICLE_BRAND" => vehicleContext?.VehicleBrandId > 0
                ? EvaluateScalarRule(rule.Operator, vehicleContext.VehicleBrandId.ToString(CultureInfo.InvariantCulture), tokens)
                : UsageRuleEvaluation.Supported(false),
            "BOOKING_CHANNEL" => EvaluateAnyTokenRule(rule.Operator, orderTypeTokens, tokens),
            "MIN_INVOICE" => EvaluateNumericRule(rule.Operator, request.EligibleAmount, tokens),
            "DAY_OF_WEEK" => EvaluateDayOfWeekRule(rule.Operator, now.DayOfWeek, tokens),
            "TIME_RANGE" => EvaluateTimeRangeRule(rule.Operator, now.TimeOfDay, tokens),
            _ => UsageRuleEvaluation.Unsupported()
        };
    }

    private static UsageRuleEvaluation EvaluateIdRule(string? ruleOperator, IReadOnlyList<long> values, HashSet<string> tokens)
    {
        if (values.Count == 0)
        {
            return UsageRuleEvaluation.Supported(false);
        }

        var valueTokens = values
            .Where(value => value > 0)
            .Select(value => value.ToString(CultureInfo.InvariantCulture))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return EvaluateAnyTokenRule(ruleOperator, valueTokens, tokens);
    }

    private static UsageRuleEvaluation EvaluateScalarRule(string? ruleOperator, string value, HashSet<string> tokens)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return UsageRuleEvaluation.Supported(false);
        }

        return EvaluateAnyTokenRule(ruleOperator, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { value }, tokens);
    }

    private static UsageRuleEvaluation EvaluateAnyTokenRule(string? ruleOperator, HashSet<string> values, HashSet<string> tokens)
    {
        var op = ChatbotVoucherEvaluationHelper.NormalizeToken(ruleOperator);
        var anyMatch = values.Any(tokens.Contains);

        return op switch
        {
            "IN" or "EQ" => UsageRuleEvaluation.Supported(anyMatch),
            "NOT_IN" or "NEQ" => UsageRuleEvaluation.Supported(!anyMatch),
            _ => UsageRuleEvaluation.Unsupported()
        };
    }

    private static UsageRuleEvaluation EvaluateNumericRule(string? ruleOperator, decimal value, HashSet<string> tokens)
    {
        if (tokens.Count == 0)
        {
            return UsageRuleEvaluation.Supported(false);
        }

        var first = tokens.First();
        if (!decimal.TryParse(first, NumberStyles.Number, CultureInfo.InvariantCulture, out var threshold))
        {
            return UsageRuleEvaluation.Unsupported();
        }

        var op = ChatbotVoucherEvaluationHelper.NormalizeToken(ruleOperator);
        var matched = op switch
        {
            "GTE" => value >= threshold,
            "LTE" => value <= threshold,
            "EQ" => value == threshold,
            "NEQ" => value != threshold,
            "IN" => value == threshold,
            "NOT_IN" => value != threshold,
            _ => false
        };

        if (op is not ("GTE" or "LTE" or "EQ" or "NEQ" or "IN" or "NOT_IN"))
        {
            return UsageRuleEvaluation.Unsupported();
        }

        return UsageRuleEvaluation.Supported(matched);
    }

    private static UsageRuleEvaluation EvaluateDayOfWeekRule(string? ruleOperator, DayOfWeek dayOfWeek, HashSet<string> tokens)
    {
        if (tokens.Count == 0)
        {
            return UsageRuleEvaluation.Supported(false);
        }

        var dayToken = ((int)dayOfWeek).ToString(CultureInfo.InvariantCulture);
        var dayShort = dayOfWeek.ToString().Substring(0, 3).ToUpperInvariant();
        var dayFull = dayOfWeek.ToString().ToUpperInvariant();

        var matched = tokens.Contains(dayToken) || tokens.Contains(dayShort) || tokens.Contains(dayFull);
        return EvaluateBooleanByOperator(ruleOperator, matched);
    }

    private static UsageRuleEvaluation EvaluateTimeRangeRule(string? ruleOperator, TimeSpan currentTime, HashSet<string> tokens)
    {
        if (tokens.Count == 0)
        {
            return UsageRuleEvaluation.Supported(false);
        }

        TimeOnly? from = null;
        TimeOnly? to = null;
        if (tokens.Count >= 2)
        {
            var tokenList = tokens.ToList();
            from = ParseTimeOnly(tokenList[0]);
            to = ParseTimeOnly(tokenList[1]);
        }
        else
        {
            var single = tokens.First();
            var rangeTokens = single.Split(new[] { '-', ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (rangeTokens.Length >= 2)
            {
                from = ParseTimeOnly(rangeTokens[0]);
                to = ParseTimeOnly(rangeTokens[1]);
            }
        }

        if (!from.HasValue || !to.HasValue)
        {
            return UsageRuleEvaluation.Unsupported();
        }

        var now = TimeOnly.FromTimeSpan(currentTime);
        var matched = from.Value <= to.Value
            ? now >= from.Value && now <= to.Value
            : now >= from.Value || now <= to.Value;

        return EvaluateBooleanByOperator(ruleOperator, matched);
    }

    private static UsageRuleEvaluation EvaluateBooleanByOperator(string? ruleOperator, bool matched)
    {
        var op = ChatbotVoucherEvaluationHelper.NormalizeToken(ruleOperator);
        return op switch
        {
            "IN" or "EQ" or "BETWEEN" => UsageRuleEvaluation.Supported(matched),
            "NOT_IN" or "NEQ" => UsageRuleEvaluation.Supported(!matched),
            _ => UsageRuleEvaluation.Unsupported()
        };
    }

    private static TimeOnly? ParseTimeOnly(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return TimeOnly.TryParse(token, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    private static HashSet<string> ParseRuleTokens(string? raw)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return result;
        }

        var text = raw.Trim();
        if (text.StartsWith("[", StringComparison.Ordinal))
        {
            try
            {
                using var jsonDocument = JsonDocument.Parse(text);
                if (jsonDocument.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in jsonDocument.RootElement.EnumerateArray())
                    {
                        switch (item.ValueKind)
                        {
                            case JsonValueKind.String:
                            {
                                var token = item.GetString();
                                if (!string.IsNullOrWhiteSpace(token))
                                {
                                    result.Add(token.Trim().ToUpperInvariant());
                                }

                                break;
                            }
                            case JsonValueKind.Number:
                            {
                                if (item.TryGetInt64(out var n))
                                {
                                    result.Add(n.ToString(CultureInfo.InvariantCulture));
                                }

                                break;
                            }
                        }
                    }

                    return result;
                }
            }
            catch
            {
                // Fall back to text split.
            }
        }

        var tokens = text.Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            result.Add(token.ToUpperInvariant());
        }

        return result;
    }

    private readonly struct UsageRuleEvaluation
    {
        public bool IsSupported { get; }

        public bool IsMatched { get; }

        private UsageRuleEvaluation(bool isSupported, bool isMatched)
        {
            IsSupported = isSupported;
            IsMatched = isMatched;
        }

        public static UsageRuleEvaluation Supported(bool isMatched) => new(true, isMatched);

        public static UsageRuleEvaluation Unsupported() => new(false, false);
    }
}
