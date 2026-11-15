using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.DTOs.Responses;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class ChatbotBookingDraftService : IChatbotBookingDraftService
{
    private readonly IChatbotBookingStore _store;
    private readonly IChatbotVehicleBranchService _vehicleBranchService;
    private readonly IChatbotServiceMatchingService _serviceMatchingService;
    private readonly IChatbotSlotResolutionService _slotResolutionService;
    private readonly IChatbotBookingExecutionService _bookingExecutionService;
    private readonly ISystemClock _clock;

    public ChatbotBookingDraftService(
        IChatbotBookingStore store,
        IChatbotVehicleBranchService vehicleBranchService,
        IChatbotServiceMatchingService serviceMatchingService,
        IChatbotSlotResolutionService slotResolutionService,
        IChatbotBookingExecutionService bookingExecutionService,
        ISystemClock clock)
    {
        _store = store;
        _vehicleBranchService = vehicleBranchService;
        _serviceMatchingService = serviceMatchingService;
        _slotResolutionService = slotResolutionService;
        _bookingExecutionService = bookingExecutionService;
        _clock = clock;
    }

    public async Task<ChatbotBookingWorkflowResponseDto> StepVehicleAsync(
        Customer customer,
        long? sessionBranchId,
        JsonObject root,
        ChatbotBookingState state,
        string message,
        CancellationToken ct = default)
    {
        var options = await _store.GetBookingVehicleOptionsAsync(customer.Id, ct);

        if (options.Count == 1)
        {
            state.VehicleId = options[0].VehicleId;
            state.Step = ChatbotBookingStep.SelectServices;
            return await StepServicesAsync(customer, sessionBranchId, root, state, message, ct);
        }

        if (options.Count > 1)
        {
            var picked = state.AutoConfirmRequested
                ? _vehicleBranchService.PickVehicleFromBookingUtterance(options, message)
                : _vehicleBranchService.PickVehicle(options, message);
            if (picked is null && ChatbotBookingIntentHelper.ShouldTryPrefillFallback(message, state.PrefillInput))
            {
                picked = state.AutoConfirmRequested
                    ? _vehicleBranchService.PickVehicleFromBookingUtterance(options, state.PrefillInput!)
                    : _vehicleBranchService.PickVehicle(options, state.PrefillInput!);
            }

            if (picked is null)
            {
                var prompt = new StringBuilder();
                prompt.AppendLine("Bước 1/5 - Chọn xe.");
                prompt.AppendLine("Bạn đang có nhiều xe. Vui lòng chọn 1 xe bằng số thứ tự, biển số hoặc tên xe:");
                prompt.AppendLine(_vehicleBranchService.FormatVehicleOptions(options));
                prompt.Append("Ví dụ: `xe 2` hoặc `51A-12345`.");
                return ChatbotBookingResponseBuilder.Build(root, state, _clock.UtcNow, true, prompt.ToString(), vehicleOptions: options);
            }

            state.VehicleId = picked.VehicleId;
            state.Step = ChatbotBookingStep.SelectServices;
            return await StepServicesAsync(customer, sessionBranchId, root, state, message, ct);
        }

        var created = await _vehicleBranchService.TryCreateVehicleAsync(customer.Id, message, ct);
        if (created is null)
        {
            return ChatbotBookingResponseBuilder.Build(
                root,
                state,
                _clock.UtcNow,
                true,
                "Bước 1/5 - Chưa tìm thấy xe trong tài khoản. Bạn vui lòng gửi thông tin xe theo mẫu: `biển số + hãng/dòng xe + năm` (ví dụ: `51A-12345 Toyota Vios 2020`).");
        }

        state.VehicleId = created.Id;
        state.Step = ChatbotBookingStep.SelectServices;
        return await StepServicesAsync(customer, sessionBranchId, root, state, message, ct);
    }

    public async Task<ChatbotBookingWorkflowResponseDto> StepServicesAsync(
        Customer customer,
        long? sessionBranchId,
        JsonObject root,
        ChatbotBookingState state,
        string message,
        CancellationToken ct = default)
    {
        if (!state.VehicleId.HasValue)
        {
            throw new InvalidOperationException("Vehicle is required before services.");
        }

        var categoryRows = await _store.GetActiveServiceCategoriesAsync(ct);
        var categoryOptions = categoryRows
            .Select(item => new ChatbotBookingServiceCategoryOptionDto
            {
                CategoryId = item.CategoryId,
                CategoryName = item.CategoryName
            })
            .ToList();

        if (categoryOptions.Count == 0)
        {
            return ChatbotBookingResponseBuilder.Build(root, state, _clock.UtcNow, false, "Hệ thống chưa có dịch vụ active để đặt lịch.");
        }

        var fullCatalog = await _store.GetActiveServiceCatalogAsync(ct);

        if ((state.AutoConfirmRequested || ChatbotBookingIntentHelper.LooksLikeBookingIntent(message) || !string.IsNullOrWhiteSpace(state.PrefillInput))
            && state.ServiceIds.Count == 0)
        {
            var extracted = _serviceMatchingService.TryExtractServiceSelectionFromBookingUtterance(fullCatalog, message);
            if (extracted is null && ChatbotBookingIntentHelper.ShouldTryPrefillFallback(message, state.PrefillInput))
            {
                extracted = _serviceMatchingService.TryExtractServiceSelectionFromBookingUtterance(fullCatalog, state.PrefillInput!);
            }

            if (extracted is not null)
            {
                state.ServiceCategoryId = extracted.CategoryId;
                state.ServiceIds = extracted.ServiceIds.ToList();
            }
        }

        if (state.ServiceCategoryId.HasValue && categoryOptions.All(item => item.CategoryId != state.ServiceCategoryId.Value))
        {
            state.ServiceCategoryId = null;
            state.ServiceIds.Clear();
        }

        var currentMessageIsVehicleSelection = state.AutoConfirmRequested && _vehicleBranchService.LooksLikeVehicleSelectionReply(message);
        var canSelectCategoryFromCurrentMessage = !currentMessageIsVehicleSelection
            && (!state.ServiceCategoryId.HasValue || _serviceMatchingService.HasServiceCategorySelectionSignal(message, categoryOptions));
        var pickedCategory = canSelectCategoryFromCurrentMessage
            ? state.AutoConfirmRequested
                ? _serviceMatchingService.PickServiceCategoryFromBookingUtterance(categoryOptions, message)
                : _serviceMatchingService.PickServiceCategory(categoryOptions, message)
            : null;
        if (pickedCategory is null
            && !state.ServiceCategoryId.HasValue
            && ChatbotBookingIntentHelper.ShouldTryPrefillFallback(message, state.PrefillInput))
        {
            pickedCategory = state.AutoConfirmRequested
                ? _serviceMatchingService.PickServiceCategoryFromBookingUtterance(categoryOptions, state.PrefillInput!)
                : _serviceMatchingService.PickServiceCategory(categoryOptions, state.PrefillInput!);
        }

        var categoryChangedInCurrentTurn = false;
        if (pickedCategory is not null && state.ServiceCategoryId != pickedCategory.CategoryId)
        {
            state.ServiceCategoryId = pickedCategory.CategoryId;
            state.ServiceIds.Clear();
            categoryChangedInCurrentTurn = true;
        }

        if (!state.ServiceCategoryId.HasValue && categoryOptions.Count == 1)
        {
            state.ServiceCategoryId = categoryOptions[0].CategoryId;
        }

        if (!state.ServiceCategoryId.HasValue)
        {
            var prompt = new StringBuilder();
            prompt.AppendLine("Bước 2/5 - Chọn nhóm dịch vụ.");
            prompt.AppendLine("Vui lòng chọn nhóm dịch vụ bằng số thứ tự, mã nhóm hoặc tên nhóm:");
            prompt.AppendLine(_serviceMatchingService.FormatServiceCategoryOptions(categoryOptions));
            prompt.Append("Ví dụ: `nhóm 1` hoặc `bảo dưỡng`.");
            return ChatbotBookingResponseBuilder.Build(root, state, _clock.UtcNow, true, prompt.ToString(), serviceCategoryOptions: categoryOptions);
        }

        var serviceRows = await _store.GetActiveServicesByCategoryAsync(state.ServiceCategoryId.Value, ct);
        if (serviceRows.Count == 0)
        {
            state.ServiceCategoryId = null;
            state.ServiceIds.Clear();
            return ChatbotBookingResponseBuilder.Build(root, state, _clock.UtcNow, true, "Nhóm dịch vụ vừa chọn hiện không có dịch vụ active. Vui lòng chọn nhóm khác.");
        }

        var options = serviceRows
            .Select(row => new ChatbotBookingServiceOptionDto
            {
                ServiceId = row.ServiceId,
                ServiceCode = row.ServiceCode ?? string.Empty,
                ServiceName = row.ServiceName
            })
            .ToList();

        var canUseCurrentMessageForServicePick = !currentMessageIsVehicleSelection
            && (!categoryChangedInCurrentTurn || state.AutoConfirmRequested || _serviceMatchingService.MentionsSpecificService(message, options));
        var canUsePrefillForServicePick = ChatbotBookingIntentHelper.ShouldTryPrefillFallback(message, state.PrefillInput)
            && _serviceMatchingService.MentionsSpecificService(state.PrefillInput, options);
        if (canUseCurrentMessageForServicePick || canUsePrefillForServicePick)
        {
            var beforeCount = state.ServiceIds.Count;
            if (canUseCurrentMessageForServicePick)
            {
                state.ServiceIds = _serviceMatchingService.PickServices(
                    options,
                    message,
                    state.ServiceIds,
                    allowNumericSelection: !state.AutoConfirmRequested || _serviceMatchingService.HasServiceOrdinalSelectionSignal(message));
            }

            if (state.ServiceIds.Count == beforeCount && canUsePrefillForServicePick)
            {
                state.ServiceIds = _serviceMatchingService.PickServices(
                    options,
                    state.PrefillInput!,
                    state.ServiceIds,
                    allowNumericSelection: _serviceMatchingService.HasServiceOrdinalSelectionSignal(state.PrefillInput));
            }
        }

        if (state.ServiceIds.Count == 0)
        {
            var categoryName = categoryOptions.FirstOrDefault(item => item.CategoryId == state.ServiceCategoryId.Value)?.CategoryName ?? "Nhóm đã chọn";
            var prompt = new StringBuilder();
            prompt.AppendLine($"Bước 3/5 - Chọn dịch vụ chi tiết trong nhóm `{categoryName}`.");
            prompt.AppendLine("Vui lòng chọn 1 hoặc nhiều dịch vụ bằng số thứ tự, serviceId, mã dịch vụ hoặc tên dịch vụ:");
            prompt.AppendLine(_serviceMatchingService.FormatServiceOptions(serviceRows));
            prompt.Append("Ví dụ: `dịch vụ 1` hoặc `SVC11`.");
            return ChatbotBookingResponseBuilder.Build(root, state, _clock.UtcNow, true, prompt.ToString(), serviceOptions: options);
        }

        var selectedServiceNames = serviceRows
            .Where(row => state.ServiceIds.Contains(row.ServiceId))
            .Select(row => row.ServiceName)
            .Distinct()
            .ToList();
        state.Step = ChatbotBookingStep.SelectBranch;
        var branchOptions = await _vehicleBranchService.LoadAvailableBranchOptionsAsync(ct);
        if (branchOptions.Count == 0)
        {
            return ChatbotBookingResponseBuilder.Build(root, state, _clock.UtcNow, false, "Đã chọn dịch vụ, nhưng hiện không có chi nhánh ACTIVE để đặt lịch.");
        }

        if (state.AutoConfirmRequested)
        {
            return await StepBranchAsync(customer, root, state, sessionBranchId, message, ct);
        }

        return ChatbotBookingResponseBuilder.Build(
            root,
            state,
            _clock.UtcNow,
            true,
            $"Đã chọn dịch vụ: {string.Join(", ", selectedServiceNames)}. Bước 4/5 - Vui lòng chọn chi nhánh đặt lịch.",
            serviceOptions: options,
            branchOptions: branchOptions);
    }

    public async Task<ChatbotBookingWorkflowResponseDto> StepBranchAsync(
        Customer customer,
        JsonObject root,
        ChatbotBookingState state,
        long? sessionBranchId,
        string message,
        CancellationToken ct = default)
    {
        if (state.ServiceIds.Count == 0)
        {
            throw new InvalidOperationException("Services are required before branch.");
        }

        var options = await _vehicleBranchService.LoadAvailableBranchOptionsAsync(ct);
        if (options.Count == 0)
        {
            return ChatbotBookingResponseBuilder.Build(root, state, _clock.UtcNow, false, "Hiện không có chi nhánh ACTIVE để đặt lịch.");
        }

        if (!state.BranchId.HasValue)
        {
            var pickedBranch = state.AutoConfirmRequested
                ? _vehicleBranchService.PickBranchFromBookingUtterance(options, message)
                : _vehicleBranchService.PickBranch(options, message);
            if (pickedBranch is null && ChatbotBookingIntentHelper.ShouldTryPrefillFallback(message, state.PrefillInput))
            {
                pickedBranch = state.AutoConfirmRequested
                    ? _vehicleBranchService.PickBranchFromBookingUtterance(options, state.PrefillInput!)
                    : _vehicleBranchService.PickBranch(options, state.PrefillInput!);
            }

            state.BranchId = pickedBranch?.BranchId;
        }

        if (!state.BranchId.HasValue
            && !state.AutoConfirmRequested
            && sessionBranchId.HasValue
            && options.Any(item => item.BranchId == sessionBranchId.Value))
        {
            state.BranchId = sessionBranchId.Value;
        }

        if (!state.BranchId.HasValue)
        {
            if (options.Count == 1)
            {
                state.BranchId = options[0].BranchId;
            }
            else
            {
                var prompt = new StringBuilder();
                prompt.AppendLine("Bước 4/5 - Chọn chi nhánh.");
                prompt.AppendLine("Vui lòng chọn chi nhánh theo tên chi nhánh hoặc branchId:");
                prompt.AppendLine(_vehicleBranchService.FormatBranchOptions(options));
                prompt.Append("Ví dụ: `Hà Nội`, `Đà Nẵng` hoặc `TP HCM`.");
                return ChatbotBookingResponseBuilder.Build(root, state, _clock.UtcNow, true, prompt.ToString(), branchOptions: options);
            }
        }

        var parsedDate = _slotResolutionService.ParseDate(message);
        if (!parsedDate.HasValue && ChatbotBookingIntentHelper.ShouldTryPrefillFallback(message, state.PrefillInput))
        {
            parsedDate = _slotResolutionService.ParseDate(state.PrefillInput!);
        }

        if (parsedDate.HasValue)
        {
            state.BookingDate = parsedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        state.Step = ChatbotBookingStep.ComputeAvailableSlots;
        return await StepSlotsAsync(customer, root, state, message, ct);
    }

    public async Task<ChatbotBookingWorkflowResponseDto> StepSlotsAsync(
        Customer customer,
        JsonObject root,
        ChatbotBookingState state,
        string message,
        CancellationToken ct = default)
    {
        if (!state.BranchId.HasValue)
        {
            throw new InvalidOperationException("Branch is required before slots.");
        }

        var parsed = _slotResolutionService.ParseDate(message);
        if (!parsed.HasValue && ChatbotBookingIntentHelper.ShouldTryPrefillFallback(message, state.PrefillInput))
        {
            parsed = _slotResolutionService.ParseDate(state.PrefillInput!);
        }

        if (parsed.HasValue)
        {
            if (_slotResolutionService.TryParseBookingDate(state.BookingDate, out var previousDate) && previousDate != parsed.Value)
            {
                await ClearSlotSelectionAsync(state, ct);
            }

            state.BookingDate = parsed.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (!_slotResolutionService.TryParseBookingDate(state.BookingDate, out var date))
        {
            return ChatbotBookingResponseBuilder.Build(
                root,
                state,
                _clock.UtcNow,
                true,
                "Bước 5/5 - Chọn ngày và khung giờ. Vui lòng nhập ngày theo `yyyy-MM-dd`, `dd/MM/yyyy`, `hôm nay` hoặc `ngày mai`.");
        }

        if (!await _slotResolutionService.IsBookableDayAsync(state.BranchId.Value, date, ct))
        {
            await ClearSlotSelectionAsync(state, ct);
            return ChatbotBookingResponseBuilder.Build(root, state, _clock.UtcNow, true, "Chi nhánh đóng cửa vào ngày bạn chọn. Vui lòng thử ngày khác.");
        }

        var preferredTime = _slotResolutionService.ParsePreferredSlotTime(message)
            ?? (ChatbotBookingIntentHelper.ShouldTryPrefillFallback(message, state.PrefillInput)
                ? _slotResolutionService.ParsePreferredSlotTime(state.PrefillInput)
                : null);
        var options = await _slotResolutionService.LoadAvailableSlotsAsync(state.BranchId.Value, state.ServiceIds, date, preferredTime, ct);
        if (options.Count == 0)
        {
            await ClearSlotSelectionAsync(state, ct);
            return ChatbotBookingResponseBuilder.Build(root, state, _clock.UtcNow, true, "Không còn khung giờ trống trong ngày này. Vui lòng chọn ngày khác.");
        }

        var picked = _slotResolutionService.PickSlot(options, message);
        if (picked is null && ChatbotBookingIntentHelper.ShouldTryPrefillFallback(message, state.PrefillInput))
        {
            picked = _slotResolutionService.PickSlot(options, state.PrefillInput!);
        }

        if (picked is null)
        {
            var prompt = new StringBuilder();
            prompt.AppendLine($"Bước 5/5 - Chọn khung giờ cho ngày {date:dd/MM/yyyy}.");
            prompt.AppendLine("Các khung giờ còn trống:");
            prompt.AppendLine(_slotResolutionService.FormatSlotOptions(options));
            prompt.Append("Trả lời: `slot 1`, `17h30` hoặc nhập đúng datetime của khung giờ.");
            return ChatbotBookingResponseBuilder.Build(
                root,
                state,
                _clock.UtcNow,
                true,
                prompt.ToString(),
                slotOptions: options,
                noShowWarning: ChatbotBookingResponseBuilder.Warning(customer.NoShowCount));
        }

        await ClearSlotSelectionAsync(state, ct);
        SlotHoldResponseDto hold;
        try
        {
            hold = await _slotResolutionService.CreateSlotHoldAsync(state.BranchId.Value, state.ServiceIds, picked.StartTime, picked.EndTime);
        }
        catch (InvalidOperationException)
        {
            return ChatbotBookingResponseBuilder.Build(
                root,
                state,
                _clock.UtcNow,
                true,
                "Khung giờ vừa chọn đã không còn khả dụng. Vui lòng chọn khung khác.",
                slotOptions: options,
                noShowWarning: ChatbotBookingResponseBuilder.Warning(customer.NoShowCount));
        }

        state.SelectedSlotStartUtc = picked.StartTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        state.SelectedSlotEndUtc = picked.EndTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        state.SlotHoldToken = hold.HoldToken;
        state.SlotHoldExpiresAtUtc = hold.ExpiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        state.Step = ChatbotBookingStep.FinalizeBooking;

        var voucherSuggestion = await _bookingExecutionService.ResolveVoucherSuggestionAsync(customer, state.BranchId, state.VehicleId, state.ServiceIds, ct);
        var summary = await _bookingExecutionService.BuildBookingConfirmationSummaryAsync(
            state.VehicleId,
            state.BranchId,
            state.ServiceIds,
            picked.StartTime,
            picked.EndTime,
            state.SlotHoldExpiresAtUtc,
            ct);
        return ChatbotBookingResponseBuilder.Build(
            root,
            state,
            _clock.UtcNow,
            true,
            $"{summary}{Environment.NewLine}Vui lòng trả lời `đồng ý` hoặc `xác nhận` để chốt lịch, hoặc `đổi ngày`/`quay lại` để chỉnh sửa.",
            slotOptions: options,
            noShowWarning: ChatbotBookingResponseBuilder.Warning(customer.NoShowCount),
            voucherSuggestion: voucherSuggestion);
    }

    public async Task ClearSlotSelectionAsync(ChatbotBookingState state, CancellationToken ct = default)
    {
        await _slotResolutionService.ReleaseSlotHoldAsync(state.SlotHoldToken);
        state.ClearSlotSelection();
    }
}
