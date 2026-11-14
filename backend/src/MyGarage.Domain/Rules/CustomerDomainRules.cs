using System;
using System.Text.RegularExpressions;

namespace MyGarage.Domain.Rules;

public static class CustomerDomainRules
{
    public const string VietnamPhonePattern = @"^(0\d{9}|\+84\d{9})$";
    public const string EmailPattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";

    public const string TypeGuest = "GUEST";
    public const string TypeIndividual = "INDIVIDUAL";
    public const string TypeCorporate = "CORPORATE";

    public const string PreferredBranchTagPrefix = "[PreferredBranchId:";
    public const string PreferredBranchTagSuffix = "]";

    private static readonly Regex PhoneRegex = new(VietnamPhonePattern, RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(EmailPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PreferredBranchRegex = new(@"\[PreferredBranchId:(\d+)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        return phone.Trim();
    }

    public static bool IsValidVietnamPhone(string? phone)
    {
        var normalized = NormalizePhone(phone);
        if (string.IsNullOrEmpty(normalized)) return false;
        return PhoneRegex.IsMatch(normalized);
    }

    public static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        return email.Trim().ToLowerInvariant();
    }

    public static bool IsValidEmail(string? email)
    {
        var normalized = NormalizeEmail(email);
        if (string.IsNullOrEmpty(normalized)) return false;
        return EmailRegex.IsMatch(normalized);
    }

    public static bool IsValidCustomerType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return false;
        var norm = type.Trim().ToUpperInvariant();
        return norm is TypeGuest or TypeIndividual or TypeCorporate;
    }

    public static string FormatPreferredBranchTag(long branchId)
    {
        if (branchId <= 0)
            throw new ArgumentOutOfRangeException(nameof(branchId), "Mã chi nhánh phải lớn hơn 0.");

        return $"{PreferredBranchTagPrefix}{branchId}{PreferredBranchTagSuffix}";
    }

    public static long? ExtractPreferredBranchId(string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) return null;
        var match = PreferredBranchRegex.Match(note);
        if (match.Success && long.TryParse(match.Groups[1].Value, out var branchId) && branchId > 0)
        {
            return branchId;
        }

        return null;
    }

    public static string SetPreferredBranchTag(string? note, long branchId)
    {
        var tag = FormatPreferredBranchTag(branchId);
        if (string.IsNullOrWhiteSpace(note)) return tag;

        if (PreferredBranchRegex.IsMatch(note))
        {
            return PreferredBranchRegex.Replace(note, tag).Trim();
        }

        return $"{note.Trim()} {tag}".Trim();
    }

    public static string RemovePreferredBranchTag(string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) return string.Empty;
        return PreferredBranchRegex.Replace(note, string.Empty).Trim();
    }

    public static (bool IsDuplicate, string? Field, string? ErrorMessage) CheckDuplicateInvariants(
        string? candidatePhone,
        string? candidateEmail,
        string? existingPhone,
        string? existingEmail)
    {
        var normCandidatePhone = NormalizePhone(candidatePhone);
        var normExistingPhone = NormalizePhone(existingPhone);

        if (!string.IsNullOrEmpty(normCandidatePhone) &&
            !string.IsNullOrEmpty(normExistingPhone) &&
            string.Equals(normCandidatePhone, normExistingPhone, StringComparison.OrdinalIgnoreCase))
        {
            return (true, "Phone", $"Số điện thoại '{normCandidatePhone}' đã tồn tại trong hệ thống.");
        }

        var normCandidateEmail = NormalizeEmail(candidateEmail);
        var normExistingEmail = NormalizeEmail(existingEmail);

        if (!string.IsNullOrEmpty(normCandidateEmail) &&
            !string.IsNullOrEmpty(normExistingEmail) &&
            string.Equals(normCandidateEmail, normExistingEmail, StringComparison.OrdinalIgnoreCase))
        {
            return (true, "Email", $"Email '{normCandidateEmail}' đã tồn tại trong hệ thống.");
        }

        return (false, null, null);
    }
}
