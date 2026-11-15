using System.Collections.Generic;
using MyGarage.Domain.Pricing;

namespace MyGarage.Application.Pricing;

public interface IQuotationPricingCalculationService
{
    QuotationTotalsOutput CalculateTotals(IEnumerable<DetailPricingInput> detailInputs, decimal quotationLevelDiscount = 0m);
    DetailPricingOutput CalculateDetailPricing(DetailPricingInput input);
}
