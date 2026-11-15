using System.Text.Json.Nodes;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Interfaces;

/// <summary>
/// Handles booking finalization (slot re-verification, appointment creation, race-condition recovery) and booking workflow audit logging.
/// </summary>
public interface IChatbotBookingFinalizationService
{
    Task<ChatbotBookingWorkflowResponseDto> FinalizeAsync(
        Customer customer,
        long sessionId,
        JsonObject root,
        ChatbotBookingState state,
        string message,
        CancellationToken ct = default);

    Task LogFunnelProgressAsync(
        long sessionId,
        ChatbotBookingState state,
        ChatbotBookingWorkflowResponseDto response,
        string userMessage,
        CancellationToken ct = default);

    Task LogActionAsync(
        long sessionId,
        ChatbotActionStatus status,
        string actionType,
        string? errorCode,
        string? errorMessage,
        ChatbotBookingResultDto? payload,
        CancellationToken ct = default);
}
