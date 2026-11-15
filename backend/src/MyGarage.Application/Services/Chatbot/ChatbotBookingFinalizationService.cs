using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class ChatbotBookingFinalizationService : IChatbotBookingFinalizationService
{
    private readonly IChatbotStore _store;
    private readonly IChatbotSlotResolutionService _slotResolutionService;
    private readonly IChatbotBookingExecutionService _bookingExecutionService;
    private readonly ISystemClock _clock;
    private readonly IAppLogger<ChatbotBookingFinalizationService> _logger;

    public ChatbotBookingFinalizationService(
        IChatbotStore store,
        IChatbotSlotResolutionService slotResolutionService,
        IChatbotBookingExecutionService bookingExecutionService,
        ISystemClock clock,
        IAppLogger<ChatbotBookingFinalizationService> logger)
    {
        _store = store;
        _slotResolutionService = slotResolutionService;
        _bookingExecutionService = bookingExecutionService;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ChatbotBookingWorkflowResponseDto> FinalizeAsync(
        Customer customer,
        long sessionId,
        JsonObject root,
        ChatbotBookingState state,
        string message,
        CancellationToken ct = default)
    {
        if (!_bookingExecutionService.IsBookingConfirmationMessage(message))
        {
            return ChatbotBookingResponseBuilder.Build(root, state, _clock.UtcNow, true, "Để xác nhận đặt lịch, bạn trả lời `đồng ý`, `xác nhận` hoặc `yes`.");
        }

        var hasStart = DateTime.TryParse(state.SelectedSlotStartUtc, null, DateTimeStyles.RoundtripKind, out var start);
        if (hasStart)
        {
            start = _bookingExecutionService.NormalizeBookingDateTime(start);
        }

        if (DateTime.TryParse(state.SlotHoldExpiresAtUtc, null, DateTimeStyles.RoundtripKind, out var holdExpiresAt)
            && holdExpiresAt.ToUniversalTime() <= _clock.UtcNow)
        {
            await ClearSlotSelectionAsync(state);
            state.Step = ChatbotBookingStep.ComputeAvailableSlots;
            return ChatbotBookingResponseBuilder.Build(root, state, _clock.UtcNow, true, "Giữ chỗ đã hết hạn. Vui lòng chọn lại khung giờ.");
        }

        if (!state.BranchId.HasValue
            || state.ServiceIds.Count == 0
            || !state.VehicleId.HasValue
            || !_slotResolutionService.TryParseBookingDate(state.BookingDate, out var date)
            || !hasStart)
        {
            state.Step = ChatbotBookingStep.ComputeAvailableSlots;
            return ChatbotBookingResponseBuilder.Build(root, state, _clock.UtcNow, true, "Thông tin đặt lịch chưa đầy đủ. Vui lòng chọn lại khung giờ.");
        }

        var slots = await _slotResolutionService.LoadAvailableSlotsAsync(state.BranchId.Value, state.ServiceIds, date, preferredTime: null, ct);
        if (!slots.Any(slot => _slotResolutionService.IsSameMoment(slot.StartTime, start)))
        {
            state.Step = ChatbotBookingStep.ComputeAvailableSlots;
            await ClearSlotSelectionAsync(state);
            await LogActionAsync(sessionId, ChatbotActionStatus.FAILED, "CREATE_APPOINTMENT", "SLOT_CONFLICT", "Selected slot no longer available.", null, ct);
            return ChatbotBookingResponseBuilder.Build(root, state, _clock.UtcNow, true, "Khung giờ vừa chọn đã hết chỗ. Vui lòng chọn khung khác.");
        }

        try
        {
            var voucherSuggestion = await _bookingExecutionService.ResolveVoucherSuggestionAsync(customer, state.BranchId, state.VehicleId, state.ServiceIds, ct);
            var result = await _bookingExecutionService.ExecuteBookingAsync(
                customer,
                state.BranchId.Value,
                state.VehicleId.Value,
                state.ServiceIds,
                start,
                state.SelectedSlotEndUtc,
                state.SlotHoldToken,
                ct);
            await ClearSlotSelectionAsync(state);
            state.Step = ChatbotBookingStep.Completed;
            state.PrefillInput = null;
            await LogActionAsync(sessionId, ChatbotActionStatus.SUCCESS, "CREATE_APPOINTMENT", null, null, result, ct);
            await LogVoucherActionAsync(sessionId, voucherSuggestion, ct);
            return ChatbotBookingResponseBuilder.Build(
                root,
                state,
                _clock.UtcNow,
                false,
                result.Message,
                bookingResult: result,
                noShowWarning: ChatbotBookingResponseBuilder.Warning(customer.NoShowCount),
                voucherSuggestion: voucherSuggestion);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Booking race condition. SessionId={SessionId}, CustomerId={CustomerId}", sessionId, customer.Id);
            state.Step = ChatbotBookingStep.ComputeAvailableSlots;
            await ClearSlotSelectionAsync(state);
            await LogActionAsync(sessionId, ChatbotActionStatus.FAILED, "CREATE_APPOINTMENT", "BOOKING_RACE_CONDITION", ex.Message, null, ct);
            return ChatbotBookingResponseBuilder.Build(root, state, _clock.UtcNow, true, _bookingExecutionService.BuildFinalizeErrorMessage(ex.Message));
        }
    }

    public async Task LogFunnelProgressAsync(
        long sessionId,
        ChatbotBookingState state,
        ChatbotBookingWorkflowResponseDto response,
        string userMessage,
        CancellationToken ct = default)
    {
        var payload = new
        {
            step = response.Step,
            requires_user_input = response.RequiresUserInput,
            has_booking_result = response.BookingResult is not null,
            quick_reply_count = response.QuickReplies?.Count ?? 0,
            turn_count = state.TurnCount,
            user_input = userMessage
        };

        await _store.AddChatbotActionLogWithTransactionAsync(new ChatbotActionLog
        {
            SessionId = sessionId,
            ActionType = "BOOKING_FUNNEL",
            Status = (response.Step == "COMPLETED" ? ChatbotActionStatus.SUCCESS : ChatbotActionStatus.PENDING).ToString().ToUpperInvariant(),
            ErrorCode = null,
            ErrorMessage = null,
            ActionPayload = JsonSerializer.Serialize(payload),
            CreatedAt = _clock.UtcNow
        }, ct);
    }

    public async Task LogActionAsync(
        long sessionId,
        ChatbotActionStatus status,
        string actionType,
        string? errorCode,
        string? errorMessage,
        ChatbotBookingResultDto? payload,
        CancellationToken ct = default)
    {
        await _store.AddChatbotActionLogWithTransactionAsync(new ChatbotActionLog
        {
            SessionId = sessionId,
            ActionType = actionType,
            Status = status.ToString().ToUpperInvariant(),
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            ActionPayload = payload is null ? null : JsonSerializer.Serialize(payload),
            CreatedAt = _clock.UtcNow
        }, ct);
    }

    private async Task LogVoucherActionAsync(long sessionId, ChatbotVoucherResolutionDto? voucher, CancellationToken ct)
    {
        await _store.AddChatbotActionLogWithTransactionAsync(new ChatbotActionLog
        {
            SessionId = sessionId,
            ActionType = "APPLY_VOUCHER",
            Status = (voucher is null ? ChatbotActionStatus.SKIPPED : ChatbotActionStatus.SUCCESS).ToString().ToUpperInvariant(),
            ErrorCode = voucher is null ? "NO_VALID_VOUCHER" : null,
            ErrorMessage = voucher is null ? "No eligible voucher for booking context." : null,
            ActionPayload = voucher is null
                ? null
                : JsonSerializer.Serialize(new
                {
                    voucher_id = voucher.VoucherId,
                    voucher_code = voucher.VoucherCode,
                    discount_amount = voucher.DiscountAmount,
                    expires_at = voucher.ExpiresAt
                }),
            CreatedAt = _clock.UtcNow
        }, ct);
    }

    private async Task ClearSlotSelectionAsync(ChatbotBookingState state)
    {
        await _slotResolutionService.ReleaseSlotHoldAsync(state.SlotHoldToken);
        state.ClearSlotSelection();
    }
}
