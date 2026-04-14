using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public class ChatbotVoucherResolverService : IChatbotVoucherResolverService
{
    private readonly IGarageContext _context;
    private readonly ISystemClock _clock;
    private readonly ILogger<ChatbotVoucherResolverService> _logger;

    public ChatbotVoucherResolverService(
        IGarageContext context,
        ISystemClock clock,
        ILogger<ChatbotVoucherResolverService> logger)
    {
        _context = context;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ChatbotVoucherResolveResult> ResolveBestVoucherAsync(
        ChatbotVoucherResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CustomerId <= 0)
        {
            return Fail("VCH_CUSTOMER_REQUIRED", "CustomerId is required.");
        }

        if (request.EligibleAmount <= 0m)
        {
            return Fail("VCH_ELIGIBLE_AMOUNT_INVALID", "Eligible amount must be greater than 0.");
        }

        var now = _clock.LocalNow;
        var branchId = request.BranchId;
        var customerTierId = await ResolveCustomerTierIdAsync(request, cancellationToken);
        var vehicleContext = await ResolveVehicleContextAsync(request.VehicleId, cancellationToken);
        var orderTypeTokens = ResolveOrderTypeTokens(request.OrderType);

        var candidateVouchers = await _context.Vouchers
            .AsNoTracking()
            .Where(voucher => voucher.IsDeleted != true)
            .Where(voucher => voucher.IsActive != false)
            .Where(voucher => voucher.Status == nameof(VoucherStatusCode.ACTIVE))
            .Where(voucher => voucher.StartDate <= now && voucher.EndDate >= now)
            // SEC-06: Pre-filter budget exhausted vouchers trong DB — không cần load lên memory để kiểm tra
            .Where(voucher => voucher.BudgetLimit == null || voucher.BudgetUsed < voucher.BudgetLimit)
            // SEC-06: Pre-filter voucher đã bị exclude cho customer này — không cần load lên memory
            .Where(voucher => !voucher.VoucherAssignments.Any(a =>
                a.AssignmentType == "CUSTOMER" &&
                !a.IsInclude &&
                a.TargetId == request.CustomerId &&
                (!a.EffectiveFrom.HasValue || a.EffectiveFrom.Value <= now) &&
                (!a.EffectiveTo.HasValue || a.EffectiveTo.Value >= now)))
            .Include(voucher => voucher.VoucherAssignments)
            .Include(voucher => voucher.VoucherUsageRules)
            .Include(voucher => voucher.VoucherConflictRules)
            .ToListAsync(cancellationToken);

        if (candidateVouchers.Count == 0)
        {
            return Fail("VCH_NOT_FOUND", "No active voucher found.");
        }

        var appliedCandidates = new List<EvaluatedVoucher>();
        foreach (var voucher in candidateVouchers)
        {
            var validation = ValidateVoucherState(voucher, now);
            if (!validation.IsSuccess)
            {
                continue;
            }

            validation = ValidateCustomerAssignment(voucher, request.CustomerId, now);
            if (!validation.IsSuccess)
            {
                continue;
            }

            validation = ValidateUsageLimits(voucher);
            if (!validation.IsSuccess)
            {
                continue;
            }

            validation = ValidateVoucherUsageRules(
                voucher,
                request,
                branchId,
                customerTierId,
                vehicleContext,
                orderTypeTokens,
                now);
            if (!validation.IsSuccess)
            {
                continue;
            }

            validation = ValidateVoucherConflictRules(voucher, request);
            if (!validation.IsSuccess)
            {
                continue;
            }

            var eligibleForVoucher = Math.Max(0m, request.EligibleAmount - request.TierDiscountAmount);
            var discount = CalculateDiscount(voucher, eligibleForVoucher);
            if (discount <= 0m)
            {
                continue;
            }

            if (voucher.BudgetLimit.HasValue)
            {
                var budgetLimit = voucher.BudgetLimit.Value;
                if (voucher.BudgetUsed >= budgetLimit)
                {
                    continue;
                }

                if (voucher.BudgetUsed + discount > budgetLimit)
                {
                    continue;
                }
            }

            appliedCandidates.Add(new EvaluatedVoucher
            {
                Voucher = voucher,
                DiscountAmount = discount
            });
        }

        if (appliedCandidates.Count == 0)
        {
            _logger.LogInformation(
                "No valid voucher found. CustomerId={CustomerId}, BranchId={BranchId}, OrderType={OrderType}, EligibleAmount={EligibleAmount}",
                request.CustomerId,
                request.BranchId,
                request.OrderType,
                request.EligibleAmount);

            return Fail("VCH_NO_VALID_VOUCHER", "No valid voucher can be applied.");
        }

        var bestVoucher = appliedCandidates
            .OrderByDescending(item => item.DiscountAmount)
            .ThenBy(item => item.Voucher.EndDate)
            .ThenBy(item => item.Voucher.Id)
            .First();

        _logger.LogInformation(
            "Voucher selected. CustomerId={CustomerId}, VoucherId={VoucherId}, VoucherCode={VoucherCode}, DiscountAmount={DiscountAmount}, EndDate={EndDate}",
            request.CustomerId,
            bestVoucher.Voucher.Id,
            bestVoucher.Voucher.Code,
            bestVoucher.DiscountAmount,
            bestVoucher.Voucher.EndDate);

        return new ChatbotVoucherResolveResult
        {
            IsApplied = true,
            Voucher = new ChatbotVoucherResolutionDto
            {
                VoucherId = bestVoucher.Voucher.Id,
                VoucherCode = bestVoucher.Voucher.Code,
                DiscountAmount = bestVoucher.DiscountAmount,
                ExpiresAt = bestVoucher.Voucher.EndDate
            }
        };
    }

    private async Task<long?> ResolveCustomerTierIdAsync(ChatbotVoucherResolveRequest request, CancellationToken cancellationToken)
    {
        if (request.CustomerTierId.HasValue && request.CustomerTierId.Value > 0)
        {
            return request.CustomerTierId.Value;
        }

        return await _context.Customers
            .AsNoTracking()
            .Where(customer => customer.Id == request.CustomerId && customer.IsDeleted != true)
            .Select(customer => customer.TierId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<VehicleContext?> ResolveVehicleContextAsync(long? vehicleId, CancellationToken cancellationToken)
    {
        if (!vehicleId.HasValue || vehicleId.Value <= 0)
        {
            return null;
        }

        return await _context.Vehicles
            .AsNoTracking()
            .Where(vehicle => vehicle.Id == vehicleId.Value && vehicle.IsDeleted != true)
            .Select(vehicle => new VehicleContext
            {
                VehicleId = vehicle.Id,
                VehicleModelId = vehicle.ModelId,
                VehicleBrandId = vehicle.Model.BrandId
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static ValidationResult ValidateVoucherState(Voucher voucher, DateTime now)
    {
        if (voucher.IsDeleted == true)
        {
            return ValidationResult.Fail("VCH_DELETED", "Voucher is deleted.");
        }

        if (voucher.IsActive == false)
        {
            return ValidationResult.Fail("VCH_INACTIVE", "Voucher is inactive.");
        }

        var status = (voucher.Status ?? string.Empty).Trim().ToUpperInvariant();
        if (!string.Equals(status, nameof(VoucherStatusCode.ACTIVE), StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Fail("VCH_STATUS_INVALID", $"Voucher status is {status}.");
        }

        if (now < voucher.StartDate)
        {
            return ValidationResult.Fail("VCH_NOT_STARTED", "Voucher has not started.");
        }

        if (now > voucher.EndDate)
        {
            return ValidationResult.Fail("VCH_EXPIRED", "Voucher has expired.");
        }

        if (!IsInValidTimeRange(voucher.ValidFrom, voucher.ValidTo, now.TimeOfDay))
        {
            return ValidationResult.Fail("VCH_TIME_RANGE_INVALID", "Voucher is outside active time range.");
        }

        if (!IsDayAllowed(voucher.ValidDaysOfWeek, now.DayOfWeek))
        {
            return ValidationResult.Fail("VCH_DAY_INVALID", "Voucher is not valid for current day.");
        }

        return ValidationResult.Success();
    }

    private static ValidationResult ValidateCustomerAssignment(Voucher voucher, long customerId, DateTime now)
    {
        var customerAssignments = voucher.VoucherAssignments
            .Where(assignment => string.Equals(assignment.AssignmentType, "CUSTOMER", StringComparison.OrdinalIgnoreCase))
            .Where(assignment =>
                (!assignment.EffectiveFrom.HasValue || assignment.EffectiveFrom.Value <= now) &&
                (!assignment.EffectiveTo.HasValue || assignment.EffectiveTo.Value >= now))
            .ToList();

        if (customerAssignments.Count == 0)
        {
            return ValidationResult.Success();
        }

        var includedCustomerIds = customerAssignments
            .Where(assignment => assignment.IsInclude)
            .Select(assignment => assignment.TargetId)
            .ToHashSet();
        var excludedCustomerIds = customerAssignments
            .Where(assignment => !assignment.IsInclude)
            .Select(assignment => assignment.TargetId)
            .ToHashSet();

        if (excludedCustomerIds.Contains(customerId))
        {
            return ValidationResult.Fail("VCH_CUSTOMER_EXCLUDED", "Voucher excludes current customer.");
        }

        if (includedCustomerIds.Count > 0 && !includedCustomerIds.Contains(customerId))
        {
            return ValidationResult.Fail("VCH_CUSTOMER_NOT_INCLUDED", "Voucher is not assigned to current customer.");
        }

        return ValidationResult.Success();
    }

    private static ValidationResult ValidateUsageLimits(Voucher voucher)
    {
        if (voucher.UsageLimit.HasValue && voucher.UsageLimit.Value > 0)
        {
            var used = voucher.UsedCount ?? 0;
            if (used >= voucher.UsageLimit.Value)
            {
                return ValidationResult.Fail("VCH_USAGE_LIMIT_REACHED", "Voucher usage limit reached.");
            }
        }

        if (voucher.BudgetLimit.HasValue && voucher.BudgetUsed >= voucher.BudgetLimit.Value)
        {
            return ValidationResult.Fail("VCH_BUDGET_EXCEEDED", "Voucher budget limit exceeded.");
        }

        return ValidationResult.Success();
    }

    private static ValidationResult ValidateVoucherUsageRules(
        Voucher voucher,
        ChatbotVoucherResolveRequest request,
        long? branchId,
        long? customerTierId,
        VehicleContext? vehicleContext,
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
                    return ValidationResult.Fail("VCH_RULE_UNSUPPORTED", $"Unsupported hard-block rule: {rule.RuleType}.");
                }

                continue;
            }

            if (rule.IsExclude)
            {
                if (evaluation.IsMatched)
                {
                    return ValidationResult.Fail($"VCH_RULE_EXCLUDE_{NormalizeToken(rule.RuleType)}", $"Voucher excluded by rule: {rule.RuleType}.");
                }

                continue;
            }

            if (evaluation.IsMatched)
            {
                continue;
            }

            if (rule.HardBlock)
            {
                return ValidationResult.Fail($"VCH_RULE_BLOCK_{NormalizeToken(rule.RuleType)}", $"Voucher blocked by rule: {rule.RuleType}.");
            }
        }

        return ValidationResult.Success();
    }

    private static ValidationResult ValidateVoucherConflictRules(Voucher voucher, ChatbotVoucherResolveRequest request)
    {
        if (request.TierDiscountAmount <= 0m)
        {
            return ValidationResult.Success();
        }

        if (voucher.IsStackable != true)
        {
            return ValidationResult.Fail("VCH_NOT_STACKABLE_WITH_TIER", "Voucher is not stackable with tier discount.");
        }

        var conflictRules = voucher.VoucherConflictRules
            .Where(rule => rule.IsActive)
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.Id)
            .ToList();

        foreach (var rule in conflictRules)
        {
            if (!IsTierDiscountConflict(rule))
            {
                continue;
            }

            var actionType = (rule.ActionType ?? string.Empty).Trim().ToUpperInvariant();
            if (actionType is "BLOCK" or "REQUIRE_APPROVAL")
            {
                return ValidationResult.Fail(
                    "VCH_CONFLICT_TIER_DISCOUNT",
                    string.IsNullOrWhiteSpace(rule.MessageText)
                        ? "Voucher conflicts with tier discount."
                        : rule.MessageText!);
            }
        }

        return ValidationResult.Success();
    }

    private static UsageRuleEvaluation EvaluateUsageRule(
        VoucherUsageRule rule,
        ChatbotVoucherResolveRequest request,
        long? branchId,
        long? customerTierId,
        VehicleContext? vehicleContext,
        HashSet<string> orderTypeTokens,
        DateTime now)
    {
        var ruleType = NormalizeToken(rule.RuleType);
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
        var op = NormalizeToken(ruleOperator);
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

        var op = NormalizeToken(ruleOperator);
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
        var op = NormalizeToken(ruleOperator);
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

    private static decimal CalculateDiscount(Voucher voucher, decimal eligibleAmount)
    {
        if (eligibleAmount <= 0m)
        {
            return 0m;
        }

        decimal discountAmount;
        var voucherType = NormalizeToken(voucher.Type);
        if (voucherType == "PERCENTAGE")
        {
            discountAmount = Math.Round(eligibleAmount * (voucher.Value / 100m), 2, MidpointRounding.AwayFromZero);
        }
        else
        {
            discountAmount = voucher.Value;
        }

        if (voucher.MaxDiscountAmount.HasValue)
        {
            discountAmount = Math.Min(discountAmount, voucher.MaxDiscountAmount.Value);
        }

        return Math.Max(0m, Math.Min(discountAmount, eligibleAmount));
    }

    private static HashSet<string> ResolveOrderTypeTokens(ChatbotVoucherOrderType orderType)
    {
        return orderType switch
        {
            ChatbotVoucherOrderType.BOOKING => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "BOOKING"
            },
            ChatbotVoucherOrderType.SERVICE_ORDER_DRAFT => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "SERVICE_ORDER",
                "SERVICE_ORDER_DRAFT"
            },
            ChatbotVoucherOrderType.RETAIL_ORDER_DRAFT => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "RETAIL_ORDER",
                "RETAIL_ORDER_DRAFT",
                "PURCHASE"
            },
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                orderType.ToString()
            }
        };
    }

    private static bool IsTierDiscountConflict(VoucherConflictRule rule)
    {
        var blob = string.Join(
            "|",
            rule.ConflictType ?? string.Empty,
            rule.ConflictTarget ?? string.Empty,
            rule.MessageCode ?? string.Empty,
            rule.MessageText ?? string.Empty)
            .ToUpperInvariant();

        return blob.Contains("TIER", StringComparison.Ordinal) ||
               blob.Contains("MEMBERSHIP", StringComparison.Ordinal);
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

    private static bool IsInValidTimeRange(TimeOnly? validFrom, TimeOnly? validTo, TimeSpan now)
    {
        if (!validFrom.HasValue && !validTo.HasValue)
        {
            return true;
        }

        var nowTime = TimeOnly.FromTimeSpan(now);

        if (validFrom.HasValue && validTo.HasValue)
        {
            return validFrom.Value <= validTo.Value
                ? nowTime >= validFrom.Value && nowTime <= validTo.Value
                : nowTime >= validFrom.Value || nowTime <= validTo.Value;
        }

        if (validFrom.HasValue)
        {
            return nowTime >= validFrom.Value;
        }

        return nowTime <= validTo!.Value;
    }

    private static bool IsDayAllowed(string? validDaysOfWeek, DayOfWeek currentDay)
    {
        if (string.IsNullOrWhiteSpace(validDaysOfWeek))
        {
            return true;
        }

        var tokens = validDaysOfWeek
            .Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.ToUpperInvariant())
            .ToList();
        if (tokens.Count == 0)
        {
            return true;
        }

        var dayNumber = ((int)currentDay).ToString(CultureInfo.InvariantCulture);
        var dayShort = currentDay.ToString().Substring(0, 3).ToUpperInvariant();
        var dayFull = currentDay.ToString().ToUpperInvariant();

        return tokens.Contains(dayNumber) || tokens.Contains(dayShort) || tokens.Contains(dayFull);
    }

    private static string NormalizeToken(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    private static ChatbotVoucherResolveResult Fail(string reasonCode, string message)
    {
        return new ChatbotVoucherResolveResult
        {
            IsApplied = false,
            ReasonCode = reasonCode,
            Message = message
        };
    }

    private sealed class VehicleContext
    {
        public long VehicleId { get; set; }

        public long VehicleModelId { get; set; }

        public long VehicleBrandId { get; set; }
    }

    private sealed class EvaluatedVoucher
    {
        public Voucher Voucher { get; set; } = null!;

        public decimal DiscountAmount { get; set; }
    }

    private sealed class ValidationResult
    {
        public bool IsSuccess { get; private set; }

        public string? ReasonCode { get; private set; }

        public string? Message { get; private set; }

        public static ValidationResult Success() => new() { IsSuccess = true };

        public static ValidationResult Fail(string reasonCode, string message) => new()
        {
            IsSuccess = false,
            ReasonCode = reasonCode,
            Message = message
        };
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
