using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public class ChatbotRetrievalService : IChatbotRetrievalService
{
    private readonly ILogger<ChatbotRetrievalService> _logger;
    private readonly ChatbotBranchRetriever _branchRetriever;
    private readonly ChatbotServicePricingRetriever _pricingRetriever;
    private readonly ChatbotPolicyRetriever _policyRetriever;

    public ChatbotRetrievalService(
        IGarageContext context,
        ILogger<ChatbotRetrievalService> logger)
    {
        _logger = logger;
        var catalogMatcher = new ChatbotServiceCatalogMatcher(context);
        _branchRetriever = new ChatbotBranchRetriever(context);
        _pricingRetriever = new ChatbotServicePricingRetriever(context, catalogMatcher);
        _policyRetriever = new ChatbotPolicyRetriever(context, catalogMatcher);
    }

    public async Task<ChatbotRetrievalResult> RetrieveAsync(
        long customerId,
        long? branchId,
        string question,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuestion = ChatbotRetrievalTextHelper.Normalize(question);
        var queryType = ChatbotRetrievalTextHelper.DetectQueryType(normalizedQuestion);

        var result = queryType switch
        {
            ChatbotKnowledgeQueryType.SERVICE_BASE_PRICE => await _pricingRetriever.ResolveServiceBasePriceAsync(normalizedQuestion, cancellationToken),
            ChatbotKnowledgeQueryType.WARRANTY_POLICY => await _policyRetriever.ResolveWarrantyPolicyAsync(normalizedQuestion, cancellationToken),
            ChatbotKnowledgeQueryType.BRANCH_ADDRESS_HOTLINE => await _branchRetriever.ResolveBranchAddressHotlineAsync(normalizedQuestion, branchId, cancellationToken),
            ChatbotKnowledgeQueryType.OPERATING_HOURS => await _branchRetriever.ResolveOperatingHoursAsync(normalizedQuestion, branchId, cancellationToken),
            ChatbotKnowledgeQueryType.MEMBERSHIP_BENEFITS => await _policyRetriever.ResolveMembershipBenefitsAsync(normalizedQuestion, cancellationToken),
            _ => new ChatbotRetrievalResult
            {
                QueryType = ChatbotKnowledgeQueryType.UNKNOWN,
                IsGrounded = false,
                MissingReason = "Query type not supported for deterministic retrieval."
            }
        };

        _logger.LogInformation(
            "Chatbot retrieval processed. CustomerId={CustomerId}, QueryType={QueryType}, Grounded={Grounded}, Ambiguous={Ambiguous}, SourceCount={SourceCount}",
            customerId,
            result.QueryType,
            result.IsGrounded,
            result.IsAmbiguous,
            result.Sources.Count);

        return result;
    }
}
