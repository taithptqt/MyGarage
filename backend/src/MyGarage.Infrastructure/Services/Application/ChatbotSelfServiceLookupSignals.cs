using System.Globalization;
using System.Text;

namespace MyGarage.Services;

internal static class ChatbotSelfServiceLookupSignals
{
    internal static readonly string[] ServiceHistorySignals =
    [
        "service history", "history", "lich su bao duong", "lich su sua chua", "past service", "bao duong truoc day"
    ];

    internal static readonly string[] InvoicePaymentSignals =
    [
        "invoice", "hoa don", "payment", "thanh toan", "paid", "con no", "cong no", "bill"
    ];

    internal static readonly string[] ServiceOrderProgressSignals =
    [
        "progress", "service order", "status order", "trang thai", "tien do", "dang sua", "xu ly don"
    ];

    internal static readonly string[] LoyaltySignals =
    [
        "loyalty", "tier", "membership", "hang khach hang", "uu dai thanh vien", "khach hang than thiet", "diem tich luy", "tich diem"
    ];

    internal static readonly string[] VoucherSignals =
    [
        "voucher", "ma giam", "giam gia", "khuyen mai", "uu dai", "quyen loi", "coupon", "promotion", "promo"
    ];

    internal static readonly string[] WarrantySignals =
    [
        "warranty", "bao hanh", "con bao hanh", "han bao hanh"
    ];

    internal static bool ContainsAny(string input, IReadOnlyList<string> signals)
    {
        var normalizedInput = Normalize(input);
        if (string.IsNullOrWhiteSpace(normalizedInput) || signals.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < signals.Count; i++)
        {
            var signal = Normalize(signals[i]);
            if (!string.IsNullOrWhiteSpace(signal)
                && normalizedInput.Contains(signal, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    internal static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var lowered = value.Trim().ToLowerInvariant();
        lowered = lowered.Replace('\u0111', 'd').Replace('\u0110', 'd');

        var decomposed = lowered.Normalize(NormalizationForm.FormD);
        var buffer = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            buffer.Append(char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) ? ch : ' ');
        }

        return string.Join(' ', buffer.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
