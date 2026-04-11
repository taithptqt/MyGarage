using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public class ChatbotBookingWorkflowService : IChatbotBookingWorkflowService
{
    private static readonly Regex NumberRegex = new(@"\d+", RegexOptions.Compiled);
    private static readonly Regex PlateRegex = new(@"\b[A-Z0-9\-]{5,12}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ModelRegex = new(@"model(?:id)?\s*[:=]?\s*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex VinRegex = new(@"vin\s*[:=]?\s*([A-Z0-9]{8,20})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TimeHintRegex = new(@"\b(?<hour>[01]?\d|2[0-3])\s*(?:h|gio)\s*(?<minute>[0-5]?\d)?\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly string[] ClosedHints = ["TEMP", "RENOV", "CLOSED", "SUSPEND"];

    private readonly IGarageContext _context;
    private readonly IAppointmentService _appointmentService;
    private readonly IChatbotVoucherResolverService _voucherResolver;
    private readonly ISystemClock _clock;
    private readonly ILogger<ChatbotBookingWorkflowService> _logger;

    public ChatbotBookingWorkflowService(
        IGarageContext context,
        IAppointmentService appointmentService,
        IChatbotVoucherResolverService voucherResolver,
        ISystemClock clock,
        ILogger<ChatbotBookingWorkflowService> logger)
    {
        _context = context;
        _appointmentService = appointmentService;
        _voucherResolver = voucherResolver;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ChatbotBookingWorkflowResponseDto> RunAsync(long customerId, long sessionId, long? sessionBranchId, string intentStateJson, string userMessage, CancellationToken cancellationToken = default)
    {
        if (customerId <= 0) throw new InvalidOperationException("Authenticated CustomerId is required.");
        var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId && c.IsDeleted != true, cancellationToken)
            ?? throw new KeyNotFoundException("Customer not found.");

        var root = ParseRoot(intentStateJson);
        var state = BookingState.From(intentStateJson);
        var msg = userMessage?.Trim() ?? string.Empty;
        if (state.Step == BookingStep.Completed)
        {
            state = new BookingState();
        }

        if (state.Step != BookingStep.IdentifyOrCreateVehicle && IsRestartBookingRequest(msg))
        {
            await ClearSlotSelectionAsync(state, cancellationToken);
            state = new BookingState();
            await LogAction(
                sessionId,
                ChatbotActionStatus.SKIPPED,
                "BOOKING_FUNNEL_RESTART",
                "USER_RESTARTED",
                "Booking flow restarted by explicit user request.",
                null,
                cancellationToken);
        }

        if (IsDirectBookingRequest(msg))
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
            await LogFunnelProgressAsync(sessionId, state, commandResult, msg, cancellationToken);
            return commandResult;
        }

        var response = state.Step switch
        {
            BookingStep.IdentifyOrCreateVehicle => await StepVehicle(customer, sessionId, sessionBranchId, root, state, msg, cancellationToken),
            BookingStep.SelectServices => await StepServices(customer, sessionId, sessionBranchId, root, state, msg, cancellationToken),
            BookingStep.SelectBranch => await StepBranch(customer, sessionId, root, state, sessionBranchId, msg, cancellationToken),
            BookingStep.ComputeAvailableSlots => await StepSlots(customer, sessionId, root, state, msg, cancellationToken),
            BookingStep.FinalizeBooking => await StepFinalize(customer, sessionId, root, state, msg, cancellationToken),
            _ => Build(root, state.Reset(), true, "Luồng đặt lịch đã được đặt lại.")
        };

        await LogFunnelProgressAsync(sessionId, state, response, msg, cancellationToken);
        return response;
    }

    private async Task<ChatbotBookingWorkflowResponseDto> StepVehicle(Customer customer, long sessionId, long? sessionBranchId, JsonObject root, BookingState state, string message, CancellationToken ct)
    {
        var options = await _context.CustomerVehicles.AsNoTracking()
            .Where(cv => cv.CustomerId == customer.Id && cv.IsDeleted != true && cv.IsCurrentOwner != false)
            .Select(cv => new ChatbotBookingVehicleOptionDto
            {
                VehicleId = cv.VehicleId,
                LicensePlate = cv.Vehicle.LicensePlate,
                VehicleModelName = string.Concat(cv.Vehicle.Model.Brand.Name, " ", cv.Vehicle.Model.ModelName).Trim()
            })
            .OrderBy(x => x.LicensePlate)
            .ToListAsync(ct);

        if (options.Count == 1)
        {
            state.VehicleId = options[0].VehicleId;
            state.Step = BookingStep.SelectServices;
            return await StepServices(customer, sessionId, sessionBranchId, root, state, message, ct);
        }

        if (options.Count > 1)
        {
            var picked = state.AutoConfirmRequested
                ? PickVehicleFromBookingUtterance(options, message)
                : PickVehicle(options, message);
            if (picked is null && ShouldTryPrefillFallback(message, state.PrefillInput))
            {
                picked = state.AutoConfirmRequested
                    ? PickVehicleFromBookingUtterance(options, state.PrefillInput!)
                    : PickVehicle(options, state.PrefillInput!);
            }
            if (picked is null)
            {
                var prompt = new StringBuilder();
                prompt.AppendLine("Bước 1/5 - Chọn xe.");
                prompt.AppendLine("Bạn đang có nhiều xe. Vui lòng chọn 1 xe bằng số thứ tự, biển số hoặc tên xe:");
                prompt.AppendLine(FormatVehicleOptions(options));
                prompt.Append("Ví dụ: `xe 2` hoặc `51A-12345`.");
                return Build(root, state, true, prompt.ToString(), vehicleOptions: options);
            }

            state.VehicleId = picked.VehicleId;
            state.Step = BookingStep.SelectServices;
            return await StepServices(customer, sessionId, sessionBranchId, root, state, message, ct);
        }

        var created = await TryCreateVehicle(customer.Id, message, ct);
        if (created is null)
        {
            return Build(
                root,
                state,
                true,
                "Bước 1/5 - Chưa tìm thấy xe trong tài khoản. Bạn vui lòng gửi thông tin xe theo mẫu: `biển số + hãng/dòng xe + năm` (ví dụ: `51A-12345 Toyota Vios 2020`).");
        }

        state.VehicleId = created.Id;
        state.Step = BookingStep.SelectServices;
        return await StepServices(customer, sessionId, sessionBranchId, root, state, message, ct);
    }

    private async Task<ChatbotBookingWorkflowResponseDto?> HandleNavigationCommandAsync(
        Customer customer,
        long sessionId,
        long? sessionBranchId,
        JsonObject root,
        BookingState state,
        string message,
        CancellationToken ct)
    {
        var command = ResolveNavigationCommand(message);
        if (command == BookingNavigationCommand.None)
        {
            return null;
        }

        switch (command)
        {
            case BookingNavigationCommand.Cancel:
                await ClearSlotSelectionAsync(state, ct);
                state = state.Reset();
                await LogAction(sessionId, ChatbotActionStatus.SKIPPED, "BOOKING_FUNNEL_EXIT", "USER_CANCELLED", "Booking flow cancelled by user command.", null, ct);
                return Build(root, state, true, "Đã hủy luồng đặt lịch. Nếu bạn muốn đặt lịch lại, vui lòng nhập `đặt lịch`.");

            case BookingNavigationCommand.Restart:
                await ClearSlotSelectionAsync(state, ct);
                state = state.Reset();
                await LogAction(sessionId, ChatbotActionStatus.SKIPPED, "BOOKING_FUNNEL_RESTART", "USER_RESTARTED", "Booking flow restarted by user command.", null, ct);
                return Build(root, state, true, "Đã bắt đầu lại luồng đặt lịch. Bước 1/5 - Vui lòng chọn xe.");

            case BookingNavigationCommand.ChangeVehicle:
                await ClearSlotSelectionAsync(state, ct);
                state.PrefillInput = null;
                state.VehicleId = null;
                state.ServiceCategoryId = null;
                state.ServiceIds.Clear();
                state.BranchId = null;
                state.BookingDate = null;
                state.Step = BookingStep.IdentifyOrCreateVehicle;
                return await StepVehicle(customer, sessionId, sessionBranchId, root, state, string.Empty, ct);

            case BookingNavigationCommand.ChangeService:
                await ClearSlotSelectionAsync(state, ct);
                state.PrefillInput = null;
                state.ServiceCategoryId = null;
                state.ServiceIds.Clear();
                state.BranchId = null;
                state.BookingDate = null;
                state.Step = BookingStep.SelectServices;
                return await StepServices(customer, sessionId, sessionBranchId, root, state, string.Empty, ct);

            case BookingNavigationCommand.ChangeDate:
                await ClearSlotSelectionAsync(state, ct);
                state.PrefillInput = null;
                state.BookingDate = null;
                if (!state.BranchId.HasValue)
                {
                    state.Step = BookingStep.SelectBranch;
                    return await StepBranch(customer, sessionId, root, state, sessionBranchId, string.Empty, ct);
                }

                state.Step = BookingStep.ComputeAvailableSlots;
                return await StepSlots(customer, sessionId, root, state, string.Empty, ct);

            case BookingNavigationCommand.Back:
                return await HandleBackCommandAsync(customer, sessionId, sessionBranchId, root, state, ct);
        }

        return null;
    }

    private async Task<ChatbotBookingWorkflowResponseDto> HandleBackCommandAsync(
        Customer customer,
        long sessionId,
        long? sessionBranchId,
        JsonObject root,
        BookingState state,
        CancellationToken ct)
    {
        switch (state.Step)
        {
            case BookingStep.IdentifyOrCreateVehicle:
                return await StepVehicle(customer, sessionId, sessionBranchId, root, state, string.Empty, ct);

            case BookingStep.SelectServices:
                await ClearSlotSelectionAsync(state, ct);
                state.VehicleId = null;
                state.ServiceCategoryId = null;
                state.ServiceIds.Clear();
                state.BranchId = null;
                state.BookingDate = null;
                state.Step = BookingStep.IdentifyOrCreateVehicle;
                return await StepVehicle(customer, sessionId, sessionBranchId, root, state, string.Empty, ct);

            case BookingStep.SelectBranch:
                await ClearSlotSelectionAsync(state, ct);
                state.ServiceCategoryId = null;
                state.ServiceIds.Clear();
                state.BranchId = null;
                state.BookingDate = null;
                state.Step = BookingStep.SelectServices;
                return await StepServices(customer, sessionId, sessionBranchId, root, state, string.Empty, ct);

            case BookingStep.ComputeAvailableSlots:
                await ClearSlotSelectionAsync(state, ct);
                state.BranchId = null;
                state.BookingDate = null;
                state.Step = BookingStep.SelectBranch;
                return await StepBranch(customer, sessionId, root, state, sessionBranchId, string.Empty, ct);

            case BookingStep.FinalizeBooking:
                await ClearSlotSelectionAsync(state, ct);
                state.Step = BookingStep.ComputeAvailableSlots;
                return await StepSlots(customer, sessionId, root, state, string.Empty, ct);

            case BookingStep.Completed:
                await ClearSlotSelectionAsync(state, ct);
                state = state.Reset();
                return await StepVehicle(customer, sessionId, sessionBranchId, root, state, string.Empty, ct);

            default:
                return await StepVehicle(customer, sessionId, sessionBranchId, root, state, string.Empty, ct);
        }
    }

    private async Task<ChatbotBookingWorkflowResponseDto> StepServices(Customer customer, long sessionId, long? sessionBranchId, JsonObject root, BookingState state, string message, CancellationToken ct)
    {
        if (!state.VehicleId.HasValue)
        {
            throw new InvalidOperationException("Vehicle is required before services.");
        }

        var categoryRows = await _context.StandardServices
            .AsNoTracking()
            .Where(service => service.IsDeleted != true && service.IsActive == true && service.Category.IsDeleted != true)
            .Select(service => new
            {
                service.CategoryId,
                CategoryName = service.Category.Name
            })
            .Distinct()
            .OrderBy(item => item.CategoryName)
            .ToListAsync(ct);

        var categories = categoryRows
            .Select(item => new ServiceCategoryOption
            {
                CategoryId = item.CategoryId,
                CategoryName = item.CategoryName
            })
            .ToList();
        var categoryOptions = categories
            .Select(item => new ChatbotBookingServiceCategoryOptionDto
            {
                CategoryId = item.CategoryId,
                CategoryName = item.CategoryName
            })
            .ToList();

        if (categories.Count == 0)
        {
            return Build(root, state, false, "Hệ thống chưa có dịch vụ active để đặt lịch.");
        }

        if (state.ServiceCategoryId.HasValue && categories.All(item => item.CategoryId != state.ServiceCategoryId.Value))
        {
            state.ServiceCategoryId = null;
            state.ServiceIds.Clear();
        }

        var currentMessageIsVehicleSelection = state.AutoConfirmRequested && LooksLikeVehicleSelectionReply(message);
        var canSelectCategoryFromCurrentMessage = !currentMessageIsVehicleSelection
            && (!state.ServiceCategoryId.HasValue || HasServiceCategorySelectionSignal(message, categories));
        var pickedCategory = canSelectCategoryFromCurrentMessage
            ? state.AutoConfirmRequested
                ? PickServiceCategoryFromBookingUtterance(categories, message)
                : PickServiceCategory(categories, message)
            : null;
        if (pickedCategory is null
            && !state.ServiceCategoryId.HasValue
            && ShouldTryPrefillFallback(message, state.PrefillInput))
        {
            pickedCategory = state.AutoConfirmRequested
                ? PickServiceCategoryFromBookingUtterance(categories, state.PrefillInput!)
                : PickServiceCategory(categories, state.PrefillInput!);
        }
        var categoryChangedInCurrentTurn = false;
        if (pickedCategory is not null && state.ServiceCategoryId != pickedCategory.CategoryId)
        {
            state.ServiceCategoryId = pickedCategory.CategoryId;
            state.ServiceIds.Clear();
            categoryChangedInCurrentTurn = true;
        }

        if (!state.ServiceCategoryId.HasValue && categories.Count == 1)
        {
            state.ServiceCategoryId = categories[0].CategoryId;
        }

        if (!state.ServiceCategoryId.HasValue)
        {
            var prompt = new StringBuilder();
            prompt.AppendLine("Bước 2/5 - Chọn nhóm dịch vụ.");
            prompt.AppendLine("Vui lòng chọn nhóm dịch vụ bằng số thứ tự, mã nhóm hoặc tên nhóm:");
            prompt.AppendLine(FormatServiceCategoryOptions(categories));
            prompt.Append("Ví dụ: `nhóm 1` hoặc `bảo dưỡng`.");
            return Build(root, state, true, prompt.ToString(), serviceCategoryOptions: categoryOptions);
        }

        var serviceRows = await _context.StandardServices
            .AsNoTracking()
            .Where(service =>
                service.IsDeleted != true
                && service.IsActive == true
                && service.CategoryId == state.ServiceCategoryId.Value)
            .OrderBy(service => service.Name)
            .Take(80)
            .Select(service => new ServiceOptionRow
            {
                ServiceId = service.Id,
                ServiceCode = service.Code,
                ServiceName = service.Name,
                BasePrice = service.BasePrice,
                StandardMinutes = Math.Max(15, service.StandardMinutes)
            })
            .ToListAsync(ct);

        if (serviceRows.Count == 0)
        {
            state.ServiceCategoryId = null;
            state.ServiceIds.Clear();
            return Build(root, state, true, "Nhóm dịch vụ vừa chọn hiện không có dịch vụ active. Vui lòng chọn nhóm khác.");
        }

        var options = serviceRows
            .Select(row => new ChatbotBookingServiceOptionDto
            {
                ServiceId = row.ServiceId,
                ServiceCode = row.ServiceCode,
                ServiceName = row.ServiceName
            })
            .ToList();

        var canUseCurrentMessageForServicePick = !currentMessageIsVehicleSelection
            && (!categoryChangedInCurrentTurn || MentionsSpecificService(message, options));
        var canUsePrefillForServicePick = ShouldTryPrefillFallback(message, state.PrefillInput)
            && MentionsSpecificService(state.PrefillInput, options);
        if (canUseCurrentMessageForServicePick || canUsePrefillForServicePick)
        {
            var beforeCount = state.ServiceIds.Count;
            if (canUseCurrentMessageForServicePick)
            {
                state.ServiceIds = PickServices(
                    options,
                    message,
                    state.ServiceIds,
                    allowNumericSelection: !state.AutoConfirmRequested || HasServiceOrdinalSelectionSignal(message));
            }

            if (state.ServiceIds.Count == beforeCount && canUsePrefillForServicePick)
            {
                state.ServiceIds = PickServices(
                    options,
                    state.PrefillInput!,
                    state.ServiceIds,
                    allowNumericSelection: HasServiceOrdinalSelectionSignal(state.PrefillInput));
            }
        }
        if (state.ServiceIds.Count == 0)
        {
            var categoryName = categories.FirstOrDefault(item => item.CategoryId == state.ServiceCategoryId.Value)?.CategoryName ?? "Nhóm đã chọn";
            var prompt = new StringBuilder();
            prompt.AppendLine($"Bước 3/5 - Chọn dịch vụ chi tiết trong nhóm `{categoryName}`.");
            prompt.AppendLine("Vui lòng chọn 1 hoặc nhiều dịch vụ bằng số thứ tự, serviceId, mã dịch vụ hoặc tên dịch vụ:");
            prompt.AppendLine(FormatServiceOptions(serviceRows));
            prompt.Append("Ví dụ: `dịch vụ 1` hoặc `SVC11`.");
            return Build(root, state, true, prompt.ToString(), serviceOptions: options);
        }

        var selectedServiceNames = serviceRows
            .Where(row => state.ServiceIds.Contains(row.ServiceId))
            .Select(row => row.ServiceName)
            .Distinct()
            .ToList();
        state.Step = BookingStep.SelectBranch;
        var branchOptions = await LoadAvailableBranchOptionsAsync(ct);
        if (branchOptions.Count == 0)
        {
            return Build(root, state, false, "Đã chọn dịch vụ, nhưng hiện không có chi nhánh ACTIVE để đặt lịch.");
        }

        if (state.AutoConfirmRequested)
        {
            return await StepBranch(customer, sessionId, root, state, sessionBranchId, message, ct);
        }

        return Build(
            root,
            state,
            true,
            $"Đã chọn dịch vụ: {string.Join(", ", selectedServiceNames)}. Bước 4/5 - Vui lòng chọn chi nhánh đặt lịch.",
            serviceOptions: options,
            branchOptions: branchOptions);
    }

    private async Task<ChatbotBookingWorkflowResponseDto> StepBranch(Customer customer, long sessionId, JsonObject root, BookingState state, long? sessionBranchId, string message, CancellationToken ct)
    {
        if (state.ServiceIds.Count == 0)
        {
            throw new InvalidOperationException("Services are required before branch.");
        }

        var options = await LoadAvailableBranchOptionsAsync(ct);

        if (options.Count == 0)
        {
            return Build(root, state, false, "Hiện không có chi nhánh ACTIVE để đặt lịch.");
        }

        if (!state.BranchId.HasValue)
        {
            var pickedBranch = state.AutoConfirmRequested
                ? PickBranchFromBookingUtterance(options, message)
                : PickBranch(options, message);
            if (pickedBranch is null && ShouldTryPrefillFallback(message, state.PrefillInput))
            {
                pickedBranch = state.AutoConfirmRequested
                    ? PickBranchFromBookingUtterance(options, state.PrefillInput!)
                    : PickBranch(options, state.PrefillInput!);
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
                prompt.AppendLine("Vui lòng chọn chi nhánh theo số thứ tự, branchId hoặc tên chi nhánh:");
                prompt.AppendLine(FormatBranchOptions(options));
                prompt.Append("Ví dụ: `chi nhánh 1` hoặc tên chi nhánh.");
                return Build(root, state, true, prompt.ToString(), branchOptions: options);
            }
        }

        var parsedDate = ParseDate(message);
        if (!parsedDate.HasValue && ShouldTryPrefillFallback(message, state.PrefillInput))
        {
            parsedDate = ParseDate(state.PrefillInput!);
        }
        if (parsedDate.HasValue)
        {
            state.BookingDate = parsedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        state.Step = BookingStep.ComputeAvailableSlots;
        return await StepSlots(customer, sessionId, root, state, message, ct);
    }

    private async Task<List<ChatbotBookingBranchOptionDto>> LoadAvailableBranchOptionsAsync(CancellationToken ct)
    {
        var branches = await _context.Branches
            .AsNoTracking()
            .Where(branch => branch.IsDeleted != true)
            .OrderBy(branch => branch.Name)
            .Select(branch => new BranchRow
            {
                Id = branch.Id,
                Name = branch.Name,
                Status = branch.Status ?? string.Empty,
                Description = branch.Description ?? string.Empty,
                Address = branch.Address,
                Hotline = branch.Hotline
            })
            .ToListAsync(ct);

        return branches
            .Where(IsAvailableBranch)
            .Select(branch => new ChatbotBookingBranchOptionDto
            {
                BranchId = branch.Id,
                BranchName = branch.Name,
                Address = branch.Address,
                Hotline = branch.Hotline
            })
            .ToList();
    }

    private async Task<ChatbotBookingWorkflowResponseDto> StepSlots(Customer customer, long sessionId, JsonObject root, BookingState state, string message, CancellationToken ct)
    {
        if (!state.BranchId.HasValue)
        {
            throw new InvalidOperationException("Branch is required before slots.");
        }

        var parsed = ParseDate(message);
        if (!parsed.HasValue && ShouldTryPrefillFallback(message, state.PrefillInput))
        {
            parsed = ParseDate(state.PrefillInput!);
        }
        if (parsed.HasValue)
        {
            if (TryDate(state.BookingDate, out var previousDate) && previousDate != parsed.Value)
            {
                await ClearSlotSelectionAsync(state, ct);
            }

            state.BookingDate = parsed.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (!TryDate(state.BookingDate, out var date))
        {
            return Build(
                root,
                state,
                true,
                "Bước 5/5 - Chọn ngày và khung giờ. Vui lòng nhập ngày theo `yyyy-MM-dd`, `dd/MM/yyyy`, `hôm nay` hoặc `ngày mai`.");
        }

        if (!await IsBookableDay(state.BranchId.Value, date, ct))
        {
            await ClearSlotSelectionAsync(state, ct);
            return Build(root, state, true, "Chi nhánh đóng cửa vào ngày bạn chọn. Vui lòng thử ngày khác.");
        }

        var options = await LoadSlots(state, date);
        if (options.Count == 0)
        {
            await ClearSlotSelectionAsync(state, ct);
            return Build(root, state, true, "Không còn khung giờ trống trong ngày này. Vui lòng chọn ngày khác.");
        }

        var picked = PickSlot(options, message);
        if (picked is null && ShouldTryPrefillFallback(message, state.PrefillInput))
        {
            picked = PickSlot(options, state.PrefillInput!);
        }

        if (picked is null)
        {
            var prompt = new StringBuilder();
            prompt.AppendLine($"Bước 5/5 - Chọn khung giờ cho ngày {date:dd/MM/yyyy}.");
            prompt.AppendLine("Các khung giờ còn trống:");
            prompt.AppendLine(FormatSlotOptions(options));
            prompt.Append("Trả lời: `slot 1`, `17h30` hoặc nhập đúng datetime của khung giờ.");
            return Build(root, state, true, prompt.ToString(), slotOptions: options, noShowWarning: Warning(customer.NoShowCount));
        }

        await ClearSlotSelectionAsync(state, ct);
        SlotHoldResponseDto hold;
        try
        {
            hold = await _appointmentService.CreateSlotHoldAsync(new SlotHoldRequestDto
            {
                BranchId = state.BranchId.Value,
                StartTime = picked.StartTime,
                EndTime = picked.EndTime,
                BookingFlow = "FIXED",
                RequestedServiceIds = string.Join(",", state.ServiceIds),
                VehicleCount = 1
            });
        }
        catch (InvalidOperationException)
        {
            return Build(root, state, true, "Khung giờ vừa chọn đã không còn khả dụng. Vui lòng chọn khung khác.", slotOptions: options, noShowWarning: Warning(customer.NoShowCount));
        }

        state.SelectedSlotStartUtc = picked.StartTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        state.SelectedSlotEndUtc = picked.EndTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        state.SlotHoldToken = hold.HoldToken;
        state.SlotHoldExpiresAtUtc = hold.ExpiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        state.Step = BookingStep.FinalizeBooking;

        var voucherSuggestion = await ResolveVoucherSuggestionAsync(customer, state, ct);
        var summary = await BuildBookingConfirmationSummaryAsync(state, picked.StartTime, picked.EndTime, ct);
        return Build(
            root,
            state,
            true,
            $"{summary}{Environment.NewLine}Vui lòng trả lời `đồng ý` hoặc `xác nhận` để chốt lịch, hoặc `đổi ngày`/`quay lại` để chỉnh sửa.",
            slotOptions: options,
            noShowWarning: Warning(customer.NoShowCount),
            voucherSuggestion: voucherSuggestion);
    }

    private async Task<ChatbotBookingWorkflowResponseDto> StepFinalize(Customer customer, long sessionId, JsonObject root, BookingState state, string message, CancellationToken ct)
    {
        if (!Confirm(message))
        {
            return Build(root, state, true, "Để xác nhận đặt lịch, bạn trả lời `đồng ý`, `xác nhận` hoặc `yes`.");
        }

        var hasStart = DateTime.TryParse(state.SelectedSlotStartUtc, null, DateTimeStyles.RoundtripKind, out var start);
        if (hasStart)
        {
            start = NormalizeBookingDateTime(start);
        }

        if (DateTime.TryParse(state.SlotHoldExpiresAtUtc, null, DateTimeStyles.RoundtripKind, out var holdExpiresAt)
            && holdExpiresAt.ToUniversalTime() <= _clock.UtcNow)
        {
            await ClearSlotSelectionAsync(state, ct);
            state.Step = BookingStep.ComputeAvailableSlots;
            return Build(root, state, true, "Giữ chỗ đã hết hạn. Vui lòng chọn lại khung giờ.");
        }

        if (!state.BranchId.HasValue
            || state.ServiceIds.Count == 0
            || !state.VehicleId.HasValue
            || !TryDate(state.BookingDate, out var date)
            || !hasStart)
        {
            state.Step = BookingStep.ComputeAvailableSlots;
            return Build(root, state, true, "Thông tin đặt lịch chưa đầy đủ. Vui lòng chọn lại khung giờ.");
        }

        var slots = await LoadSlots(state, date);
        if (!slots.Any(slot => SameMoment(slot.StartTime, start)))
        {
            state.Step = BookingStep.ComputeAvailableSlots;
            await ClearSlotSelectionAsync(state, ct);
            await LogAction(sessionId, ChatbotActionStatus.FAILED, "CREATE_APPOINTMENT", "SLOT_CONFLICT", "Selected slot no longer available.", null, ct);
            return Build(root, state, true, "Khung giờ vừa chọn đã hết chỗ. Vui lòng chọn khung khác.");
        }

        try
        {
            var voucherSuggestion = await ResolveVoucherSuggestionAsync(customer, state, ct);
            var result = state.ServiceIds.Count > 1
                ? await BookGroup(customer, state, start, ct)
                : await BookSingle(customer, state, start, ct);
            await ClearSlotSelectionAsync(state, ct);
            state.Step = BookingStep.Completed;
            state.PrefillInput = null;
            await LogAction(sessionId, ChatbotActionStatus.SUCCESS, "CREATE_APPOINTMENT", null, null, result, ct);
            await LogVoucherActionAsync(sessionId, voucherSuggestion, ct);
            return Build(
                root,
                state,
                false,
                result.Message,
                bookingResult: result,
                noShowWarning: Warning(customer.NoShowCount),
                voucherSuggestion: voucherSuggestion);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Booking race condition. SessionId={SessionId}, CustomerId={CustomerId}", sessionId, customer.Id);
            state.Step = BookingStep.ComputeAvailableSlots;
            await ClearSlotSelectionAsync(state, ct);
            await LogAction(sessionId, ChatbotActionStatus.FAILED, "CREATE_APPOINTMENT", "BOOKING_RACE_CONDITION", ex.Message, null, ct);
            return Build(root, state, true, BuildFinalizeErrorMessage(ex.Message));
        }
    }

    private async Task<ChatbotBookingResultDto> BookSingle(Customer customer, BookingState state, DateTime start, CancellationToken ct)
    {
        var end = DateTime.TryParse(state.SelectedSlotEndUtc, null, DateTimeStyles.RoundtripKind, out var parsed)
            ? NormalizeBookingDateTime(parsed)
            : start.AddMinutes(60);
        if (end <= start)
        {
            end = start.AddMinutes(60);
        }

        var dto = new BookAppointmentRequestDto
        {
            BranchId = state.BranchId!.Value,
            VehicleId = state.VehicleId,
            StartTime = start,
            EndTime = end,
            BookingFlow = "FIXED",
            RequestedServiceIds = state.ServiceIds[0].ToString(CultureInfo.InvariantCulture),
            CustomerFullName = customer.FullName,
            CustomerPhone = customer.Phone,
            SlotHoldToken = state.SlotHoldToken
        };
        var appointment = await _appointmentService.BookAsync(customer.Id, dto);
        return new ChatbotBookingResultDto { AppointmentId = appointment.Id, Message = $"Đặt lịch thành công. Mã lịch hẹn: {appointment.Id}." };
    }

    private async Task<ChatbotBookingResultDto> BookGroup(Customer customer, BookingState state, DateTime start, CancellationToken ct)
    {
        var durations = await _context.StandardServices.AsNoTracking().Where(s => state.ServiceIds.Contains(s.Id)).Select(s => new { s.Id, Minutes = Math.Max(15, s.StandardMinutes) }).ToListAsync(ct);
        var map = durations.ToDictionary(x => x.Id, x => x.Minutes);
        var cursor = start; var items = new List<BookAppointmentRequestDto>();
        foreach (var serviceId in state.ServiceIds)
        {
            var end = cursor.AddMinutes(map.TryGetValue(serviceId, out var m) ? m : 60);
            items.Add(new BookAppointmentRequestDto
            {
                BranchId = state.BranchId!.Value,
                VehicleId = state.VehicleId,
                StartTime = cursor,
                EndTime = end,
                BookingFlow = "FIXED",
                RequestedServiceIds = serviceId.ToString(CultureInfo.InvariantCulture),
                CustomerFullName = customer.FullName,
                CustomerPhone = customer.Phone,
                SlotHoldToken = items.Count == 0 ? state.SlotHoldToken : null
            });
            cursor = end;
        }
        var batch = new BookAppointmentBatchRequestDto { BranchId = state.BranchId!.Value, CustomerNote = "Created by chatbot sequential booking workflow.", Items = items };
        var group = await _appointmentService.BookBatchForCustomerAsync(customer.Id, customer.Id, batch);
        return new ChatbotBookingResultDto { BookingGroupId = group.Id, Message = $"Đặt lịch nhóm thành công. Mã nhóm lịch hẹn: {group.Id}." };
    }

    private async Task<List<ChatbotBookingSlotOptionDto>> LoadSlots(BookingState state, DateOnly date)
    {
        var raw = await _appointmentService.GetAvailableSlotsAsync(state.BranchId!.Value, date, string.Join(",", state.ServiceIds), bookingFlow: "FIXED", vehicleCount: 1, isParallel: false);
        return raw.Where(s => s.IsAvailable && s.RemainingCapacity > 0).OrderBy(s => s.StartTime).Take(8)
            .Select(s => new ChatbotBookingSlotOptionDto { StartTime = s.StartTime, EndTime = s.EndTime, RemainingCapacity = s.RemainingCapacity, MaxCapacity = s.MaxCapacity })
            .ToList();
    }

    private async Task<bool> IsBookableDay(long branchId, DateOnly date, CancellationToken ct)
    {
        var exception = await _context.BranchExceptions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.BranchId == branchId && x.ExceptionDate == date, ct);
        if (exception is not null)
        {
            if (exception.IsClosed == true) return false;
            if (exception.StartTime.HasValue && exception.EndTime.HasValue && exception.StartTime.Value >= exception.EndTime.Value) return false;
            return true;
        }

        var dow = (int)date.DayOfWeek;
        var operatingHour = await _context.BranchOperatingHours.AsNoTracking()
            .FirstOrDefaultAsync(x => x.BranchId == branchId && x.DayOfWeek == dow, ct);
        if (operatingHour is null) return false;
        if (operatingHour.IsDayOff == true) return false;
        if (operatingHour.OpenTime >= operatingHour.CloseTime) return false;
        if (operatingHour.SlotDurationMinutes <= 0 || operatingHour.MaxConcurrentBookings <= 0) return false;
        return true;
    }

    private async Task<Vehicle?> TryCreateVehicle(long customerId, string message, CancellationToken ct)
    {
        var plate = ParsePlate(message);
        var modelId = await ResolveModelIdFromMessageAsync(message, ct);
        if (string.IsNullOrWhiteSpace(plate) || !modelId.HasValue)
        {
            return null;
        }

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate == plate && v.IsDeleted != true, ct);
            if (vehicle is null)
            {
                vehicle = new Vehicle
                {
                    LicensePlate = plate,
                    ModelId = modelId.Value,
                    Vin = ParseVin(message),
                    ManufactureYear = ParseManufactureYear(message),
                    CreatedAt = _clock.UtcNow,
                    CreatedBy = customerId,
                    IsDeleted = false
                };
                _context.Vehicles.Add(vehicle);
                await _context.SaveChangesAsync(ct);
            }

            var linked = await _context.CustomerVehicles.AnyAsync(cv => cv.CustomerId == customerId && cv.VehicleId == vehicle.Id && cv.IsDeleted != true, ct);
            if (!linked)
            {
                _context.CustomerVehicles.Add(new CustomerVehicle
                {
                    CustomerId = customerId,
                    VehicleId = vehicle.Id,
                    IsCurrentOwner = true,
                    OwnershipStartDate = _clock.LocalToday,
                    CreatedAt = _clock.UtcNow,
                    CreatedBy = customerId,
                    IsDeleted = false
                });
                await _context.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);
            return vehicle;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private async Task<long?> ResolveModelIdFromMessageAsync(string message, CancellationToken ct)
    {
        var explicitModelId = ParseModelId(message);
        if (explicitModelId.HasValue)
        {
            var exists = await _context.VehicleModels
                .AsNoTracking()
                .AnyAsync(model => model.Id == explicitModelId.Value && model.IsDeleted != true, ct);
            if (exists)
            {
                return explicitModelId.Value;
            }
        }

        var normalizedInput = NormalizeNlp(message);
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return null;
        }

        var candidates = await _context.VehicleModels
            .AsNoTracking()
            .Where(model => model.IsDeleted != true && model.Brand.IsDeleted != true)
            .Select(model => new
            {
                model.Id,
                Label = string.Concat(model.Brand.Name, " ", model.ModelName)
            })
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return null;
        }

        var best = candidates
            .Select(candidate => new
            {
                candidate.Id,
                Score = ScoreTextOverlap(normalizedInput, NormalizeNlp(candidate.Label))
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Id)
            .FirstOrDefault();

        return best is { Score: >= 2 } ? best.Id : null;
    }

    private async Task ClearSlotSelectionAsync(BookingState state, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(state.SlotHoldToken))
        {
            try
            {
                await _appointmentService.ReleaseSlotHoldAsync(state.SlotHoldToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to release slot hold token {HoldToken}", state.SlotHoldToken);
            }
        }

        state.SelectedSlotStartUtc = null;
        state.SelectedSlotEndUtc = null;
        state.SlotHoldToken = null;
        state.SlotHoldExpiresAtUtc = null;
    }

    private async Task<string> BuildBookingConfirmationSummaryAsync(BookingState state, DateTime start, DateTime end, CancellationToken ct)
    {
        var vehicleText = "Chưa xác định";
        if (state.VehicleId.HasValue)
        {
            vehicleText = await _context.Vehicles
                .AsNoTracking()
                .Where(vehicle => vehicle.Id == state.VehicleId.Value && vehicle.IsDeleted != true)
                .Select(vehicle => string.Concat(vehicle.LicensePlate, " - ", vehicle.Model.Brand.Name, " ", vehicle.Model.ModelName).Trim())
                .FirstOrDefaultAsync(ct)
                ?? vehicleText;
        }

        var branchText = "Chưa xác định";
        if (state.BranchId.HasValue)
        {
            branchText = await _context.Branches
                .AsNoTracking()
                .Where(branch => branch.Id == state.BranchId.Value && branch.IsDeleted != true)
                .Select(branch => branch.Name)
                .FirstOrDefaultAsync(ct)
                ?? branchText;
        }

        var serviceRows = await _context.StandardServices
            .AsNoTracking()
            .Where(service => state.ServiceIds.Contains(service.Id) && service.IsDeleted != true && service.IsActive == true)
            .Select(service => new
            {
                service.Name,
                service.BasePrice,
                Minutes = Math.Max(15, service.StandardMinutes)
            })
            .ToListAsync(ct);

        var serviceNames = serviceRows.Count == 0
            ? "Chưa xác định"
            : string.Join(", ", serviceRows.Select(service => service.Name));
        var totalMinutes = Math.Max(15, serviceRows.Sum(service => service.Minutes));
        var totalPrice = Math.Max(0m, serviceRows.Sum(service => service.BasePrice));

        var summary = new StringBuilder();
        summary.AppendLine("Tóm tắt đặt lịch:");
        summary.AppendLine($"- Xe: {vehicleText}");
        summary.AppendLine($"- Dịch vụ: {serviceNames}");
        summary.AppendLine($"- Chi nhánh: {branchText}");
        summary.AppendLine($"- Khung giờ: {start:HH:mm dd/MM/yyyy} - {end:HH:mm}");
        summary.AppendLine($"- Tổng thời lượng dự kiến: {totalMinutes} phút");
        summary.AppendLine($"- Giá dự kiến: {FormatCurrency(totalPrice)}");
        if (DateTime.TryParse(state.SlotHoldExpiresAtUtc, null, DateTimeStyles.RoundtripKind, out var holdExpiresAt))
        {
            summary.AppendLine($"- Giữ chỗ đến: {holdExpiresAt.ToLocalTime():HH:mm:ss dd/MM/yyyy}");
        }

        return summary.ToString().TrimEnd();
    }

    private async Task LogFunnelProgressAsync(
        long sessionId,
        BookingState state,
        ChatbotBookingWorkflowResponseDto response,
        string userMessage,
        CancellationToken ct)
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

        _context.ChatbotActionLogs.Add(new ChatbotActionLog
        {
            SessionId = sessionId,
            ActionType = "BOOKING_FUNNEL",
            Status = (response.Step == "COMPLETED" ? ChatbotActionStatus.SUCCESS : ChatbotActionStatus.PENDING).ToString().ToUpperInvariant(),
            ErrorCode = null,
            ErrorMessage = null,
            ActionPayload = JsonSerializer.Serialize(payload),
            CreatedAt = _clock.UtcNow
        });
        await _context.SaveChangesAsync(ct);
    }

    private async Task LogAction(long sessionId, ChatbotActionStatus status, string actionType, string? errorCode, string? errorMessage, ChatbotBookingResultDto? payload, CancellationToken ct)
    {
        _context.ChatbotActionLogs.Add(new ChatbotActionLog { SessionId = sessionId, ActionType = actionType, Status = status.ToString().ToUpperInvariant(), ErrorCode = errorCode, ErrorMessage = errorMessage, ActionPayload = payload is null ? null : JsonSerializer.Serialize(payload), CreatedAt = _clock.UtcNow });
        await _context.SaveChangesAsync(ct);
    }

    private async Task LogVoucherActionAsync(long sessionId, ChatbotVoucherResolutionDto? voucher, CancellationToken ct)
    {
        _context.ChatbotActionLogs.Add(new ChatbotActionLog
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
        });
        await _context.SaveChangesAsync(ct);
    }

    private async Task<ChatbotVoucherResolutionDto?> ResolveVoucherSuggestionAsync(Customer customer, BookingState state, CancellationToken ct)
    {
        if (!state.BranchId.HasValue || !state.VehicleId.HasValue || state.ServiceIds.Count == 0)
        {
            return null;
        }

        var eligibleAmount = await ResolveBookingEligibleAmountAsync(state.ServiceIds, ct);
        if (eligibleAmount <= 0m)
        {
            return null;
        }

        var resolution = await _voucherResolver.ResolveBestVoucherAsync(
            new ChatbotVoucherResolveRequest
            {
                CustomerId = customer.Id,
                BranchId = state.BranchId,
                VehicleId = state.VehicleId,
                CustomerTierId = customer.TierId,
                EligibleAmount = eligibleAmount,
                TierDiscountAmount = 0m,
                OrderType = ChatbotVoucherOrderType.BOOKING,
                ServiceIds = state.ServiceIds,
                ProductIds = Array.Empty<long>()
            },
            ct);

        return resolution.IsApplied ? resolution.Voucher : null;
    }

    private async Task<decimal> ResolveBookingEligibleAmountAsync(IReadOnlyList<long> serviceIds, CancellationToken ct)
    {
        var normalizedIds = serviceIds.Where(id => id > 0).Distinct().ToList();
        if (normalizedIds.Count == 0)
        {
            return 0m;
        }

        var baseAmount = await _context.StandardServices
            .AsNoTracking()
            .Where(service => normalizedIds.Contains(service.Id) && service.IsDeleted != true && service.IsActive == true)
            .SumAsync(service => (decimal?)service.BasePrice, ct);

        return Math.Max(0m, baseAmount ?? 0m);
    }

    private static ServiceCategoryOption? PickServiceCategory(
        IReadOnlyList<ServiceCategoryOption> options,
        string message,
        bool allowNumericSelection = true)
    {
        if (options.Count == 0 || string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        if (allowNumericSelection)
        {
            var numbers = ExtractNumbers(message).ToList();
            var byIndex = PickByIndex(options, numbers);
            if (byIndex is not null)
            {
                return byIndex;
            }

            var byId = options.FirstOrDefault(option => numbers.Contains(option.CategoryId));
            if (byId is not null)
            {
                return byId;
            }
        }

        var normalizedInput = NormalizeNlp(message);
        return options.FirstOrDefault(option => HasTextualOverlap(normalizedInput, NormalizeNlp(option.CategoryName)));
    }

    private static ServiceCategoryOption? PickServiceCategoryFromBookingUtterance(
        IReadOnlyList<ServiceCategoryOption> options,
        string message)
    {
        return PickServiceCategory(
            options,
            message,
            allowNumericSelection: HasServiceCategoryOrdinalSignal(message));
    }

    private static T? PickByIndex<T>(IReadOnlyList<T> options, IReadOnlyList<long> numbers) where T : class
    {
        if (options.Count == 0 || numbers.Count == 0)
        {
            return null;
        }

        foreach (var number in numbers)
        {
            if (number >= 1 && number <= options.Count)
            {
                return options[(int)number - 1];
            }
        }

        return null;
    }

    private static bool HasTextualOverlap(string source, string target)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        if (source.Contains(target, StringComparison.Ordinal) || target.Contains(source, StringComparison.Ordinal))
        {
            return true;
        }

        return ScoreTextOverlap(source, target) >= 2;
    }

    private static int ScoreTextOverlap(string source, string target)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
        {
            return 0;
        }

        var sourceTokens = source.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var targetTokens = target.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (sourceTokens.Length == 0 || targetTokens.Length == 0)
        {
            return 0;
        }

        var score = 0;
        foreach (var token in targetTokens)
        {
            if (token.Length < 2)
            {
                continue;
            }

            if (sourceTokens.Any(sourceToken => sourceToken.Equals(token, StringComparison.Ordinal)))
            {
                score++;
            }
        }

        return score;
    }

    private static string FormatVehicleOptions(IReadOnlyList<ChatbotBookingVehicleOptionDto> options)
    {
        if (options.Count == 0)
        {
            return "- Không có xe khả dụng.";
        }

        var lines = options
            .Select((option, index) => $"{index + 1}. [{option.VehicleId}] {option.LicensePlate} - {option.VehicleModelName}")
            .ToList();
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatServiceCategoryOptions(IReadOnlyList<ServiceCategoryOption> options)
    {
        if (options.Count == 0)
        {
            return "- Không có nhóm dịch vụ khả dụng.";
        }

        var lines = options
            .Select((option, index) => $"{index + 1}. [{option.CategoryId}] {option.CategoryName}")
            .ToList();
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatServiceOptions(IReadOnlyList<ServiceOptionRow> options)
    {
        if (options.Count == 0)
        {
            return "- Không có dịch vụ khả dụng.";
        }

        var lines = options
            .Select((option, index) =>
                $"{index + 1}. [{option.ServiceId}] {option.ServiceName} ({option.ServiceCode}) - {FormatCurrency(option.BasePrice)} - {option.StandardMinutes} phút")
            .ToList();
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatBranchOptions(IReadOnlyList<ChatbotBookingBranchOptionDto> options)
    {
        if (options.Count == 0)
        {
            return "- Không có chi nhánh khả dụng.";
        }

        var lines = options
            .Select((option, index) =>
                $"{index + 1}. [{option.BranchId}] {option.BranchName} - {option.Address ?? "Chưa cập nhật địa chỉ"}")
            .ToList();
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatSlotOptions(IReadOnlyList<ChatbotBookingSlotOptionDto> options)
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

    private static string FormatCurrency(decimal amount)
        => string.Format(CultureInfo.InvariantCulture, "{0:N0} VND", amount);

    private static ChatbotBookingVehicleOptionDto? PickVehicle(
        IReadOnlyList<ChatbotBookingVehicleOptionDto> options,
        string message,
        bool allowNumericSelection = true)
    {
        if (options.Count == 0 || string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        if (allowNumericSelection)
        {
            var numbers = ExtractNumbers(message).ToList();
            if (numbers.Count > 0)
            {
                var byIndex = PickByIndex(options, numbers);
                if (byIndex is not null)
                {
                    return byIndex;
                }

                var byId = options.FirstOrDefault(o => numbers.Contains(o.VehicleId));
                if (byId is not null)
                {
                    return byId;
                }
            }
        }

        var normalizedInput = NormalizeNlp(message);
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return null;
        }

        return options.FirstOrDefault(o => ContainsNormalized(o.LicensePlate, normalizedInput))
            ?? options.FirstOrDefault(o => ContainsNormalized(o.VehicleModelName, normalizedInput));
    }

    private static ChatbotBookingVehicleOptionDto? PickVehicleFromBookingUtterance(
        IReadOnlyList<ChatbotBookingVehicleOptionDto> options,
        string message)
    {
        return PickVehicle(
            options,
            message,
            allowNumericSelection: HasVehicleSelectionSignal(message));
    }

    private static bool ContainsNormalized(string? source, string normalizedInput)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(normalizedInput))
        {
            return false;
        }

        var normalizedSource = NormalizeNlp(source);
        return normalizedSource.Contains(normalizedInput, StringComparison.Ordinal)
            || normalizedInput.Contains(normalizedSource, StringComparison.Ordinal);
    }

    private static List<long> PickServices(
        IReadOnlyList<ChatbotBookingServiceOptionDto> options,
        string message,
        IReadOnlyList<long> current,
        bool allowNumericSelection = true)
    {
        var set = new HashSet<long>(current);

        if (allowNumericSelection)
        {
            var numbers = ExtractNumbers(message).ToList();

            foreach (var number in numbers)
            {
                var matchedById = options.FirstOrDefault(option => option.ServiceId == number);
                if (matchedById is not null)
                {
                    set.Add(matchedById.ServiceId);
                }
            }

            foreach (var number in numbers)
            {
                if (number >= 1 && number <= options.Count)
                {
                    set.Add(options[(int)number - 1].ServiceId);
                }
            }
        }

        var normalizedInput = NormalizeNlp(message);
        var textualMatches = new HashSet<long>();
        foreach (var option in options)
        {
            if (!string.IsNullOrWhiteSpace(option.ServiceCode))
            {
                var normalizedCode = NormalizeNlp(option.ServiceCode);
                if (!string.IsNullOrWhiteSpace(normalizedCode)
                    && normalizedInput.Contains(normalizedCode, StringComparison.Ordinal))
                {
                    set.Add(option.ServiceId);
                    continue;
                }
            }

            var normalizedName = NormalizeNlp(option.ServiceName);
            if (!string.IsNullOrWhiteSpace(normalizedName)
                && normalizedInput.Contains(normalizedName, StringComparison.Ordinal))
            {
                set.Add(option.ServiceId);
                continue;
            }

            if (HasTextualOverlap(normalizedInput, normalizedName))
            {
                textualMatches.Add(option.ServiceId);
            }
        }

        if (textualMatches.Count == 1)
        {
            set.Add(textualMatches.First());
        }

        return set.ToList();
    }

    private static bool MentionsSpecificService(string? message, IReadOnlyList<ChatbotBookingServiceOptionDto> options)
    {
        if (string.IsNullOrWhiteSpace(message) || options.Count == 0)
        {
            return false;
        }

        var normalizedInput = NormalizeNlp(message);
        foreach (var option in options)
        {
            var normalizedCode = NormalizeNlp(option.ServiceCode);
            if (!string.IsNullOrWhiteSpace(normalizedCode)
                && normalizedInput.Contains(normalizedCode, StringComparison.Ordinal))
            {
                return true;
            }

            var normalizedName = NormalizeNlp(option.ServiceName);
            if (!string.IsNullOrWhiteSpace(normalizedName)
                && normalizedInput.Contains(normalizedName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static ChatbotBookingBranchOptionDto? PickBranch(
        IReadOnlyList<ChatbotBookingBranchOptionDto> options,
        string message,
        bool allowNumericSelection = true)
    {
        if (options.Count == 0 || string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        if (allowNumericSelection)
        {
            var numbers = ExtractNumbers(message).ToList();
            var byIndex = PickByIndex(options, numbers);
            if (byIndex is not null)
            {
                return byIndex;
            }

            var byId = options.FirstOrDefault(option => numbers.Contains(option.BranchId));
            if (byId is not null)
            {
                return byId;
            }
        }

        var normalizedInput = NormalizeNlp(message);
        return options.FirstOrDefault(option => HasTextualOverlap(normalizedInput, NormalizeNlp(option.BranchName)));
    }

    private static ChatbotBookingBranchOptionDto? PickBranchFromBookingUtterance(
        IReadOnlyList<ChatbotBookingBranchOptionDto> options,
        string message)
    {
        return PickBranch(
            options,
            message,
            allowNumericSelection: HasBranchSelectionSignal(message));
    }

    private static bool LooksLikeVehicleSelectionReply(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = NormalizeNlp(message);
        return normalized == "xe"
            || normalized.StartsWith("xe ", StringComparison.Ordinal)
            || normalized == "vehicle"
            || normalized.StartsWith("vehicle ", StringComparison.Ordinal)
            || normalized.Contains("bien so", StringComparison.Ordinal)
            || normalized.Contains("license plate", StringComparison.Ordinal);
    }

    private static ChatbotBookingSlotOptionDto? PickSlot(IReadOnlyList<ChatbotBookingSlotOptionDto> options, string message)
    {
        var normalized = NormalizeNlp(message);
        if (normalized.Contains("slot", StringComparison.Ordinal)
            || normalized.Contains("khung", StringComparison.Ordinal)
            || normalized.Contains("so", StringComparison.Ordinal))
        {
            var idx = (int)ExtractNumbers(message).FirstOrDefault(); if (idx >= 1 && idx <= options.Count) return options[idx - 1];
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
            var byMoment = options.FirstOrDefault(o => SameMoment(o.StartTime, dt));
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
    private static bool IsAvailableBranch(BranchRow row) => string.Equals(row.Status.Trim(), "ACTIVE", StringComparison.OrdinalIgnoreCase) && ClosedHints.All(h => !($"{row.Status} {row.Description} {row.Name}").ToUpperInvariant().Contains(h, StringComparison.Ordinal));
    private static bool Confirm(string message)
    {
        var n = NormalizeNlp(message);
        if (string.IsNullOrWhiteSpace(n))
        {
            return false;
        }

        return n is "yes" or "y" or "ok" or "oke" or "confirm"
            || n.Contains("dong y", StringComparison.Ordinal)
            || n.Contains("xac nhan", StringComparison.Ordinal)
            || n.Contains("duoc", StringComparison.Ordinal)
            || n.Equals("vang", StringComparison.Ordinal)
            || n.Equals("da", StringComparison.Ordinal);
    }

    private static BookingNavigationCommand ResolveNavigationCommand(string message)
    {
        var normalized = NormalizeNlp(message);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return BookingNavigationCommand.None;
        }

        if (normalized.Contains("dat lich moi", StringComparison.Ordinal)
            || normalized.Contains("dat lich lai", StringComparison.Ordinal)
            || normalized.Contains("bat dau lai", StringComparison.Ordinal)
            || normalized.Contains("start over", StringComparison.Ordinal)
            || normalized.Contains("restart", StringComparison.Ordinal))
        {
            return BookingNavigationCommand.Restart;
        }

        if (normalized.Contains("huy", StringComparison.Ordinal)
            || normalized.Contains("thoat", StringComparison.Ordinal)
            || normalized.Contains("cancel", StringComparison.Ordinal))
        {
            return BookingNavigationCommand.Cancel;
        }

        if (normalized.Contains("doi xe", StringComparison.Ordinal))
        {
            return BookingNavigationCommand.ChangeVehicle;
        }

        if (normalized.Contains("doi dich vu", StringComparison.Ordinal)
            || normalized.Contains("doi nhom", StringComparison.Ordinal))
        {
            return BookingNavigationCommand.ChangeService;
        }

        if (normalized.Contains("doi ngay", StringComparison.Ordinal))
        {
            return BookingNavigationCommand.ChangeDate;
        }

        if (normalized.Equals("quay lai", StringComparison.Ordinal)
            || normalized.Equals("back", StringComparison.Ordinal)
            || normalized.Equals("tro lai", StringComparison.Ordinal))
        {
            return BookingNavigationCommand.Back;
        }

        return BookingNavigationCommand.None;
    }

    private DateTime NormalizeBookingDateTime(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
    }

    private string BuildFinalizeErrorMessage(string? rawMessage)
    {
        var normalized = NormalizeNlp(rawMessage);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "Đặt lịch thất bại. Vui lòng thử lại.";
        }

        if (normalized.Contains("het cong suat", StringComparison.Ordinal)
            || normalized.Contains("qua tai", StringComparison.Ordinal)
            || normalized.Contains("khung gio", StringComparison.Ordinal))
        {
            return "Vừa có đặt lịch trùng khung giờ. Vui lòng chọn lại khung khác.";
        }

        if (normalized.Contains("giu cho", StringComparison.Ordinal)
            || normalized.Contains("het han", StringComparison.Ordinal))
        {
            return "Giữ chỗ đã hết hạn. Vui lòng chọn lại khung giờ.";
        }

        if (normalized.Contains("already in a transaction", StringComparison.Ordinal)
            || normalized.Contains("cannot participate in another transaction", StringComparison.Ordinal))
        {
            return "Hệ thống đang bận, vui lòng thử lại sau vài giây.";
        }

        if (normalized.Contains("gio lam viec", StringComparison.Ordinal))
        {
            return "Khung giờ bạn chọn nằm ngoài giờ làm việc của chi nhánh. Vui lòng chọn khung khác.";
        }

        if (normalized.Contains("chi nhanh khong", StringComparison.Ordinal)
            || normalized.Contains("ngay nay", StringComparison.Ordinal))
        {
            return "Chi nhánh không hoạt động ở thời điểm bạn chọn. Vui lòng chọn khung khác.";
        }

        if (normalized.Contains("deadlock", StringComparison.Ordinal)
            || normalized.Contains("timeout", StringComparison.Ordinal)
            || normalized.Contains("connection", StringComparison.Ordinal))
        {
            return "Hệ thống tạm thời quá tải. Vui lòng thử lại sau ít phút.";
        }

        return "Đặt lịch thất bại do lỗi hệ thống. Vui lòng thử lại.";
    }

    private static bool IsDirectBookingRequest(string message)
    {
        var n = NormalizeNlp(message);
        if (string.IsNullOrWhiteSpace(n))
        {
            return false;
        }

        var asksToBook = n.Contains("dat lich", StringComparison.Ordinal)
            || n.Contains("book", StringComparison.Ordinal)
            || n.Contains("appointment", StringComparison.Ordinal);
        if (!asksToBook)
        {
            return false;
        }

        return n.Contains("giup toi", StringComparison.Ordinal)
            || n.Contains("giup em", StringComparison.Ordinal)
            || n.Contains("ho toi", StringComparison.Ordinal)
            || n.Contains("ho em", StringComparison.Ordinal)
            || n.Contains("book for me", StringComparison.Ordinal)
            || n.Contains("tu dong", StringComparison.Ordinal)
            || n.Contains("luon", StringComparison.Ordinal)
            || n.Contains("ngay", StringComparison.Ordinal);
    }

    private static bool IsRestartBookingRequest(string message)
    {
        var n = NormalizeNlp(message);
        if (string.IsNullOrWhiteSpace(n))
        {
            return false;
        }

        if (n.Contains("dat lich moi", StringComparison.Ordinal)
            || n.Contains("dat lich lai", StringComparison.Ordinal)
            || n.Contains("bat dau lai", StringComparison.Ordinal)
            || n.Contains("start over", StringComparison.Ordinal)
            || n.Contains("restart", StringComparison.Ordinal))
        {
            return true;
        }

        return IsDirectBookingRequest(message);
    }

    private static string? Warning(int? noShowCount) => noShowCount.HasValue && noShowCount.Value >= 2 ? $"Cảnh báo: tài khoản có {noShowCount.Value} lần no-show gần đây." : null;
    private static JsonObject ParseRoot(string? stateJson) { try { return string.IsNullOrWhiteSpace(stateJson) ? new JsonObject() : JsonNode.Parse(stateJson) as JsonObject ?? new JsonObject(); } catch { return new JsonObject(); } }
    private static IEnumerable<long> ExtractNumbers(string text) => NumberRegex.Matches(text ?? string.Empty).Select(x => long.TryParse(x.Value, out var n) ? n : 0).Where(x => x > 0);
    private static string? ParsePlate(string text)
    {
        var matches = PlateRegex.Matches((text ?? string.Empty).ToUpperInvariant());
        foreach (Match match in matches)
        {
            if (!match.Success)
            {
                continue;
            }

            var candidate = match.Value.ToUpperInvariant();
            var hasLetter = candidate.Any(char.IsLetter);
            var hasDigit = candidate.Any(char.IsDigit);
            if (hasLetter && hasDigit)
            {
                return candidate;
            }
        }

        return null;
    }

    private static long? ParseModelId(string text)
    {
        var match = ModelRegex.Match(text ?? string.Empty);
        if (match.Success && long.TryParse(match.Groups[1].Value, out var id) && id > 0)
        {
            return id;
        }

        return null;
    }

    private int? ParseManufactureYear(string text)
    {
        var nowYear = _clock.LocalNow.Year + 1;
        var years = NumberRegex.Matches(text ?? string.Empty)
            .Select(match => int.TryParse(match.Value, out var parsed) ? parsed : 0)
            .Where(year => year >= 1980 && year <= nowYear)
            .ToList();
        return years.Count > 0 ? years[0] : null;
    }

    private static string? ParseVin(string text) { var m = VinRegex.Match((text ?? string.Empty).ToUpperInvariant()); return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null; }
    private DateOnly? ParseDate(string text)
    {
        var normalized = NormalizeNlp(text);
        if (normalized is "today" or "hom nay"
            || normalized.Contains("hom nay", StringComparison.Ordinal))
        {
            return DateOnly.FromDateTime(_clock.LocalNow);
        }

        if (normalized is "tomorrow" or "ngay mai" or "mai"
            || normalized.Contains("ngay mai", StringComparison.Ordinal))
        {
            return DateOnly.FromDateTime(_clock.LocalNow.AddDays(1));
        }

        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d1))
        {
            return d1;
        }

        if (DateOnly.TryParseExact(text, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2))
        {
            return d2;
        }

        return null;
    }
    private static bool TryDate(string? text, out DateOnly date) => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    private static bool SameMoment(DateTime left, DateTime right) => left.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) == right.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static bool HasVehicleSelectionSignal(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = NormalizeNlp(message);
        return Regex.IsMatch(normalized, @"\b(?:xe|vehicle)\s+(?:so\s+)?\d+\b", RegexOptions.IgnoreCase)
            || normalized.Contains("bien so", StringComparison.Ordinal)
            || normalized.Contains("license plate", StringComparison.Ordinal);
    }

    private static bool HasServiceCategoryOrdinalSignal(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = NormalizeNlp(message);
        return normalized.Contains("nhom", StringComparison.Ordinal)
            || normalized.Contains("category", StringComparison.Ordinal)
            || normalized.Contains("loai dich vu", StringComparison.Ordinal);
    }

    private static bool HasServiceCategorySelectionSignal(
        string? message,
        IReadOnlyList<ServiceCategoryOption> categories)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        if (HasServiceCategoryOrdinalSignal(message))
        {
            return true;
        }

        var normalized = NormalizeNlp(message);
        return categories.Any(category =>
        {
            var normalizedName = NormalizeNlp(category.CategoryName);
            return !string.IsNullOrWhiteSpace(normalizedName)
                && normalized.Contains(normalizedName, StringComparison.Ordinal);
        });
    }

    private static bool HasServiceOrdinalSelectionSignal(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = NormalizeNlp(message);
        return normalized.Contains("dich vu", StringComparison.Ordinal)
            || normalized.Contains("service", StringComparison.Ordinal)
            || normalized.Contains("svc", StringComparison.Ordinal);
    }

    private static bool HasBranchSelectionSignal(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = NormalizeNlp(message);
        return normalized.Contains("chi nhanh", StringComparison.Ordinal)
            || normalized.Contains("branch", StringComparison.Ordinal)
            || normalized.Contains("dia diem", StringComparison.Ordinal)
            || normalized.Contains("garage", StringComparison.Ordinal);
    }

    private static bool ShouldTryPrefillFallback(string message, string? prefillInput)
    {
        if (string.IsNullOrWhiteSpace(prefillInput))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return true;
        }

        return !NormalizeNlp(message).Equals(NormalizeNlp(prefillInput), StringComparison.Ordinal);
    }

    private static string NormalizeNlp(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var lowered = text.Trim().ToLowerInvariant().Replace('\u0111', 'd').Replace('\u0110', 'd');
        var decomposed = lowered.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            sb.Append(char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) ? ch : ' ');
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private ChatbotBookingWorkflowResponseDto Build(
        JsonObject root,
        BookingState state,
        bool requiresUserInput,
        string prompt,
        IReadOnlyList<ChatbotBookingVehicleOptionDto>? vehicleOptions = null,
        IReadOnlyList<ChatbotBookingServiceOptionDto>? serviceOptions = null,
        IReadOnlyList<ChatbotBookingServiceCategoryOptionDto>? serviceCategoryOptions = null,
        IReadOnlyList<ChatbotBookingBranchOptionDto>? branchOptions = null,
        IReadOnlyList<ChatbotBookingSlotOptionDto>? slotOptions = null,
        ChatbotBookingResultDto? bookingResult = null,
        string? noShowWarning = null,
        ChatbotVoucherResolutionDto? voucherSuggestion = null)
    {
        root["booking"] = state.ToJson();
        root["flow"] = new JsonObject { ["intent"] = nameof(ChatbotIntentType.BOOKING), ["step"] = state.Step.ToLabel(), ["updated_at"] = DateTime.UtcNow };
        var finalPrompt = requiresUserInput && state.Step != BookingStep.Completed
            ? $"{prompt}{Environment.NewLine}{Environment.NewLine}Lệnh nhanh: `quay lại`, `đổi xe`, `đổi dịch vụ`, `đổi ngày`, `đặt lịch mới`, `hủy`."
            : prompt;
        var holdExpiresAtUtc = DateTime.TryParse(state.SlotHoldExpiresAtUtc, null, DateTimeStyles.RoundtripKind, out var holdExpires)
            ? holdExpires.ToUniversalTime()
            : (DateTime?)null;
        var holdSecondsRemaining = holdExpiresAtUtc.HasValue
            ? Math.Max(0, (int)Math.Ceiling((holdExpiresAtUtc.Value - _clock.UtcNow).TotalSeconds))
            : (int?)null;
        var computedQuickReplies = BuildQuickReplies(
            state,
            vehicleOptions ?? [],
            serviceCategoryOptions ?? [],
            serviceOptions ?? [],
            branchOptions ?? [],
            slotOptions ?? []);
        return new ChatbotBookingWorkflowResponseDto
        {
            Step = state.Step.ToLabel(),
            RequiresUserInput = requiresUserInput,
            Prompt = finalPrompt,
            NoShowWarning = noShowWarning,
            VehicleOptions = vehicleOptions ?? [],
            ServiceCategoryOptions = serviceCategoryOptions ?? [],
            ServiceOptions = serviceOptions ?? [],
            BranchOptions = branchOptions ?? [],
            SlotOptions = slotOptions ?? [],
            QuickReplies = computedQuickReplies,
            SlotHoldToken = state.SlotHoldToken,
            SlotHoldExpiresAtUtc = holdExpiresAtUtc,
            SlotHoldSecondsRemaining = holdSecondsRemaining,
            BookingResult = bookingResult,
            VoucherSuggestion = voucherSuggestion,
            UpdatedIntentState = root.ToJsonString()
        };
    }

    private static IReadOnlyList<ChatbotQuickReplyOptionDto> BuildQuickReplies(
        BookingState state,
        IReadOnlyList<ChatbotBookingVehicleOptionDto> vehicleOptions,
        IReadOnlyList<ChatbotBookingServiceCategoryOptionDto> serviceCategoryOptions,
        IReadOnlyList<ChatbotBookingServiceOptionDto> serviceOptions,
        IReadOnlyList<ChatbotBookingBranchOptionDto> branchOptions,
        IReadOnlyList<ChatbotBookingSlotOptionDto> slotOptions)
    {
        var quickReplies = new List<ChatbotQuickReplyOptionDto>();
        switch (state.Step)
        {
            case BookingStep.IdentifyOrCreateVehicle:
                for (var i = 0; i < Math.Min(4, vehicleOptions.Count); i++)
                {
                    AddQuickReply(quickReplies, $"Xe {i + 1}", $"xe {i + 1}");
                }
                AddQuickReply(quickReplies, "Hủy", "huy");
                break;

            case BookingStep.SelectServices:
                if (!state.ServiceCategoryId.HasValue)
                {
                    for (var i = 0; i < Math.Min(6, serviceCategoryOptions.Count); i++)
                    {
                        AddQuickReply(quickReplies, $"Nhóm {i + 1}", $"nhom {i + 1}");
                    }
                    AddQuickReply(quickReplies, "Quay lại", "quay lai");
                    AddQuickReply(quickReplies, "Đổi xe", "doi xe");
                    AddQuickReply(quickReplies, "Đặt lịch mới", "dat lich moi");
                    AddQuickReply(quickReplies, "Hủy", "huy");
                }
                else
                {
                    for (var i = 0; i < Math.Min(6, serviceOptions.Count); i++)
                    {
                        AddQuickReply(quickReplies, $"Dịch vụ {i + 1}", $"dich vu {i + 1}");
                    }
                    AddQuickReply(quickReplies, "Đổi dịch vụ", "doi dich vu");
                    AddQuickReply(quickReplies, "Quay lại", "quay lai");
                    AddQuickReply(quickReplies, "Đặt lịch mới", "dat lich moi");
                    AddQuickReply(quickReplies, "Hủy", "huy");
                }
                break;

            case BookingStep.SelectBranch:
                for (var i = 0; i < Math.Min(4, branchOptions.Count); i++)
                {
                    AddQuickReply(quickReplies, $"Chi nhánh {i + 1}", $"chi nhanh {i + 1}");
                }
                AddQuickReply(quickReplies, "Quay lại", "quay lai");
                AddQuickReply(quickReplies, "Đổi dịch vụ", "doi dich vu");
                AddQuickReply(quickReplies, "Đặt lịch mới", "dat lich moi");
                AddQuickReply(quickReplies, "Hủy", "huy");
                break;

            case BookingStep.ComputeAvailableSlots:
                if (string.IsNullOrWhiteSpace(state.BookingDate))
                {
                    AddQuickReply(quickReplies, "Hôm nay", "hom nay");
                    AddQuickReply(quickReplies, "Ngày mai", "ngay mai");
                }
                for (var i = 0; i < Math.Min(4, slotOptions.Count); i++)
                {
                    AddQuickReply(quickReplies, $"Slot {i + 1}", $"slot {i + 1}");
                }
                AddQuickReply(quickReplies, "Đổi ngày", "doi ngay");
                AddQuickReply(quickReplies, "Quay lại", "quay lai");
                AddQuickReply(quickReplies, "Đặt lịch mới", "dat lich moi");
                AddQuickReply(quickReplies, "Hủy", "huy");
                break;

            case BookingStep.FinalizeBooking:
                AddQuickReply(quickReplies, "Đồng ý", "dong y");
                AddQuickReply(quickReplies, "Đổi ngày", "doi ngay");
                AddQuickReply(quickReplies, "Quay lại", "quay lai");
                AddQuickReply(quickReplies, "Đặt lịch mới", "dat lich moi");
                AddQuickReply(quickReplies, "Hủy", "huy");
                break;

            case BookingStep.Completed:
                AddQuickReply(quickReplies, "Đặt lịch mới", "dat lich");
                break;
        }

        if (state.Step != BookingStep.Completed)
        {
            AddQuickReply(quickReplies, "Tư vấn", "tu van cho toi");
            AddQuickReply(quickReplies, "Mua hàng", "toi can mua hang");
        }

        return quickReplies;
    }

    private static void AddQuickReply(List<ChatbotQuickReplyOptionDto> quickReplies, string label, string value)
    {
        if (quickReplies.Any(item => item.Value.Equals(value, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        quickReplies.Add(new ChatbotQuickReplyOptionDto
        {
            Label = label,
            Value = value
        });
    }

    private sealed class BranchRow { public long Id { get; set; } public string Name { get; set; } = string.Empty; public string Status { get; set; } = string.Empty; public string Description { get; set; } = string.Empty; public string? Address { get; set; } public string? Hotline { get; set; } }
    private sealed class ServiceCategoryOption
    {
        public long CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }

    private sealed class ServiceOptionRow
    {
        public long ServiceId { get; set; }
        public string ServiceCode { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public decimal BasePrice { get; set; }
        public int StandardMinutes { get; set; }
    }

    private sealed class BookingState
    {
        public BookingStep Step { get; set; } = BookingStep.IdentifyOrCreateVehicle;
        public long? VehicleId { get; set; }
        public long? ServiceCategoryId { get; set; }
        public List<long> ServiceIds { get; set; } = [];
        public long? BranchId { get; set; }
        public string? BookingDate { get; set; }
        public string? SelectedSlotStartUtc { get; set; }
        public string? SelectedSlotEndUtc { get; set; }
        public string? SlotHoldToken { get; set; }
        public string? SlotHoldExpiresAtUtc { get; set; }
        public string? PrefillInput { get; set; }
        public int TurnCount { get; set; }
        public bool AutoConfirmRequested { get; set; }
        public BookingState Reset() => new();
        public JsonNode ToJson() => JsonSerializer.SerializeToNode(this) ?? new JsonObject();

        public static BookingState From(string? intentState)
        {
            if (string.IsNullOrWhiteSpace(intentState)) return new BookingState();
            try
            {
                using var doc = JsonDocument.Parse(intentState);
                if (!doc.RootElement.TryGetProperty("booking", out var b)) return new BookingState();
                var s = new BookingState();
                if (b.TryGetProperty("Step", out var st) || b.TryGetProperty("step", out st))
                {
                    if (st.ValueKind == JsonValueKind.String && Enum.TryParse<BookingStep>(st.GetString(), true, out var parsed)) s.Step = parsed;
                    else if (st.ValueKind == JsonValueKind.Number && st.TryGetInt32(out var i) && i >= 0 && i <= 5) s.Step = (BookingStep)i;
                }
                if ((b.TryGetProperty("VehicleId", out var v) || b.TryGetProperty("vehicleId", out v)) && v.ValueKind == JsonValueKind.Number) s.VehicleId = v.GetInt64();
                if ((b.TryGetProperty("ServiceCategoryId", out var cat) || b.TryGetProperty("serviceCategoryId", out cat)) && cat.ValueKind == JsonValueKind.Number) s.ServiceCategoryId = cat.GetInt64();
                if ((b.TryGetProperty("BranchId", out var br) || b.TryGetProperty("branchId", out br)) && br.ValueKind == JsonValueKind.Number) s.BranchId = br.GetInt64();
                if ((b.TryGetProperty("BookingDate", out var d) || b.TryGetProperty("bookingDate", out d)) && d.ValueKind == JsonValueKind.String) s.BookingDate = d.GetString();
                if ((b.TryGetProperty("SelectedSlotStartUtc", out var ss) || b.TryGetProperty("selectedSlotStartUtc", out ss)) && ss.ValueKind == JsonValueKind.String) s.SelectedSlotStartUtc = ss.GetString();
                if ((b.TryGetProperty("SelectedSlotEndUtc", out var se) || b.TryGetProperty("selectedSlotEndUtc", out se)) && se.ValueKind == JsonValueKind.String) s.SelectedSlotEndUtc = se.GetString();
                if ((b.TryGetProperty("SlotHoldToken", out var sht) || b.TryGetProperty("slotHoldToken", out sht)) && sht.ValueKind == JsonValueKind.String) s.SlotHoldToken = sht.GetString();
                if ((b.TryGetProperty("SlotHoldExpiresAtUtc", out var she) || b.TryGetProperty("slotHoldExpiresAtUtc", out she)) && she.ValueKind == JsonValueKind.String) s.SlotHoldExpiresAtUtc = she.GetString();
                if ((b.TryGetProperty("PrefillInput", out var pi) || b.TryGetProperty("prefillInput", out pi)) && pi.ValueKind == JsonValueKind.String) s.PrefillInput = pi.GetString();
                if ((b.TryGetProperty("TurnCount", out var tc) || b.TryGetProperty("turnCount", out tc)) && tc.ValueKind == JsonValueKind.Number && tc.TryGetInt32(out var turnCount)) s.TurnCount = Math.Max(0, turnCount);
                if ((b.TryGetProperty("AutoConfirmRequested", out var ac) || b.TryGetProperty("autoConfirmRequested", out ac)) &&
                    (ac.ValueKind == JsonValueKind.True || ac.ValueKind == JsonValueKind.False)) s.AutoConfirmRequested = ac.GetBoolean();
                if ((b.TryGetProperty("ServiceIds", out var arr) || b.TryGetProperty("serviceIds", out arr)) && arr.ValueKind == JsonValueKind.Array) s.ServiceIds = arr.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetInt64()).ToList();
                return s;
            }
            catch { return new BookingState(); }
        }
    }

    private enum BookingNavigationCommand
    {
        None = 0,
        Back = 1,
        ChangeVehicle = 2,
        ChangeService = 3,
        ChangeDate = 4,
        Cancel = 5,
        Restart = 6
    }

    private enum BookingStep { IdentifyOrCreateVehicle = 0, SelectServices = 1, SelectBranch = 2, ComputeAvailableSlots = 3, FinalizeBooking = 4, Completed = 5 }
}

internal static class BookingStepLabel
{
    public static string ToLabel(this object step) => step.ToString() switch
    {
        "IdentifyOrCreateVehicle" => "IDENTIFY_OR_CREATE_VEHICLE",
        "SelectServices" => "SELECT_SERVICES",
        "SelectBranch" => "SELECT_BRANCH",
        "ComputeAvailableSlots" => "COMPUTE_AVAILABLE_SLOTS",
        "FinalizeBooking" => "FINALIZE_BOOKING",
        "Completed" => "COMPLETED",
        _ => "IDENTIFY_OR_CREATE_VEHICLE"
    };
}







