namespace MyGarage.Services;

internal enum ChatbotBookingNavigationCommand
{
    None = 0,
    Back = 1,
    ChangeVehicle = 2,
    ChangeService = 3,
    ChangeDate = 4,
    Cancel = 5,
    Restart = 6
}

internal static class ChatbotBookingIntentHelper
{
    public static bool IsDirectBookingRequest(string message)
    {
        var n = ChatbotBookingNlpHelper.NormalizeNlp(message);
        if (string.IsNullOrWhiteSpace(n))
        {
            return false;
        }

        if (!LooksLikeBookingIntent(message))
        {
            return false;
        }

        return n.Contains("giup toi", StringComparison.Ordinal)
            || n.Contains("giup em", StringComparison.Ordinal)
            || n.Contains("ho toi", StringComparison.Ordinal)
            || n.Contains("ho em", StringComparison.Ordinal)
            || n.Contains("book for me", StringComparison.Ordinal)
            || n.Contains("tu dong", StringComparison.Ordinal)
            || n.Contains("luon", StringComparison.Ordinal)
            || n.Contains("ngay", StringComparison.Ordinal);
    }

    public static bool LooksLikeBookingIntent(string? message)
    {
        var n = ChatbotBookingNlpHelper.NormalizeNlp(message);
        if (string.IsNullOrWhiteSpace(n))
        {
            return false;
        }

        return n.Contains("dat lich", StringComparison.Ordinal)
            || n.Contains("book", StringComparison.Ordinal)
            || n.Contains("appointment", StringComparison.Ordinal);
    }

    public static bool IsRestartBookingRequest(string message)
    {
        var n = ChatbotBookingNlpHelper.NormalizeNlp(message);
        if (string.IsNullOrWhiteSpace(n))
        {
            return false;
        }

        if (n.Contains("dat lich moi", StringComparison.Ordinal)
            || n.Contains("dat lich lai", StringComparison.Ordinal)
            || n.Contains("bat dau lai", StringComparison.Ordinal)
            || n.Contains("start over", StringComparison.Ordinal)
            || n.Contains("restart", StringComparison.Ordinal))
        {
            return true;
        }

        return IsDirectBookingRequest(message);
    }

    public static bool ShouldTryPrefillFallback(string message, string? prefillInput)
    {
        if (string.IsNullOrWhiteSpace(prefillInput))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return true;
        }

        return !ChatbotBookingNlpHelper.NormalizeNlp(message).Equals(ChatbotBookingNlpHelper.NormalizeNlp(prefillInput), StringComparison.Ordinal);
    }

    public static ChatbotBookingNavigationCommand ResolveNavigationCommand(string message)
    {
        var normalized = ChatbotBookingNlpHelper.NormalizeNlp(message);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return ChatbotBookingNavigationCommand.None;
        }

        if (normalized.Contains("dat lich moi", StringComparison.Ordinal)
            || normalized.Contains("dat lich lai", StringComparison.Ordinal)
            || normalized.Contains("bat dau lai", StringComparison.Ordinal)
            || normalized.Contains("start over", StringComparison.Ordinal)
            || normalized.Contains("restart", StringComparison.Ordinal))
        {
            return ChatbotBookingNavigationCommand.Restart;
        }

        if (normalized.Contains("huy", StringComparison.Ordinal)
            || normalized.Contains("thoat", StringComparison.Ordinal)
            || normalized.Contains("cancel", StringComparison.Ordinal))
        {
            return ChatbotBookingNavigationCommand.Cancel;
        }

        if (normalized.Contains("doi xe", StringComparison.Ordinal))
        {
            return ChatbotBookingNavigationCommand.ChangeVehicle;
        }

        if (normalized.Contains("doi dich vu", StringComparison.Ordinal)
            || normalized.Contains("doi nhom", StringComparison.Ordinal))
        {
            return ChatbotBookingNavigationCommand.ChangeService;
        }

        if (normalized.Contains("doi ngay", StringComparison.Ordinal))
        {
            return ChatbotBookingNavigationCommand.ChangeDate;
        }

        if (normalized.Equals("quay lai", StringComparison.Ordinal)
            || normalized.Equals("back", StringComparison.Ordinal)
            || normalized.Equals("tro lai", StringComparison.Ordinal))
        {
            return ChatbotBookingNavigationCommand.Back;
        }

        return ChatbotBookingNavigationCommand.None;
    }
}
