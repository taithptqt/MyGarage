using System.Text;

namespace MyGarage.Common;

public static class Utf8MojibakeRepair
{
    private static readonly string[] SuspiciousMarkers =
    [
        "Ã",
        "Â",
        "Ð",
        "Æ",
        "Ä",
        "áº",
        "á»",
        "á¼",
        "á½",
        "á¾",
        "á¿",
        "â€"
    ];

    public static string? Fix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (!LooksLikeMojibake(value))
        {
            return value;
        }

        var decoded = Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(value));
        return decoded.Contains('\uFFFD') ? value : decoded;
    }

    private static bool LooksLikeMojibake(string value)
    {
        foreach (var marker in SuspiciousMarkers)
        {
            if (value.Contains(marker, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
