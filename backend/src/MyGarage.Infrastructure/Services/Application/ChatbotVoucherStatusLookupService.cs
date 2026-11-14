using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class ChatbotVoucherStatusLookupService : IChatbotVoucherStatusLookupService
{
    private readonly IGarageContext _context;
    private readonly ISystemClock _clock;
    private readonly ILogger<ChatbotVoucherStatusLookupService> _logger;

    public ChatbotVoucherStatusLookupService(
        IGarageContext context,
        ISystemClock clock,
        ILogger<ChatbotVoucherStatusLookupService> logger)
    {
        _context = context;
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

        var customerProfile = await _context.Customers
            .AsNoTracking()
            .Where(x => x.Id == customerId && x.IsDeleted != true)
            .Select(x => new
            {
                TierName = x.Tier != null ? x.Tier.TierName : null,
                TierDiscount = x.Tier != null ? x.Tier.DiscountPercent : (decimal?)null,
                TierBenefits = x.Tier != null ? x.Tier.Benefits : null
            })
            .FirstOrDefaultAsync(cancellationToken);

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

        var directRows = await _context.CustomerVouchers
            .AsNoTracking()
            .Where(cv =>
                cv.CustomerId == customerId
                && cv.IsUsed != true
                && cv.Voucher.IsDeleted != true
                && cv.Voucher.IsActive == true
                && cv.Voucher.Status != null
                && cv.Voucher.Status.ToUpper() == nameof(VoucherStatusCode.ACTIVE)
                && cv.Voucher.EndDate >= now)
            .Select(cv => new VoucherRow
            {
                VoucherId = cv.VoucherId,
                VoucherCode = cv.Voucher.Code,
                VoucherName = cv.Voucher.VoucherName,
                VoucherType = cv.Voucher.Type,
                VoucherValue = cv.Voucher.Value,
                MinOrderValue = cv.Voucher.MinOrderValue,
                MaxDiscountAmount = cv.Voucher.MaxDiscountAmount,
                StartDate = cv.Voucher.StartDate,
                EndDate = cv.Voucher.EndDate,
                MaxUsesPerCustomer = cv.Voucher.MaxUsesPerCustomer,
                Source = "DIRECT_ISSUED"
            })
            .ToListAsync(cancellationToken);

        var excludedVoucherIds = await _context.VoucherAssignments
            .AsNoTracking()
            .Where(a =>
                a.AssignmentType != null
                && a.AssignmentType.ToUpper() == "CUSTOMER"
                && a.TargetId == customerId
                && !a.IsInclude
                && (!a.EffectiveFrom.HasValue || a.EffectiveFrom <= now)
                && (!a.EffectiveTo.HasValue || a.EffectiveTo >= now))
            .Select(a => a.VoucherId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var assignedRows = await _context.VoucherAssignments
            .AsNoTracking()
            .Where(a =>
                a.AssignmentType != null
                && a.AssignmentType.ToUpper() == "CUSTOMER"
                && a.TargetId == customerId
                && a.IsInclude
                && (!a.EffectiveFrom.HasValue || a.EffectiveFrom <= now)
                && (!a.EffectiveTo.HasValue || a.EffectiveTo >= now)
                && a.Voucher.IsDeleted != true
                && a.Voucher.IsActive == true
                && a.Voucher.Status != null
                && a.Voucher.Status.ToUpper() == nameof(VoucherStatusCode.ACTIVE)
                && a.Voucher.EndDate >= now
                && !excludedVoucherIds.Contains(a.VoucherId))
            .Select(a => new VoucherRow
            {
                VoucherId = a.VoucherId,
                VoucherCode = a.Voucher.Code,
                VoucherName = a.Voucher.VoucherName,
                VoucherType = a.Voucher.Type,
                VoucherValue = a.Voucher.Value,
                MinOrderValue = a.Voucher.MinOrderValue,
                MaxDiscountAmount = a.Voucher.MaxDiscountAmount,
                StartDate = a.Voucher.StartDate,
                EndDate = a.Voucher.EndDate,
                MaxUsesPerCustomer = a.Voucher.MaxUsesPerCustomer,
                Source = "CUSTOMER_ASSIGNED"
            })
            .ToListAsync(cancellationToken);

        var redemptionUsage = await _context.VoucherRedemptions
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.ReversedAt == null)
            .GroupBy(x => x.VoucherId)
            .Select(group => new
            {
                VoucherId = group.Key,
                UsedCount = group.Count()
            })
            .ToDictionaryAsync(x => x.VoucherId, x => x.UsedCount, cancellationToken);

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

    private sealed class VoucherRow
    {
        public long VoucherId { get; set; }

        public string VoucherCode { get; set; } = string.Empty;

        public string VoucherName { get; set; } = string.Empty;

        public string VoucherType { get; set; } = string.Empty;

        public decimal VoucherValue { get; set; }

        public decimal? MinOrderValue { get; set; }

        public decimal? MaxDiscountAmount { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public int MaxUsesPerCustomer { get; set; }

        public int? RemainingIssuedCount { get; set; }

        public string Source { get; set; } = string.Empty;
    }
}
