using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class ChatbotLoyaltyStatusLookupService : IChatbotLoyaltyStatusLookupService
{
    private readonly IGarageContext _context;
    private readonly ILogger<ChatbotLoyaltyStatusLookupService> _logger;

    public ChatbotLoyaltyStatusLookupService(
        IGarageContext context,
        ILogger<ChatbotLoyaltyStatusLookupService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ChatbotCustomerSelfServiceLookupResponseDto?> TryLookupAsync(
        long customerId,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var normalizedMessage = ChatbotSelfServiceLookupSignals.Normalize(userMessage);
        if (!ChatbotSelfServiceLookupSignals.ContainsAny(normalizedMessage, ChatbotSelfServiceLookupSignals.LoyaltySignals))
        {
            return null;
        }

        var customer = await _context.Customers
            .AsNoTracking()
            .Where(x => x.Id == customerId && x.IsDeleted != true)
            .Select(x => new
            {
                x.Id,
                x.TotalSpending,
                TierName = x.Tier != null ? x.Tier.TierName : null,
                TierDiscount = x.Tier != null ? x.Tier.DiscountPercent : (decimal?)null,
                TierBenefits = x.Tier != null ? x.Tier.Benefits : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null)
        {
            return new ChatbotCustomerSelfServiceLookupResponseDto
            {
                LookupType = "LOYALTY_STATUS",
                Summary = "Unable to load your loyalty profile right now.",
                Disclaimer = "Loyalty information is shown for the authenticated customer account only."
            };
        }

        var totalSpending = customer.TotalSpending ?? 0m;
        var nextTier = await _context.CustomerTiers
            .AsNoTracking()
            .Where(x =>
                x.IsDeleted != true &&
                x.MinSpending > totalSpending)
            .OrderBy(x => x.MinSpending)
            .Select(x => new
            {
                x.TierName,
                x.MinSpending
            })
            .FirstOrDefaultAsync(cancellationToken);

        decimal? amountToNextTier = null;
        if (nextTier is not null)
        {
            amountToNextTier = Math.Max(0m, nextTier.MinSpending - totalSpending);
        }

        var currentTierName = string.IsNullOrWhiteSpace(customer.TierName) ? "Standard" : customer.TierName;
        var currentTierDiscount = customer.TierDiscount ?? 0m;

        var highlights = new List<string>
        {
            $"Current tier: {currentTierName} ({currentTierDiscount:0.##}% discount).",
            nextTier is null
                ? "You are currently at the highest configured tier."
                : $"Spend {amountToNextTier:0.##} more to reach tier {nextTier.TierName}."
        };

        var summary = nextTier is null
            ? $"Your loyalty tier is {currentTierName} with {currentTierDiscount:0.##}% discount benefits."
            : $"Your loyalty tier is {currentTierName}. You need {amountToNextTier:0.##} more spending to reach {nextTier.TierName}.";

        _logger.LogInformation(
            "Chatbot loyalty lookup completed. CustomerId={CustomerId}, Tier={TierName}, NextTier={NextTierName}",
            customerId,
            currentTierName,
            nextTier?.TierName);

        return new ChatbotCustomerSelfServiceLookupResponseDto
        {
            LookupType = "LOYALTY_STATUS",
            Summary = summary,
            Highlights = highlights,
            Disclaimer = "Tier qualification depends on approved spending rules in your account.",
            LoyaltyStatus = new ChatbotLoyaltyStatusLookupDto
            {
                CurrentTierName = currentTierName,
                CurrentTierDiscountPercent = currentTierDiscount,
                CurrentTierBenefits = customer.TierBenefits,
                TotalSpending = totalSpending,
                NextTierName = nextTier?.TierName,
                AmountToNextTier = amountToNextTier
            }
        };
    }
}
