using System.Text;

namespace MyGarage.Common;

public static class Utf8MojibakeRepair
{
    private static readonly string[] SuspiciousMarkers =
    [
        "\u00C3\u0192",
        "\u00C3\u201A",
        "\u00C3\u0090",
        "\u00C3\u2020",
        "\u00C3\u201E",
        "\u00C3\u00A1\u00C2\u00BA",
        "\u00C3\u00A1\u00C2\u00BB",
        "\u00C3\u00A1\u00C2\u00BC",
        "\u00C3\u00A1\u00C2\u00BD",
        "\u00C3\u00A1\u00C2\u00BE",
        "\u00C3\u00A1\u00C2\u00BF",
        "\u00C3\u00A2\u00E2\u201A\u00AC"
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
