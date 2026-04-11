using System;
using System.Collections.Generic;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Common;

public static class InsuranceBillingRules
{
    public const string PayerTypeCustomer = "CUSTOMER";
    public const string PayerTypeInsurance = "INSURANCE";
    public const string InsuranceRepairCategoryCode = "INSURANCE_REPAIR";

    private static readonly HashSet<string> CustomerChargeServiceCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BH-MT500",
        "BH-MT1000",
        "BH-KHAUHAO",
        "BH-HOSO",
        "BH-LUUBAI",
    };

    public static bool IsInsuranceSignalService(StandardService? service)
    {
        if (service == null)
        {
            return false;
        }

        var code = NormalizeServiceCode(service.Code);
        return service.CategoryId == 7
            || code.StartsWith("PKG-BH", StringComparison.OrdinalIgnoreCase)
            || code.StartsWith("BH-", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsInsuranceSignalPackage(Package? package)
    {
        if (package == null)
        {
            return false;
        }

        var code = NormalizeServiceCode(package.Code);
        var categoryCode = NormalizeToken(package.Category?.Code);
        return package.CategoryId == 7
            || categoryCode == InsuranceRepairCategoryCode
            || code.StartsWith("PKG-BH", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCustomerChargeService(StandardService? service)
    {
        if (service == null)
        {
            return false;
        }

        return CustomerChargeServiceCodes.Contains(NormalizeServiceCode(service.Code));
    }

    public static bool ShouldBillInsurance(StandardService? service, bool insuranceOrderContext)
    {
        if (!insuranceOrderContext || service == null)
        {
            return false;
        }

        return !IsCustomerChargeService(service);
    }

    public static string NormalizePayerType(string? payerType)
    {
        var normalized = NormalizeToken(payerType);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return PayerTypeCustomer;
        }

        if (string.Equals(normalized, PayerTypeCustomer, StringComparison.OrdinalIgnoreCase))
        {
            return PayerTypeCustomer;
        }

        if (string.Equals(normalized, PayerTypeInsurance, StringComparison.OrdinalIgnoreCase))
        {
            return PayerTypeInsurance;
        }

        throw new InvalidOperationException("PayerType không hợp lệ. Chỉ chấp nhận CUSTOMER hoặc INSURANCE.");
    }

    public static string NormalizeInsuranceStatus(string? insuranceStatus, string payerType)
    {
        var normalizedPayerType = NormalizePayerType(payerType);
        if (normalizedPayerType == PayerTypeCustomer)
        {
            return nameof(ServiceOrderDetailInsuranceStatusCode.NOT_APPLICABLE);
        }

        var normalized = NormalizeToken(insuranceStatus);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return nameof(ServiceOrderDetailInsuranceStatusCode.PENDING_SUBMISSION);
        }

        if (!Enum.TryParse<ServiceOrderDetailInsuranceStatusCode>(normalized, ignoreCase: true, out var parsed))
        {
            throw new InvalidOperationException("InsuranceStatus không hợp lệ.");
        }

        return parsed.ToString();
    }

    public static string NormalizeOrderCategory(string? orderCategory)
    {
        return NormalizeToken(orderCategory);
    }

    private static string NormalizeServiceCode(string? serviceCode)
    {
        return string.IsNullOrWhiteSpace(serviceCode)
            ? string.Empty
            : serviceCode.Trim().ToUpperInvariant();
    }

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim()
            .ToUpperInvariant()
            .Replace("-", "_")
            .Replace(" ", "_");
    }
}
