using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.DTOs.Responses.Chatbot;

namespace MyGarage.Interfaces;

/// <summary>
/// Matches customer utterances to service categories and services for the chatbot booking workflow.
/// </summary>
public interface IChatbotServiceMatchingService
{
    ChatbotExtractedServiceSelection? TryExtractServiceSelectionFromBookingUtterance(
        IReadOnlyList<ChatbotBookingServiceCatalogRow> options,
        string? message);

    ChatbotBookingServiceCategoryOptionDto? PickServiceCategory(
        IReadOnlyList<ChatbotBookingServiceCategoryOptionDto> options,
        string message,
        bool allowNumericSelection = true);

    ChatbotBookingServiceCategoryOptionDto? PickServiceCategoryFromBookingUtterance(
        IReadOnlyList<ChatbotBookingServiceCategoryOptionDto> options,
        string message);

    List<long> PickServices(
        IReadOnlyList<ChatbotBookingServiceOptionDto> options,
        string message,
        IReadOnlyList<long> current,
        bool allowNumericSelection = true);

    bool MentionsSpecificService(string? message, IReadOnlyList<ChatbotBookingServiceOptionDto> options);
    bool HasServiceCategoryOrdinalSignal(string? message);
    bool HasServiceCategorySelectionSignal(string? message, IReadOnlyList<ChatbotBookingServiceCategoryOptionDto> categories);
    bool HasServiceOrdinalSelectionSignal(string? message);
    string FormatServiceCategoryOptions(IReadOnlyList<ChatbotBookingServiceCategoryOptionDto> options);
    string FormatServiceOptions(IReadOnlyList<ChatbotBookingServiceOptionRow> options);
}

public sealed class ChatbotExtractedServiceSelection
{
    public long CategoryId { get; set; }
    public List<long> ServiceIds { get; set; } = [];
}
