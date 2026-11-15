using MyGarage.DTOs.Responses;
using MyGarage.DTOs.Responses.Chatbot;

namespace MyGarage.Interfaces;

/// <summary>
/// Resolves booking dates, preferred times, branch availability, slot options, and slot holds for the chatbot booking workflow.
/// </summary>
public interface IChatbotSlotResolutionService
{
    DateOnly? ParseDate(string text);
    TimeSpan? ParsePreferredSlotTime(string? text);
    Task<bool> IsBookableDayAsync(long branchId, DateOnly date, CancellationToken ct = default);
    Task<List<ChatbotBookingSlotOptionDto>> LoadAvailableSlotsAsync(long branchId, IReadOnlyList<long> serviceIds, DateOnly date, TimeSpan? preferredTime, CancellationToken ct = default);
    ChatbotBookingSlotOptionDto? PickSlot(IReadOnlyList<ChatbotBookingSlotOptionDto> options, string message);
    string FormatSlotOptions(IReadOnlyList<ChatbotBookingSlotOptionDto> options);
    Task<SlotHoldResponseDto> CreateSlotHoldAsync(long branchId, IReadOnlyList<long> serviceIds, DateTime startTime, DateTime endTime);
    Task ReleaseSlotHoldAsync(string? holdToken);
    bool TryParseBookingDate(string? text, out DateOnly date);
    bool IsSameMoment(DateTime left, DateTime right);
}
