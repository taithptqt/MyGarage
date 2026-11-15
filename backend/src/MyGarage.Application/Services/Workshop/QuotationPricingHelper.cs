using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Common;
using MyGarage.Domain.Pricing;
using MyGarage.DTOs.Requests;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services;

internal static class QuotationPricingHelper
{
    internal static List<QuotationDiscount> BuildLineManualDiscountsFromCreateItems(
        Quotation quotation,
        IReadOnlyList<QuotationCreateItemDto> items,
        IReadOnlyList<QuotationDetail> details,
        long actorStaffId,
        DateTime now,
        string? note)
    {
        var result = new List<QuotationDiscount>();
        if (quotation == null || items == null || details == null || actorStaffId <= 0)
        {
            return result;
        }

        var pairCount = Math.Min(items.Count, details.Count);
        var normalizedReason = QuotationValidationHelper.NormalizeText(note, 500) ?? "Line discount from quotation create";
        for (var i = 0; i < pairCount; i++)
        {
            var item = items[i];
            var detail = details[i];
            if (item == null || detail == null)
            {
                continue;
            }

            var discount = item.DiscountValue < 0m ? 0m : item.DiscountValue;
            if (discount <= 0m)
            {
                continue;
            }

            result.Add(new QuotationDiscount
            {
                Quotation = quotation,
                QuotationDetail = detail,
                DiscountType = QuotationWorkflowConstants.DiscountTypeManual,
                DiscountValue = discount,
                ValueType = QuotationWorkflowConstants.DiscountValueTypeAmount,
                AppliedAmount = discount,
                Reason = normalizedReason,
                AppliedBy = actorStaffId,
                CreatedAt = now
            });
        }

        return result;
    }

    internal static (decimal InsuranceCoveredAmount, decimal CustomerCoveredAmount, string PayerType) CalculateSplitBilling(
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

    private static decimal ClampCurrency(decimal value, decimal min, decimal max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    internal static (string ItemType, long? ItemId, string ItemName, decimal UnitPrice, decimal? CostPrice, decimal TaxRate) MapRecommendationToPrice(
        DiagnosisRecommendation recommendation,
        IReadOnlyDictionary<long, StandardService> serviceById,
        IReadOnlyDictionary<long, Product> productById)
    {
        if (recommendation.StandardServiceId.HasValue &&
            recommendation.StandardServiceId.Value > 0 &&
            serviceById.TryGetValue(recommendation.StandardServiceId.Value, out var standardService))
        {
            return (
                ItemType: "LABOR",
                ItemId: standardService.Id,
                ItemName: standardService.Name,
                UnitPrice: standardService.BasePrice,
                CostPrice: standardService.ConsumableCost,
                TaxRate: standardService.TaxRate ?? 0m
            );
        }

        if (recommendation.InventoryItemId.HasValue &&
            recommendation.InventoryItemId.Value > 0 &&
            productById.TryGetValue(recommendation.InventoryItemId.Value, out var product))
        {
            return (
                ItemType: "PART",
                ItemId: product.Id,
                ItemName: product.Name,
                UnitPrice: product.ServicePrice ?? product.RetailPrice ?? 0m,
                CostPrice: product.CostPrice,
                TaxRate: product.TaxRate ?? 0m
            );
        }

        var fallbackName = string.IsNullOrWhiteSpace(recommendation.TechnicianNotes)
            ? "UNNAMED_ITEM"
            : recommendation.TechnicianNotes!.Trim();
        return (
            ItemType: "LABOR",
            ItemId: recommendation.StandardServiceId ?? recommendation.InventoryItemId,
            ItemName: fallbackName,
            UnitPrice: 0m,
            CostPrice: null,
            TaxRate: 0m
        );
    }

    internal static (decimal UnitPrice, decimal? CostPrice, decimal TaxRate) RepriceQuotationDetail(
        QuotationDetail detail,
        IReadOnlyDictionary<long, StandardService> serviceById,
        IReadOnlyDictionary<long, Product> productById)
    {
        var normalizedItemType = QuotationMappingHelper.NormalizeServiceOrderDetailItemType(detail.ItemType);
        if (detail.ItemId.HasValue &&
            detail.ItemId.Value > 0 &&
            string.Equals(normalizedItemType, "LABOR", StringComparison.OrdinalIgnoreCase) &&
            serviceById.TryGetValue(detail.ItemId.Value, out var standardService))
        {
            return (standardService.BasePrice, standardService.ConsumableCost, standardService.TaxRate ?? detail.TaxRate);
        }

        if (detail.ItemId.HasValue &&
            detail.ItemId.Value > 0 &&
            string.Equals(normalizedItemType, "PART", StringComparison.OrdinalIgnoreCase) &&
            productById.TryGetValue(detail.ItemId.Value, out var product))
        {
            return (product.ServicePrice ?? product.RetailPrice ?? detail.UnitPriceAtTime, product.CostPrice, product.TaxRate ?? detail.TaxRate);
        }

        return (detail.UnitPriceAtTime, detail.CostPriceAtTime, detail.TaxRate);
    }

    internal static decimal ResolveQuotationTotalDiscountAmount(Quotation quotation)
    {
        if (quotation == null)
        {
            return 0m;
        }

        return SumQuotationLevelDiscount(quotation.QuotationDiscounts);
    }

    internal static decimal? ResolveQuotationDetailDiscountPercentage(
        QuotationDetail detail,
        IEnumerable<QuotationDiscount>? quotationDiscounts = null)
    {
        if (detail == null)
        {
            return null;
        }

        var subTotal = detail.SubTotal > 0m
            ? detail.SubTotal
            : Math.Round((detail.Quantity <= 0m ? 1m : detail.Quantity) * (detail.UnitPriceAtTime < 0m ? 0m : detail.UnitPriceAtTime), 2, MidpointRounding.AwayFromZero);
        if (subTotal <= 0m)
        {
            return null;
        }

        var discountAmount = ResolveQuotationDetailDiscountAmount(detail, quotationDiscounts);
        if (discountAmount <= 0m)
        {
            return null;
        }

        return Math.Round((discountAmount / subTotal) * 100m, 4, MidpointRounding.AwayFromZero);
    }

    internal static decimal ResolveQuotationDetailDiscountAmount(
        QuotationDetail detail,
        IEnumerable<QuotationDiscount>? quotationDiscounts = null)
    {
        if (detail == null)
        {
            return 0m;
        }

        var matchedDiscounts = (quotationDiscounts ?? Enumerable.Empty<QuotationDiscount>())
            .Where(x => x != null && x.QuotationDetailId == detail.Id)
            .ToList();
        if (matchedDiscounts.Count > 0)
        {
            return matchedDiscounts.Sum(x => x.AppliedAmount > 0m ? x.AppliedAmount : Math.Max(0m, x.DiscountValue));
        }

        return 0m;
    }

    internal static void RecalculateQuotationTotals(Quotation quotation)
    {
        var details = (quotation.QuotationDetails ?? new List<QuotationDetail>())
            .Where(x => !string.Equals(x.Status, QuotationWorkflowConstants.DetailStatusCanceled, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var allDiscounts = quotation.QuotationDiscounts ?? new List<QuotationDiscount>();

        var inputs = details.Select(detail =>
        {
            var lineDiscounts = allDiscounts
                .Where(d => d.QuotationDetailId == detail.Id)
                .Sum(d => d.AppliedAmount > 0 ? d.AppliedAmount : d.DiscountValue);

            return new DetailPricingInput(
                detail.Id,
                detail.Quantity,
                detail.UnitPriceAtTime,
                detail.TaxRate,
                lineDiscounts,
                detail.InsuranceCoveredAmount,
                detail.CustomerCoveredAmount,
                detail.PayerType,
                detail.Status
            );
        }).ToList();

        var quotationLevelDiscount = SumQuotationLevelDiscount(allDiscounts.Where(d => d.QuotationDetailId == null));

        var totals = QuotationPricingEngine.CalculateTotals(inputs, quotationLevelDiscount);

        var outputsById = totals.Details.ToDictionary(d => d.Id);
        foreach (var detail in details)
        {
            if (outputsById.TryGetValue(detail.Id, out var output))
            {
                detail.Quantity = output.Quantity;
                detail.UnitPriceAtTime = output.UnitPriceAtTime;
                detail.TaxRate = output.TaxRate;
                detail.SubTotal = output.SubTotal;
                detail.TaxAmount = output.TaxAmount;
                detail.FinalAmount = output.FinalAmount;
                detail.PayerType = output.PayerType;
                detail.InsuranceCoveredAmount = output.InsuranceCoveredAmount;
                detail.CustomerCoveredAmount = output.CustomerCoveredAmount;
            }
        }

        quotation.SubTotalAmount = totals.SubTotalAmount;
        quotation.TaxAmount = totals.TaxAmount;
        quotation.FinalAmount = totals.FinalAmount;
        quotation.TotalInsuranceAmount = totals.TotalInsuranceAmount;
        quotation.TotalCustomerAmount = totals.TotalCustomerAmount;
    }

    private static decimal SumQuotationLevelDiscount(IEnumerable<QuotationDiscount>? discounts)
    {
        if (discounts == null)
        {
            return 0m;
        }

        return discounts
            .Where(x => x != null)
            .Sum(x =>
            {
                var appliedAmount = x.AppliedAmount < 0m ? 0m : x.AppliedAmount;
                if (appliedAmount > 0m)
                {
                    return appliedAmount;
                }

                return x.DiscountValue < 0m ? 0m : x.DiscountValue;
            });
    }

    internal static async Task RecalculateServiceOrderTotalsAsync(IQuotationWorkflowStore store, long serviceOrderId, long actorStaffId, DateTime now)
    {
        var serviceOrder = await store.GetServiceOrderForUpdateAsync(serviceOrderId);
        if (serviceOrder == null)
        {
            return;
        }

        var summary = await store.GetNonCanceledServiceOrderDetailTotalsAsync(
            serviceOrderId,
            nameof(ServiceOrderDetailStatusCode.CANCELED));

        serviceOrder.SubTotalAmount = summary?.SubTotal ?? 0m;
        serviceOrder.TaxAmount = summary?.Tax ?? 0m;
        serviceOrder.FinalAmount = summary?.Final ?? 0m;
        serviceOrder.UpdatedBy = actorStaffId;
        serviceOrder.UpdatedAt = now;
    }

    internal static async Task<List<QuotationDiscount>> NormalizeQuotationDiscountItemsAsync(
        IQuotationLineageQueryStore store,
        IReadOnlyCollection<QuotationDiscountUpsertItemDto>? items,
        long actorStaffId,
        long quotationId)
    {
        var normalizedRows = new List<QuotationDiscount>();
        if (items == null || items.Count == 0)
        {
            return normalizedRows;
        }

        foreach (var item in items)
        {
            if (item == null)
            {
                continue;
            }

            var discountType = NormalizeQuotationDiscountType(item.DiscountType);
            if (string.IsNullOrWhiteSpace(discountType))
            {
                continue;
            }

            var discountValue = item.DiscountValue.GetValueOrDefault();
            var appliedAmount = item.AppliedAmount.GetValueOrDefault();
            if (discountValue < 0m) discountValue = 0m;
            if (appliedAmount < 0m) appliedAmount = 0m;
            if (appliedAmount <= 0m && discountValue > 0m)
            {
                appliedAmount = discountValue;
            }
            if (discountValue <= 0m && appliedAmount <= 0m)
            {
                continue;
            }

            var resolvedVoucherId = item.VoucherId > 0 ? item.VoucherId : null;
            if (!resolvedVoucherId.HasValue && !string.IsNullOrWhiteSpace(item.VoucherCode))
            {
                var voucherCode = item.VoucherCode.Trim();
                resolvedVoucherId = await store.GetActiveVoucherIdByCodeAsync(voucherCode);
            }

            normalizedRows.Add(new QuotationDiscount
            {
                QuotationId = quotationId,
                DiscountType = discountType,
                CustomerTierId = item.CustomerTierId > 0 ? item.CustomerTierId : null,
                VoucherId = resolvedVoucherId,
                DiscountValue = discountValue,
                ValueType = string.IsNullOrWhiteSpace(item.ValueType)
                    ? QuotationWorkflowConstants.DiscountValueTypeAmount
                    : item.ValueType.Trim().ToUpperInvariant(),
                AppliedAmount = appliedAmount,
                Reason = QuotationValidationHelper.NormalizeText(item.Reason, 500),
                AppliedBy = actorStaffId,
                CreatedAt = VietnamTime.Now
            });
        }

        return normalizedRows
            .GroupBy(x => x.DiscountType, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static string? NormalizeQuotationDiscountType(string? discountType)
    {
        var normalized = (discountType ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            QuotationWorkflowConstants.DiscountTypeMembership => QuotationWorkflowConstants.DiscountTypeMembership,
            QuotationWorkflowConstants.DiscountTypeManual => QuotationWorkflowConstants.DiscountTypeManual,
            QuotationWorkflowConstants.DiscountTypeVoucher => QuotationWorkflowConstants.DiscountTypeVoucher,
            _ => null
        };
    }
}
