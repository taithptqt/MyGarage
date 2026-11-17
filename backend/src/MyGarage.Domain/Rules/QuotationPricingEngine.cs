using System;
using System.Collections.Generic;
using System.Linq;

namespace MyGarage.Domain.Pricing;

public sealed record DetailPricingInput(
    long Id,
    decimal Quantity,
    decimal UnitPriceAtTime,
    decimal TaxRate,
    decimal LineDiscounts,
    decimal? InsuranceCoveredAmount,
    decimal? CustomerCoveredAmount,
    string? PayerType,
    string? Status
);

public sealed record DetailPricingOutput(
    long Id,
    decimal Quantity,
    decimal UnitPriceAtTime,
    decimal TaxRate,
    decimal SubTotal,
    decimal LineDiscount,
    decimal TaxableBase,
    decimal TaxAmount,
    decimal FinalAmount,
    string PayerType,
    decimal InsuranceCoveredAmount,
    decimal CustomerCoveredAmount
);

public sealed record QuotationTotalsOutput(
    decimal SubTotalAmount,
    decimal TaxAmount,
    decimal FinalAmount,
    decimal TotalInsuranceAmount,
    decimal TotalCustomerAmount,
    IReadOnlyList<DetailPricingOutput> Details
);

public static class QuotationPricingEngine
{
    public const string DetailStatusCanceled = "CANCELED";

    public static DetailPricingOutput CalculateDetailPricing(DetailPricingInput input)
    {
        var quantity = input.Quantity <= 0 ? 1m : input.Quantity;
        var unitPrice = input.UnitPriceAtTime < 0 ? 0m : input.UnitPriceAtTime;
        var taxRate = input.TaxRate < 0 ? 0m : input.TaxRate;

        var subTotal = Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
        var discount = Math.Min(Math.Max(0m, input.LineDiscounts), Math.Max(0m, subTotal));
        var taxableBase = Math.Max(0m, subTotal - discount);
        var taxAmount = Math.Round(taxableBase * (taxRate / 100m), 2, MidpointRounding.AwayFromZero);
        var finalAmount = Math.Round(Math.Max(0m, taxableBase + taxAmount), 2, MidpointRounding.AwayFromZero);

        var split = CalculateSplitBilling(
            finalAmount,
            input.InsuranceCoveredAmount,
            input.CustomerCoveredAmount,
            input.PayerType);

        return new DetailPricingOutput(
            input.Id,
            quantity,
            unitPrice,
            taxRate,
            subTotal,
            discount,
            taxableBase,
            taxAmount,
            finalAmount,
            split.PayerType,
            split.InsuranceCoveredAmount,
            split.CustomerCoveredAmount
        );
    }

    public static QuotationTotalsOutput CalculateTotals(
        IEnumerable<DetailPricingInput> detailInputs,
        decimal quotationLevelDiscount = 0m)
    {
        var activeDetails = detailInputs
            .Where(x => !string.Equals(x.Status, DetailStatusCanceled, StringComparison.OrdinalIgnoreCase))
            .Select(CalculateDetailPricing)
            .ToList();

        var safeQuotationLevelDiscount = Math.Max(0m, quotationLevelDiscount);

        var subTotal = activeDetails.Sum(x => x.SubTotal);
        var taxAmount = activeDetails.Sum(x => x.TaxAmount);
        var detailsFinal = activeDetails.Sum(x => x.FinalAmount);
        var detailsInsurance = activeDetails.Sum(x => x.InsuranceCoveredAmount);
        var detailsCustomer = activeDetails.Sum(x => x.CustomerCoveredAmount);

        if (safeQuotationLevelDiscount > detailsCustomer)
        {
            throw new InvalidOperationException("Giảm giá toàn báo giá không được vượt quá phần khách hàng thanh toán.");
        }

        var finalAmount = Math.Max(0m, detailsFinal - safeQuotationLevelDiscount);
        var totalCustomerAmount = Math.Max(0m, detailsCustomer - safeQuotationLevelDiscount);

        return new QuotationTotalsOutput(
            subTotal,
            taxAmount,
            finalAmount,
            detailsInsurance,
            totalCustomerAmount,
            activeDetails
        );
    }

    public static (decimal InsuranceCoveredAmount, decimal CustomerCoveredAmount, string PayerType) CalculateSplitBilling(
        decimal totalLineAmount,
        decimal? insuranceCoveredInput,
        decimal? customerCoveredInput,
        string? payerTypeInput)
    {
        var total = Math.Max(0m, Math.Round(totalLineAmount, 2, MidpointRounding.AwayFromZero));
        var normalizedPayer = (payerTypeInput ?? string.Empty).Trim().ToUpperInvariant();

        decimal insuranceCovered;
        decimal customerCovered;

        if (insuranceCoveredInput.HasValue)
        {
            insuranceCovered = ClampCurrency(insuranceCoveredInput.Value, 0m, total);
            customerCovered = Math.Round(total - insuranceCovered, 2, MidpointRounding.AwayFromZero);
        }
        else if (customerCoveredInput.HasValue)
        {
            customerCovered = ClampCurrency(customerCoveredInput.Value, 0m, total);
            insuranceCovered = Math.Round(total - customerCovered, 2, MidpointRounding.AwayFromZero);
        }
        else
        {
            if (normalizedPayer == "INSURANCE")
            {
                insuranceCovered = total;
                customerCovered = 0m;
            }
            else
            {
                insuranceCovered = 0m;
                customerCovered = total;
            }
        }

        var inferredPayer = insuranceCovered <= 0m
            ? "CUSTOMER"
            : customerCovered <= 0m
                ? "INSURANCE"
                : "SPLIT";

        return (insuranceCovered, customerCovered, inferredPayer);
    }

    public static decimal ClampCurrency(decimal value, decimal min, decimal max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
