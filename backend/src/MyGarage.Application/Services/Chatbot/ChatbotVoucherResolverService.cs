using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public class ChatbotVoucherResolverService : IChatbotVoucherResolverService
{
    private readonly IChatbotStore _store;
    private readonly ISystemClock _clock;
    private readonly IAppLogger<ChatbotVoucherResolverService> _logger;

    public ChatbotVoucherResolverService(
        IChatbotStore store,
        ISystemClock clock,
        IAppLogger<ChatbotVoucherResolverService> logger)
    {
        _store = store;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ChatbotVoucherResolveResult> ResolveBestVoucherAsync(
        ChatbotVoucherResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CustomerId <= 0)
        {
            return ChatbotVoucherEvaluationHelper.Fail("VCH_CUSTOMER_REQUIRED", "CustomerId is required.");
        }

        if (request.EligibleAmount <= 0m)
        {
            return ChatbotVoucherEvaluationHelper.Fail("VCH_ELIGIBLE_AMOUNT_INVALID", "Eligible amount must be greater than 0.");
        }

        var now = _clock.LocalNow;
        var branchId = request.BranchId;
        var customerTierId = await ResolveCustomerTierIdAsync(request, cancellationToken);
        var vehicleContext = await ResolveVehicleContextAsync(request.VehicleId, cancellationToken);
        var orderTypeTokens = ChatbotVoucherEvaluationHelper.ResolveOrderTypeTokens(request.OrderType);

        var candidateVouchers = await _store.GetCandidateVouchersForResolverAsync(request.CustomerId, now, cancellationToken);
        if (candidateVouchers.Count == 0)
        {
            return ChatbotVoucherEvaluationHelper.Fail("VCH_NOT_FOUND", "No active voucher found.");
        }

        var appliedCandidates = EvaluateCandidates(
            candidateVouchers,
            request,
            branchId,
            customerTierId,
            vehicleContext,
            orderTypeTokens,
            now);

        if (appliedCandidates.Count == 0)
        {
            _logger.LogInformation(
                "No valid voucher found. CustomerId={CustomerId}, BranchId={BranchId}, OrderType={OrderType}, EligibleAmount={EligibleAmount}",
                request.CustomerId,
                request.BranchId,
                request.OrderType,
                request.EligibleAmount);

            return ChatbotVoucherEvaluationHelper.Fail("VCH_NO_VALID_VOUCHER", "No valid voucher can be applied.");
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

    private static List<EvaluatedVoucher> EvaluateCandidates(
        IReadOnlyList<Voucher> candidateVouchers,
        ChatbotVoucherResolveRequest request,
        long? branchId,
        long? customerTierId,
        VoucherVehicleContext? vehicleContext,
        HashSet<string> orderTypeTokens,
        DateTime now)
    {
        var appliedCandidates = new List<EvaluatedVoucher>();
        var eligibleForVoucher = Math.Max(0m, request.EligibleAmount - request.TierDiscountAmount);

        foreach (var voucher in candidateVouchers)
        {
            if (!IsVoucherEligible(voucher, request, branchId, customerTierId, vehicleContext, orderTypeTokens, now))
            {
                continue;
            }

            var discount = ChatbotVoucherEvaluationHelper.CalculateDiscount(voucher, eligibleForVoucher);
            if (discount <= 0m || !ChatbotVoucherEvaluationHelper.HasRemainingBudget(voucher, discount))
            {
                continue;
            }

            appliedCandidates.Add(new EvaluatedVoucher
            {
                Voucher = voucher,
                DiscountAmount = discount
            });
        }

        return appliedCandidates;
    }

    private static bool IsVoucherEligible(
        Voucher voucher,
        ChatbotVoucherResolveRequest request,
        long? branchId,
        long? customerTierId,
        VoucherVehicleContext? vehicleContext,
        HashSet<string> orderTypeTokens,
        DateTime now)
    {
        if (!ChatbotVoucherEvaluationHelper.ValidateVoucherState(voucher, now).IsSuccess)
        {
            return false;
        }

        if (!ChatbotVoucherEvaluationHelper.ValidateCustomerAssignment(voucher, request.CustomerId, now).IsSuccess)
        {
            return false;
        }

        if (!ChatbotVoucherEvaluationHelper.ValidateUsageLimits(voucher).IsSuccess)
        {
            return false;
        }

        if (!ChatbotVoucherUsageRuleEvaluator.ValidateVoucherUsageRules(
                voucher,
                request,
                branchId,
                customerTierId,
                vehicleContext,
                orderTypeTokens,
                now).IsSuccess)
        {
            return false;
        }

        return ChatbotVoucherEvaluationHelper.ValidateVoucherConflictRules(voucher, request).IsSuccess;
    }

    private async Task<long?> ResolveCustomerTierIdAsync(ChatbotVoucherResolveRequest request, CancellationToken cancellationToken)
    {
        if (request.CustomerTierId.HasValue && request.CustomerTierId.Value > 0)
        {
            return request.CustomerTierId.Value;
        }

        return await _store.GetCustomerTierIdAsync(request.CustomerId, cancellationToken);
    }

    private async Task<VoucherVehicleContext?> ResolveVehicleContextAsync(long? vehicleId, CancellationToken cancellationToken)
    {
        if (!vehicleId.HasValue || vehicleId.Value <= 0)
        {
            return null;
        }

        var snapshot = await _store.GetVehicleContextAsync(vehicleId.Value, cancellationToken);
        return snapshot is null
            ? null
            : new VoucherVehicleContext
            {
                VehicleId = snapshot.VehicleId,
                VehicleModelId = snapshot.VehicleModelId,
                VehicleBrandId = snapshot.VehicleBrandId
            };
    }
}
