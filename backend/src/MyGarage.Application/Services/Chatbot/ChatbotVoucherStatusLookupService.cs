using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class ChatbotVoucherStatusLookupService : IChatbotVoucherStatusLookupService
{
    private readonly IChatbotStore _store;
    private readonly ISystemClock _clock;
    private readonly IAppLogger<ChatbotVoucherStatusLookupService> _logger;

    public ChatbotVoucherStatusLookupService(
        IChatbotStore store,
        ISystemClock clock,
        IAppLogger<ChatbotVoucherStatusLookupService> logger)
    {
        _store = store;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ChatbotCustomerSelfServiceLookupResponseDto?> TryLookupAsync(
        long customerId,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var normalizedMessage = ChatbotSelfServiceLookupSignals.Normalize(userMessage);
        if (!ChatbotSelfServiceLookupSignals.ContainsAny(normalizedMessage, ChatbotSelfServiceLookupSignals.VoucherSignals))
        {
            return null;
        }

        var customerProfile = await _store.GetCustomerTierSnapshotAsync(customerId, cancellationToken);

        if (customerProfile is null)
        {
            return new ChatbotCustomerSelfServiceLookupResponseDto
            {
                LookupType = "VOUCHER_STATUS",
                Summary = "Unable to load voucher information right now.",
                Disclaimer = "Voucher information is scoped to the authenticated customer account."
            };
        }

        var now = _clock.UtcNow;

        var directRows = await _store.GetDirectCustomerVouchersAsync(customerId, now, cancellationToken);

        var excludedVoucherIds = await _store.GetExcludedCustomerVoucherIdsAsync(customerId, now, cancellationToken);

        var assignedRows = await _store.GetAssignedCustomerVouchersAsync(customerId, now, excludedVoucherIds, cancellationToken);

        var redemptionUsage = await _store.GetCustomerVoucherRedemptionCountsAsync(customerId, cancellationToken);

        var directGrouped = directRows
            .GroupBy(x => x.VoucherId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var first = group.First();
                    first.RemainingIssuedCount = group.Count();
                    return first;
                });

        foreach (var assigned in assignedRows)
        {
            if (!directGrouped.ContainsKey(assigned.VoucherId))
            {
                directGrouped[assigned.VoucherId] = assigned;
            }
        }

        var availableItems = directGrouped.Values
            .Select(row =>
            {
                var used = redemptionUsage.GetValueOrDefault(row.VoucherId, 0);
                var remainingPerCustomer = row.MaxUsesPerCustomer > 0
                    ? Math.Max(0, row.MaxUsesPerCustomer - used)
                    : (int?)null;
                var isCurrentlyActive = row.StartDate <= now && now <= row.EndDate;

                return new ChatbotVoucherStatusItemDto
                {
                    VoucherId = row.VoucherId,
                    VoucherCode = row.VoucherCode,
                    VoucherName = row.VoucherName,
                    VoucherType = row.VoucherType,
                    VoucherValue = row.VoucherValue,
                    MinOrderValue = row.MinOrderValue,
                    MaxDiscountAmount = row.MaxDiscountAmount,
                    StartDate = row.StartDate,
                    EndDate = row.EndDate,
                    RemainingUsesPerCustomer = remainingPerCustomer,
                    RemainingIssuedCount = row.RemainingIssuedCount,
                    Source = row.Source,
                    IsCurrentlyActive = isCurrentlyActive
                };
            })
            .Where(item => item.RemainingUsesPerCustomer is null || item.RemainingUsesPerCustomer.Value > 0)
            .Where(item => item.RemainingIssuedCount is null || item.RemainingIssuedCount.Value > 0)
            .OrderByDescending(item => item.IsCurrentlyActive)
            .ThenBy(item => item.EndDate)
            .ThenBy(item => item.VoucherCode)
            .Take(10)
            .ToList();

        var activeCount = availableItems.Count(x => x.IsCurrentlyActive);
        var upcomingCount = availableItems.Count - activeCount;

        var summary = availableItems.Count == 0
            ? "You currently have no available vouchers in your account."
            : activeCount > 0
                ? $"You have {activeCount} active voucher(s) available now." + (upcomingCount > 0 ? $" Plus {upcomingCount} upcoming voucher(s)." : string.Empty)
                : $"You currently have {upcomingCount} upcoming voucher(s), but none active right now.";

        var highlights = new List<string>();
        if (!string.IsNullOrWhiteSpace(customerProfile.TierName))
        {
            var tierDiscount = customerProfile.TierDiscount ?? 0m;
            highlights.Add($"Current loyalty tier: {customerProfile.TierName} ({tierDiscount:0.##}% discount).");
        }

        if (!string.IsNullOrWhiteSpace(customerProfile.TierBenefits))
        {
            highlights.Add($"Tier benefits: {customerProfile.TierBenefits}.");
        }

        if (availableItems.Count > 0)
        {
            var preview = availableItems.Take(3)
                .Select(item =>
                {
                    var minOrderText = item.MinOrderValue.HasValue ? $", min order {item.MinOrderValue.Value:0.##}" : string.Empty;
                    var statusText = item.IsCurrentlyActive
                        ? "active"
                        : $"starts {item.StartDate:yyyy-MM-dd}";
                    return $"{item.VoucherCode} ({statusText}, expires {item.EndDate:yyyy-MM-dd}{minOrderText})";
                });
            highlights.Add($"Top vouchers: {string.Join("; ", preview)}.");
        }

        _logger.LogInformation(
            "Chatbot voucher lookup completed. CustomerId={CustomerId}, VoucherCount={VoucherCount}, ActiveCount={ActiveCount}",
            customerId,
            availableItems.Count,
            activeCount);

        return new ChatbotCustomerSelfServiceLookupResponseDto
        {
            LookupType = "VOUCHER_STATUS",
            Summary = summary,
            Highlights = highlights,
            Disclaimer = "Voucher eligibility is preliminary. Final validation depends on booking/order context and branch policy at apply time.",
            VoucherStatus = new ChatbotVoucherStatusLookupDto
            {
                AvailableVouchers = availableItems
            }
        };
    }
}
