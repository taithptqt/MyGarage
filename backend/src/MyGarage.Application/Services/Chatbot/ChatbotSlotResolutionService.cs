using System.Globalization;
using System.Text.RegularExpressions;
using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Interfaces;

namespace MyGarage.Services;

/// <summary>
/// Handles date/time parsing, branch operating-day checks, slot loading/picking, and slot hold lifecycle for chatbot bookings.
/// </summary>
public sealed class ChatbotSlotResolutionService : IChatbotSlotResolutionService
{
    private static readonly Regex TimeHintRegex = new(@"\b(?<hour>[01]?\d|2[0-3])\s*(?:h|gio)\s*(?<minute>[0-5]?\d)?\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IChatbotBookingStore _store;
    private readonly IAppointmentAvailabilityQueryService _appointmentAvailabilityService;
    private readonly IAppointmentSlotHoldService _appointmentSlotHoldService;
    private readonly ISystemClock _clock;
    private readonly IAppLogger<ChatbotSlotResolutionService> _logger;

    public ChatbotSlotResolutionService(
        IChatbotBookingStore store,
        IAppointmentAvailabilityQueryService appointmentAvailabilityService,
        IAppointmentSlotHoldService appointmentSlotHoldService,
        ISystemClock clock,
        IAppLogger<ChatbotSlotResolutionService> logger)
    {
        _store = store;
        _appointmentAvailabilityService = appointmentAvailabilityService;
        _appointmentSlotHoldService = appointmentSlotHoldService;
        _clock = clock;
        _logger = logger;
    }

    public async Task<List<ChatbotBookingSlotOptionDto>> LoadAvailableSlotsAsync(
        long branchId,
        IReadOnlyList<long> serviceIds,
        DateOnly date,
        TimeSpan? preferredTime,
        CancellationToken ct = default)
    {
        var raw = await _appointmentAvailabilityService.GetAvailableSlotsAsync(
            branchId,
            date,
            string.Join(",", serviceIds),
            bookingFlow: "FIXED",
            vehicleCount: 1,
            isParallel: false);

        var available = raw
            .Where(s => s.IsAvailable && s.RemainingCapacity > 0)
            .Select(s => new ChatbotBookingSlotOptionDto
            {
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                RemainingCapacity = s.RemainingCapacity,
                MaxCapacity = s.MaxCapacity
            })
            .ToList();

        if (preferredTime.HasValue)
        {
            return available
                .OrderBy(option => Math.Abs((option.StartTime.TimeOfDay - preferredTime.Value).TotalMinutes))
                .ThenBy(option => option.StartTime)
                .Take(8)
                .ToList();
        }

        return available
            .OrderBy(s => s.StartTime)
            .Take(8)
            .ToList();
    }

    public TimeSpan? ParsePreferredSlotTime(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = TimeHintRegex.Match(text);
        if (!match.Success)
        {
            return null;
        }

        if (!int.TryParse(match.Groups["hour"].Value, out var hour) || hour is < 0 or > 23)
        {
            return null;
        }

        var minute = 0;
        if (match.Groups["minute"].Success
            && !string.IsNullOrWhiteSpace(match.Groups["minute"].Value)
            && int.TryParse(match.Groups["minute"].Value, out var parsedMinute)
            && parsedMinute is >= 0 and <= 59)
        {
            minute = parsedMinute;
        }

        return new TimeSpan(hour, minute, 0);
    }

    public async Task<bool> IsBookableDayAsync(long branchId, DateOnly date, CancellationToken ct = default)
    {
        var exception = await _store.GetBranchExceptionAsync(branchId, date, ct);
        if (exception is not null)
        {
            if (exception.IsClosed == true) return false;
            if (exception.StartTime.HasValue && exception.EndTime.HasValue && exception.StartTime.Value >= exception.EndTime.Value) return false;
            return true;
        }

        var dow = (int)date.DayOfWeek;
        var operatingHour = await _store.GetBranchOperatingHourAsync(branchId, dow, ct);
        if (operatingHour is null) return false;
        if (operatingHour.IsDayOff == true) return false;
        if (operatingHour.OpenTime >= operatingHour.CloseTime) return false;
        if (operatingHour.SlotDurationMinutes <= 0 || operatingHour.MaxConcurrentBookings <= 0) return false;
        return true;
    }

    public Task<SlotHoldResponseDto> CreateSlotHoldAsync(
        long branchId,
        IReadOnlyList<long> serviceIds,
        DateTime startTime,
        DateTime endTime)
    {
        return _appointmentSlotHoldService.CreateSlotHoldAsync(new SlotHoldRequestDto
        {
            BranchId = branchId,
            StartTime = startTime,
            EndTime = endTime,
            BookingFlow = "FIXED",
            RequestedServiceIds = string.Join(",", serviceIds),
            VehicleCount = 1
        });
    }

    public async Task ReleaseSlotHoldAsync(string? holdToken)
    {
        if (string.IsNullOrWhiteSpace(holdToken))
        {
            return;
        }

        try
        {
            await _appointmentSlotHoldService.ReleaseSlotHoldAsync(holdToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to release slot hold token {HoldToken}", holdToken);
        }
    }

    public string FormatSlotOptions(IReadOnlyList<ChatbotBookingSlotOptionDto> options)
    {
        if (options.Count == 0)
        {
            return "- Không có slot khả dụng.";
        }

        var lines = options
            .Select((option, index) =>
                $"{index + 1}. {option.StartTime:HH:mm dd/MM} - {option.EndTime:HH:mm} (còn {option.RemainingCapacity}/{option.MaxCapacity})")
            .ToList();
        return string.Join(Environment.NewLine, lines);
    }

    public ChatbotBookingSlotOptionDto? PickSlot(IReadOnlyList<ChatbotBookingSlotOptionDto> options, string message)
    {
        var normalized = ChatbotBookingNlpHelper.NormalizeNlp(message);
        if (normalized.Contains("slot", StringComparison.Ordinal)
            || normalized.Contains("khung", StringComparison.Ordinal)
            || normalized.Contains("so", StringComparison.Ordinal))
        {
            var idx = (int)ChatbotBookingNlpHelper.ExtractNumbers(message).FirstOrDefault();
            if (idx >= 1 && idx <= options.Count) return options[idx - 1];
        }

        if (TimeHintRegex.Match(message ?? string.Empty) is { Success: true } timeMatch)
        {
            var hourText = timeMatch.Groups["hour"].Value;
            var minuteText = timeMatch.Groups["minute"].Value;
            if (int.TryParse(hourText, out var hour) && hour is >= 0 and <= 23)
            {
                var hasMinute = int.TryParse(minuteText, out var minute);
                if (hasMinute && minute is >= 0 and <= 59)
                {
                    return options.FirstOrDefault(o => o.StartTime.Hour == hour && o.StartTime.Minute == minute);
                }

                return options.FirstOrDefault(o => o.StartTime.Hour == hour);
            }
        }

        if (DateTime.TryParse(message, out var dt))
        {
            var byMoment = options.FirstOrDefault(o => IsSameMoment(o.StartTime, dt));
            if (byMoment is not null)
            {
                return byMoment;
            }

            if (dt.TimeOfDay != TimeSpan.Zero)
            {
                return options.FirstOrDefault(o => o.StartTime.Hour == dt.Hour && o.StartTime.Minute == dt.Minute);
            }
        }

        return null;
    }

    public DateOnly? ParseDate(string text)
    {
        var raw = (text ?? string.Empty).Trim();
        var normalized = ChatbotBookingNlpHelper.NormalizeNlp(text);
        var today = DateOnly.FromDateTime(_clock.LocalNow);
        if (normalized is "today" or "hom nay"
            || normalized.Contains("hom nay", StringComparison.Ordinal))
        {
            return today;
        }

        if (normalized is "tomorrow" or "ngay mai" or "mai"
            || normalized.Contains("ngay mai", StringComparison.Ordinal))
        {
            return today.AddDays(1);
        }

        if (DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d1))
        {
            return d1;
        }

        if (DateOnly.TryParseExact(raw, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2))
        {
            return d2;
        }

        if (DateOnly.TryParseExact(raw, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d3))
        {
            return d3;
        }

        var shortDateMatch = Regex.Match(normalized, @"(?:^|\b)(?<day>\d{1,2})[\/\-](?<month>\d{1,2})(?:\b|$)");
        if (shortDateMatch.Success
            && int.TryParse(shortDateMatch.Groups["day"].Value, out var shortDay)
            && int.TryParse(shortDateMatch.Groups["month"].Value, out var shortMonth))
        {
            var candidate = ResolveMonthDayCandidate(today, shortDay, shortMonth);
            if (candidate.HasValue)
            {
                return candidate.Value;
            }
        }

        var explicitDayMatch = Regex.Match(normalized, @"(?:^|\b)ngay\s+(?<day>\d{1,2})(?:\b|$)");
        if (explicitDayMatch.Success && int.TryParse(explicitDayMatch.Groups["day"].Value, out var explicitDay))
        {
            var candidate = ResolveDayOnlyCandidate(today, explicitDay);
            if (candidate.HasValue)
            {
                return candidate.Value;
            }
        }

        var dayOnlyMatch = Regex.Match(normalized, @"^(?:ngay\s*)?(?<day>\d{1,2})$");
        if (dayOnlyMatch.Success && int.TryParse(dayOnlyMatch.Groups["day"].Value, out var dayOnly))
        {
            var candidate = ResolveDayOnlyCandidate(today, dayOnly);
            if (candidate.HasValue)
            {
                return candidate.Value;
            }
        }

        return null;
    }

    public bool TryParseBookingDate(string? text, out DateOnly date)
        => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    public bool IsSameMoment(DateTime left, DateTime right)
        => left.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) == right.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateOnly? ResolveMonthDayCandidate(DateOnly today, int day, int month)
    {
        if (month is < 1 or > 12 || day < 1)
        {
            return null;
        }

        var year = today.Year;
        if (day > DateTime.DaysInMonth(year, month))
        {
            return null;
        }

        var candidate = new DateOnly(year, month, day);
        if (candidate < today)
        {
            year += 1;
            if (day > DateTime.DaysInMonth(year, month))
            {
                return null;
            }

            candidate = new DateOnly(year, month, day);
        }

        return candidate;
    }

    private static DateOnly? ResolveDayOnlyCandidate(DateOnly today, int day)
    {
        if (day < 1 || day > 31)
        {
            return null;
        }

        var year = today.Year;
        var month = today.Month;

        for (var i = 0; i < 14; i++)
        {
            if (day <= DateTime.DaysInMonth(year, month))
            {
                var candidate = new DateOnly(year, month, day);
                if (candidate >= today)
                {
                    return candidate;
                }
            }

            month += 1;
            if (month > 12)
            {
                month = 1;
                year += 1;
            }
        }

        return null;
    }
}
