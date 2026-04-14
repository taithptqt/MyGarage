namespace MyGarage.Common;

public static class VietnamTime
{
    private const string WindowsTimeZoneId = "SE Asia Standard Time";
    private const string IanaTimeZoneId = "Asia/Ho_Chi_Minh";

    private static readonly TimeZoneInfo TimeZone = ResolveTimeZone();

    public static DateTime Now
    {
        get
        {
            var utcNow = DateTime.UtcNow;
            return TimeZoneInfo.ConvertTimeFromUtc(utcNow, TimeZone);
        }
    }

    public static DateOnly Today => DateOnly.FromDateTime(Now);

    public static DateTimeOffset OffsetNow
    {
        get
        {
            var now = Now;
            return new DateTimeOffset(now, TimeZone.GetUtcOffset(now));
        }
    }

    public static DateTime ToVietnamTime(DateTime value)
    {
        if (value == default)
        {
            return value;
        }

        return value.Kind switch
        {
            DateTimeKind.Utc => TimeZoneInfo.ConvertTimeFromUtc(value, TimeZone),
            DateTimeKind.Local => TimeZoneInfo.ConvertTime(value, TimeZone),
            _ => value
        };
    }

    public static DateTimeOffset ToVietnamOffset(DateTimeOffset value)
    {
        if (value == default)
        {
            return value;
        }

        return TimeZoneInfo.ConvertTime(value, TimeZone);
    }

    private static TimeZoneInfo ResolveTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(WindowsTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(IanaTimeZoneId);
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(IanaTimeZoneId);
        }
    }
}
