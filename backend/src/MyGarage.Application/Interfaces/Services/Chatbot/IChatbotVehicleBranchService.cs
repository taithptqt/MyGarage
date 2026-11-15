using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Models;

namespace MyGarage.Interfaces;

/// <summary>
/// Resolves, matches, creates, and formats customer vehicles and available branches for the chatbot booking workflow.
/// </summary>
public interface IChatbotVehicleBranchService
{
    Task<List<ChatbotBookingBranchOptionDto>> LoadAvailableBranchOptionsAsync(CancellationToken ct = default);
    Task<Vehicle?> TryCreateVehicleAsync(long customerId, string message, CancellationToken ct = default);

    ChatbotBookingVehicleOptionDto? PickVehicle(
        IReadOnlyList<ChatbotBookingVehicleOptionDto> options,
        string message,
        bool allowNumericSelection = true);

    ChatbotBookingVehicleOptionDto? PickVehicleFromBookingUtterance(
        IReadOnlyList<ChatbotBookingVehicleOptionDto> options,
        string message);

    ChatbotBookingBranchOptionDto? PickBranch(
        IReadOnlyList<ChatbotBookingBranchOptionDto> options,
        string message,
        bool allowNumericSelection = true);

    ChatbotBookingBranchOptionDto? PickBranchFromBookingUtterance(
        IReadOnlyList<ChatbotBookingBranchOptionDto> options,
        string message);

    string FormatVehicleOptions(IReadOnlyList<ChatbotBookingVehicleOptionDto> options);
    string FormatBranchOptions(IReadOnlyList<ChatbotBookingBranchOptionDto> options);
    bool LooksLikeVehicleSelectionReply(string? message);
}
