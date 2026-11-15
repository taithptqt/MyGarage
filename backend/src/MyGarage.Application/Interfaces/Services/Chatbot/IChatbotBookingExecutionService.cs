using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Models;

namespace MyGarage.Interfaces;

/// <summary>
/// Executes single or batch appointment bookings, builds confirmation summaries, resolves voucher suggestions, and formats booking errors.
/// </summary>
public interface IChatbotBookingExecutionService
{
    Task<ChatbotBookingResultDto> ExecuteBookingAsync(
        Customer customer,
        long branchId,
        long vehicleId,
        IReadOnlyList<long> serviceIds,
        DateTime start,
        string? selectedSlotEndUtc,
        string? slotHoldToken,
        CancellationToken ct = default);

    Task<string> BuildBookingConfirmationSummaryAsync(
        long? vehicleId,
        long? branchId,
        IReadOnlyList<long> serviceIds,
        DateTime start,
        DateTime end,
        string? slotHoldExpiresAtUtc,
        CancellationToken ct = default);

    Task<ChatbotVoucherResolutionDto?> ResolveVoucherSuggestionAsync(
        Customer customer,
        long? branchId,
        long? vehicleId,
        IReadOnlyList<long> serviceIds,
        CancellationToken ct = default);

    bool IsBookingConfirmationMessage(string message);
    DateTime NormalizeBookingDateTime(DateTime value);
    string BuildFinalizeErrorMessage(string? rawMessage);
}
