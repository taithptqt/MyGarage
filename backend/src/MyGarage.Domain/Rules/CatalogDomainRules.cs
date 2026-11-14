using System;

namespace MyGarage.Domain.Rules;

public static class CatalogDomainRules
{
    public const string PackageCodePrefix = "PKG-";

    public static string? NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        return code.Trim().ToUpperInvariant();
    }

    public static (bool IsValid, string? ErrorMessage) ValidateStandardServiceCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return (false, "Mã dịch vụ tiêu chuẩn không được để trống.");
        }

        var normalized = code.Trim();
        if (normalized.StartsWith(PackageCodePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Mã bắt đầu bằng PKG- phải được quản lý ở bảng Packages, không tạo trong Standard Services.");
        }

        if (normalized.Length > 50)
        {
            return (false, "Mã dịch vụ tiêu chuẩn không được vượt quá 50 ký tự.");
        }

        return (true, null);
    }

    public static (bool IsValid, string? ErrorMessage) ValidateServicePricing(
        decimal basePrice,
        decimal taxRate,
        decimal consumableCost,
        int standardMinutes)
    {
        if (basePrice < 0m)
        {
            return (false, "Giá dịch vụ cơ bản không được âm.");
        }

        if (taxRate < 0m || taxRate > 100m)
        {
            return (false, "Thuế suất phải từ 0% đến 100%.");
        }

        if (consumableCost < 0m)
        {
            return (false, "Chi phí vật tư tiêu hao không được âm.");
        }

        if (standardMinutes < 0)
        {
            return (false, "Thời gian thực hiện tiêu chuẩn không được âm.");
        }

        return (true, null);
    }

    public static (bool IsValid, string? ErrorMessage) ValidateWarranty(int warrantyMonths, int warrantyKm)
    {
        if (warrantyMonths < 0)
        {
            return (false, "Số tháng bảo hành không được âm.");
        }

        if (warrantyKm < 0)
        {
            return (false, "Số km bảo hành không được âm.");
        }

        return (true, null);
    }

    public static decimal RoundPrice(decimal price, int decimals = 2)
    {
        return Math.Round(price, decimals, MidpointRounding.AwayFromZero);
    }

    public static (bool IsValid, string? ErrorMessage) ValidateCategoryHierarchy(long categoryId, long? parentCategoryId)
    {
        if (parentCategoryId.HasValue && categoryId > 0 && parentCategoryId.Value == categoryId)
        {
            return (false, "Danh mục không thể là danh mục cha của chính nó.");
        }

        return (true, null);
    }
}
