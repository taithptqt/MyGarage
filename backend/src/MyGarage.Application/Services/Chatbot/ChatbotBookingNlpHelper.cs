using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MyGarage.Services;

internal static class ChatbotBookingNlpHelper
{
    internal static readonly Regex NumberRegex = new(@"\d+", RegexOptions.Compiled);

    public static T? PickByIndex<T>(IReadOnlyList<T> options, IReadOnlyList<long> numbers) where T : class
    {
        if (options.Count == 0 || numbers.Count == 0)
        {
            return null;
        }

        foreach (var number in numbers)
        {
            if (number >= 1 && number <= options.Count)
            {
                return options[(int)number - 1];
            }
        }

        return null;
    }

    public static bool HasTextualOverlap(string source, string target)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        if (source.Contains(target, StringComparison.Ordinal) || target.Contains(source, StringComparison.Ordinal))
        {
            return true;
        }

        return ScoreTextOverlap(source, target) >= 2;
    }

    public static int ScoreTextOverlap(string source, string target)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
        {
            return 0;
        }

        var sourceTokens = source.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var targetTokens = target.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (sourceTokens.Length == 0 || targetTokens.Length == 0)
        {
            return 0;
        }

        var score = 0;
        foreach (var token in targetTokens)
        {
            if (token.Length < 2)
            {
                continue;
            }

            if (sourceTokens.Any(sourceToken => sourceToken.Equals(token, StringComparison.Ordinal)))
            {
                score++;
            }
        }

        return score;
    }

    public static bool ContainsNormalized(string? source, string normalizedInput)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(normalizedInput))
        {
            return false;
        }

        var normalizedSource = NormalizeNlp(source);
        return normalizedSource.Contains(normalizedInput, StringComparison.Ordinal)
            || normalizedInput.Contains(normalizedSource, StringComparison.Ordinal);
    }

    public static IEnumerable<long> ExtractNumbers(string text)
        => NumberRegex.Matches(text ?? string.Empty)
            .Select(x => long.TryParse(x.Value, out var n) ? n : 0)
            .Where(x => x > 0);

    public static string NormalizeNlp(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var lowered = text.Trim().ToLowerInvariant().Replace('\u0111', 'd').Replace('\u0110', 'd');
        var decomposed = lowered.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            sb.Append(char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) ? ch : ' ');
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static string FormatCurrency(decimal amount)
        => string.Format(CultureInfo.InvariantCulture, "{0:N0} VND", amount);
}
