using System.Text.Json.Nodes;
using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class ChatbotBookingWorkflowService : IChatbotBookingWorkflowService
{
    private readonly IChatbotBookingStore _store;
    private readonly IChatbotBookingDraftService _draftService;
    private readonly IChatbotBookingFinalizationService _finalizationService;
    private readonly ISystemClock _clock;

    public ChatbotBookingWorkflowService(
        IChatbotBookingStore store,
        IChatbotBookingDraftService draftService,
        IChatbotBookingFinalizationService finalizationService,
        ISystemClock clock)
    {
        _store = store;
        _draftService = draftService;
        _finalizationService = finalizationService;
        _clock = clock;
    }

    public async Task<ChatbotBookingWorkflowResponseDto> RunAsync(
        long customerId,
        long sessionId,
        long? sessionBranchId,
        string intentStateJson,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        if (customerId <= 0) throw new InvalidOperationException("Authenticated CustomerId is required.");
        var customer = await _store.GetActiveCustomerEntityAsync(customerId, cancellationToken)
            ?? throw new KeyNotFoundException("Customer not found.");

        var root = ChatbotBookingResponseBuilder.ParseRoot(intentStateJson);
        var state = ChatbotBookingState.From(intentStateJson);
        var msg = userMessage?.Trim() ?? string.Empty;
        if (state.Step == ChatbotBookingStep.Completed)
        {
            state = new ChatbotBookingState();
        }

        if (state.Step != ChatbotBookingStep.IdentifyOrCreateVehicle && ChatbotBookingIntentHelper.IsRestartBookingRequest(msg))
        {
            await _draftService.ClearSlotSelectionAsync(state, cancellationToken);
            state = new ChatbotBookingState();
            await _finalizationService.LogActionAsync(
                sessionId,
                ChatbotActionStatus.SKIPPED,
                "BOOKING_FUNNEL_RESTART",
                "USER_RESTARTED",
                "Booking flow restarted by explicit user request.",
                null,
                cancellationToken);
        }

        if (ChatbotBookingIntentHelper.LooksLikeBookingIntent(msg))
        {
            state.PrefillInput = msg;
        }

        if (ChatbotBookingIntentHelper.IsDirectBookingRequest(msg))
        {
            state.AutoConfirmRequested = true;
            state.PrefillInput = msg;
        }

        if (!string.IsNullOrWhiteSpace(msg))
        {
            state.TurnCount++;
        }

        var commandResult = await HandleNavigationCommandAsync(customer, sessionId, sessionBranchId, root, state, msg, cancellationToken);
        if (commandResult is not null)
        {
            await _finalizationService.LogFunnelProgressAsync(sessionId, state, commandResult, msg, cancellationToken);
            return commandResult;
        }

        var response = state.Step switch
        {
            ChatbotBookingStep.IdentifyOrCreateVehicle => await _draftService.StepVehicleAsync(customer, sessionBranchId, root, state, msg, cancellationToken),
            ChatbotBookingStep.SelectServices => await _draftService.StepServicesAsync(customer, sessionBranchId, root, state, msg, cancellationToken),
            ChatbotBookingStep.SelectBranch => await _draftService.StepBranchAsync(customer, root, state, sessionBranchId, msg, cancellationToken),
            ChatbotBookingStep.ComputeAvailableSlots => await _draftService.StepSlotsAsync(customer, root, state, msg, cancellationToken),
            ChatbotBookingStep.FinalizeBooking => await _finalizationService.FinalizeAsync(customer, sessionId, root, state, msg, cancellationToken),
            _ => ChatbotBookingResponseBuilder.Build(root, state.Reset(), _clock.UtcNow, true, "Luồng đặt lịch đã được đặt lại.")
        };

        await _finalizationService.LogFunnelProgressAsync(sessionId, state, response, msg, cancellationToken);
        return response;
    }

    private async Task<ChatbotBookingWorkflowResponseDto?> HandleNavigationCommandAsync(
        Customer customer,
        long sessionId,
        long? sessionBranchId,
        JsonObject root,
        ChatbotBookingState state,
        string message,
        CancellationToken ct)
    {
        var command = ChatbotBookingIntentHelper.ResolveNavigationCommand(message);
        if (command == ChatbotBookingNavigationCommand.None)
        {
            return null;
        }

        switch (command)
        {
            case ChatbotBookingNavigationCommand.Cancel:
                await _draftService.ClearSlotSelectionAsync(state, ct);
                state = state.Reset();
                await _finalizationService.LogActionAsync(sessionId, ChatbotActionStatus.SKIPPED, "BOOKING_FUNNEL_EXIT", "USER_CANCELED", "Booking flow canceled by user command.", null, ct);
                return ChatbotBookingResponseBuilder.Build(root, state, _clock.UtcNow, true, "Đã hủy luồng đặt lịch. Nếu bạn muốn đặt lịch lại, vui lòng nhập `đặt lịch`.");

            case ChatbotBookingNavigationCommand.Restart:
                await _draftService.ClearSlotSelectionAsync(state, ct);
                state = state.Reset();
                await _finalizationService.LogActionAsync(sessionId, ChatbotActionStatus.SKIPPED, "BOOKING_FUNNEL_RESTART", "USER_RESTARTED", "Booking flow restarted by user command.", null, ct);
                return ChatbotBookingResponseBuilder.Build(root, state, _clock.UtcNow, true, "Đã bắt đầu lại luồng đặt lịch. Bước 1/5 - Vui lòng chọn xe.");

            case ChatbotBookingNavigationCommand.ChangeVehicle:
                await _draftService.ClearSlotSelectionAsync(state, ct);
                state.PrefillInput = null;
                state.VehicleId = null;
                state.ServiceCategoryId = null;
                state.ServiceIds.Clear();
                state.BranchId = null;
                state.BookingDate = null;
                state.Step = ChatbotBookingStep.IdentifyOrCreateVehicle;
                return await _draftService.StepVehicleAsync(customer, sessionBranchId, root, state, string.Empty, ct);

            case ChatbotBookingNavigationCommand.ChangeService:
                await _draftService.ClearSlotSelectionAsync(state, ct);
                state.PrefillInput = null;
                state.ServiceCategoryId = null;
                state.ServiceIds.Clear();
                state.BranchId = null;
                state.BookingDate = null;
                state.Step = ChatbotBookingStep.SelectServices;
                return await _draftService.StepServicesAsync(customer, sessionBranchId, root, state, string.Empty, ct);

            case ChatbotBookingNavigationCommand.ChangeDate:
                await _draftService.ClearSlotSelectionAsync(state, ct);
                state.PrefillInput = null;
                state.BookingDate = null;
                if (!state.BranchId.HasValue)
                {
                    state.Step = ChatbotBookingStep.SelectBranch;
                    return await _draftService.StepBranchAsync(customer, root, state, sessionBranchId, string.Empty, ct);
                }

                state.Step = ChatbotBookingStep.ComputeAvailableSlots;
                return await _draftService.StepSlotsAsync(customer, root, state, string.Empty, ct);

            case ChatbotBookingNavigationCommand.Back:
                return await HandleBackCommandAsync(customer, sessionBranchId, root, state, ct);
        }

        return null;
    }

    private async Task<ChatbotBookingWorkflowResponseDto> HandleBackCommandAsync(
        Customer customer,
        long? sessionBranchId,
        JsonObject root,
        ChatbotBookingState state,
        CancellationToken ct)
    {
        switch (state.Step)
        {
            case ChatbotBookingStep.IdentifyOrCreateVehicle:
                return await _draftService.StepVehicleAsync(customer, sessionBranchId, root, state, string.Empty, ct);

            case ChatbotBookingStep.SelectServices:
                await _draftService.ClearSlotSelectionAsync(state, ct);
                state.VehicleId = null;
                state.ServiceCategoryId = null;
                state.ServiceIds.Clear();
                state.BranchId = null;
                state.BookingDate = null;
                state.Step = ChatbotBookingStep.IdentifyOrCreateVehicle;
                return await _draftService.StepVehicleAsync(customer, sessionBranchId, root, state, string.Empty, ct);

            case ChatbotBookingStep.SelectBranch:
                await _draftService.ClearSlotSelectionAsync(state, ct);
                state.ServiceCategoryId = null;
                state.ServiceIds.Clear();
                state.BranchId = null;
                state.BookingDate = null;
                state.Step = ChatbotBookingStep.SelectServices;
                return await _draftService.StepServicesAsync(customer, sessionBranchId, root, state, string.Empty, ct);

            case ChatbotBookingStep.ComputeAvailableSlots:
                await _draftService.ClearSlotSelectionAsync(state, ct);
                state.BranchId = null;
                state.BookingDate = null;
                state.Step = ChatbotBookingStep.SelectBranch;
                return await _draftService.StepBranchAsync(customer, root, state, sessionBranchId, string.Empty, ct);

            case ChatbotBookingStep.FinalizeBooking:
                await _draftService.ClearSlotSelectionAsync(state, ct);
                state.Step = ChatbotBookingStep.ComputeAvailableSlots;
                return await _draftService.StepSlotsAsync(customer, root, state, string.Empty, ct);

            case ChatbotBookingStep.Completed:
                await _draftService.ClearSlotSelectionAsync(state, ct);
                state = state.Reset();
                return await _draftService.StepVehicleAsync(customer, sessionBranchId, root, state, string.Empty, ct);

            default:
                return await _draftService.StepVehicleAsync(customer, sessionBranchId, root, state, string.Empty, ct);
        }
    }
}
