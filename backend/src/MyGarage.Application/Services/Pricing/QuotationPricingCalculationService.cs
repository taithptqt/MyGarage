using System.Collections.Generic;
using MyGarage.Domain.Pricing;

namespace MyGarage.Application.Pricing;

public sealed class QuotationPricingCalculationService : IQuotationPricingCalculationService
{
    public QuotationTotalsOutput CalculateTotals(IEnumerable<DetailPricingInput> detailInputs, decimal quotationLevelDiscount = 0m)
    {
        return QuotationPricingEngine.CalculateTotals(detailInputs, quotationLevelDiscount);
    }

    public DetailPricingOutput CalculateDetailPricing(DetailPricingInput input)
    {
        return QuotationPricingEngine.CalculateDetailPricing(input);
    }
}
