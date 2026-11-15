using System.Text.Json.Nodes;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Models;

namespace MyGarage.Interfaces;

/// <summary>
/// Handles booking draft state progression across vehicle, service, branch, and slot selection steps.
/// </summary>
public interface IChatbotBookingDraftService
{
    Task<ChatbotBookingWorkflowResponseDto> StepVehicleAsync(
        Customer customer,
        long? sessionBranchId,
        JsonObject root,
        ChatbotBookingState state,
        string message,
        CancellationToken ct = default);

    Task<ChatbotBookingWorkflowResponseDto> StepServicesAsync(
        Customer customer,
        long? sessionBranchId,
        JsonObject root,
        ChatbotBookingState state,
        string message,
        CancellationToken ct = default);

    Task<ChatbotBookingWorkflowResponseDto> StepBranchAsync(
        Customer customer,
        JsonObject root,
        ChatbotBookingState state,
        long? sessionBranchId,
        string message,
        CancellationToken ct = default);

    Task<ChatbotBookingWorkflowResponseDto> StepSlotsAsync(
        Customer customer,
        JsonObject root,
        ChatbotBookingState state,
        string message,
        CancellationToken ct = default);

    Task ClearSlotSelectionAsync(ChatbotBookingState state, CancellationToken ct = default);
}
