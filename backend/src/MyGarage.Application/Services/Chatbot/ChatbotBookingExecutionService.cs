using System.Globalization;
using System.Text;
using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

/// <summary>
/// Handles appointment creation (single and group), confirmation summary formatting, voucher resolution, and booking error mapping.
/// </summary>
public sealed class ChatbotBookingExecutionService : IChatbotBookingExecutionService
{
    private readonly IChatbotBookingStore _store;
    private readonly IAppointmentBookingCommandService _appointmentBookingService;
    private readonly IChatbotVoucherResolverService _voucherResolver;

    public ChatbotBookingExecutionService(
        IChatbotBookingStore store,
        IAppointmentBookingCommandService appointmentBookingService,
        IChatbotVoucherResolverService voucherResolver)
    {
        _store = store;
        _appointmentBookingService = appointmentBookingService;
        _voucherResolver = voucherResolver;
    }

    public async Task<ChatbotBookingResultDto> ExecuteBookingAsync(
        Customer customer,
        long branchId,
        long vehicleId,
        IReadOnlyList<long> serviceIds,
        DateTime start,
        string? selectedSlotEndUtc,
        string? slotHoldToken,
        CancellationToken ct = default)
    {
        if (serviceIds.Count > 1)
        {
            return await BookGroupAsync(customer, branchId, vehicleId, serviceIds, start, slotHoldToken, ct);
        }

        return await BookSingleAsync(customer, branchId, vehicleId, serviceIds[0], start, selectedSlotEndUtc, slotHoldToken);
    }

    public async Task<string> BuildBookingConfirmationSummaryAsync(
        long? vehicleId,
        long? branchId,
        IReadOnlyList<long> serviceIds,
        DateTime start,
        DateTime end,
        string? slotHoldExpiresAtUtc,
        CancellationToken ct = default)
    {
        var vehicleText = "Chưa xác định";
        if (vehicleId.HasValue)
        {
            var vehicleSummary = await _store.GetVehicleSummaryAsync(vehicleId.Value, ct);
            if (vehicleSummary != null)
            {
                vehicleText = $"{vehicleSummary.LicensePlate} - {vehicleSummary.BrandName} {vehicleSummary.ModelName}".Trim();
            }
        }

        var branchText = "Chưa xác định";
        if (branchId.HasValue)
        {
            branchText = await _store.GetBranchNameAsync(branchId.Value, ct) ?? branchText;
        }

        var serviceRows = await _store.GetActiveServiceSummariesAsync(serviceIds, ct);

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
        summary.AppendLine($"- Giá dự kiến: {ChatbotBookingNlpHelper.FormatCurrency(totalPrice)}");
        if (DateTime.TryParse(slotHoldExpiresAtUtc, null, DateTimeStyles.RoundtripKind, out var holdExpiresAt))
        {
            summary.AppendLine($"- Giữ chỗ đến: {VietnamTime.ToVietnamTime(holdExpiresAt):HH:mm:ss dd/MM/yyyy}");
        }

        return summary.ToString().TrimEnd();
    }

    public async Task<ChatbotVoucherResolutionDto?> ResolveVoucherSuggestionAsync(
        Customer customer,
        long? branchId,
        long? vehicleId,
        IReadOnlyList<long> serviceIds,
        CancellationToken ct = default)
    {
        if (!branchId.HasValue || !vehicleId.HasValue || serviceIds.Count == 0)
        {
            return null;
        }

        var eligibleAmount = await ResolveBookingEligibleAmountAsync(serviceIds, ct);
        if (eligibleAmount <= 0m)
        {
            return null;
        }

        var resolution = await _voucherResolver.ResolveBestVoucherAsync(
            new ChatbotVoucherResolveRequest
            {
                CustomerId = customer.Id,
                BranchId = branchId,
                VehicleId = vehicleId,
                CustomerTierId = customer.TierId,
                EligibleAmount = eligibleAmount,
                TierDiscountAmount = 0m,
                OrderType = ChatbotVoucherOrderType.BOOKING,
                ServiceIds = serviceIds,
                ProductIds = Array.Empty<long>()
            },
            ct);

        return resolution.IsApplied ? resolution.Voucher : null;
    }

    public bool IsBookingConfirmationMessage(string message)
    {
        var n = ChatbotBookingNlpHelper.NormalizeNlp(message);
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

    public DateTime NormalizeBookingDateTime(DateTime value)
    {
        return VietnamTime.ToVietnamTime(value);
    }

    public string BuildFinalizeErrorMessage(string? rawMessage)
    {
        var normalized = ChatbotBookingNlpHelper.NormalizeNlp(rawMessage);
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

    private async Task<ChatbotBookingResultDto> BookSingleAsync(
        Customer customer,
        long branchId,
        long vehicleId,
        long serviceId,
        DateTime start,
        string? selectedSlotEndUtc,
        string? slotHoldToken)
    {
        var end = DateTime.TryParse(selectedSlotEndUtc, null, DateTimeStyles.RoundtripKind, out var parsed)
            ? NormalizeBookingDateTime(parsed)
            : start.AddMinutes(60);
        if (end <= start)
        {
            end = start.AddMinutes(60);
        }

        var dto = new BookAppointmentRequestDto
        {
            BranchId = branchId,
            VehicleId = vehicleId,
            StartTime = start,
            EndTime = end,
            BookingFlow = "FIXED",
            RequestedServiceIds = serviceId.ToString(CultureInfo.InvariantCulture),
            CustomerFullName = customer.FullName,
            CustomerPhone = customer.Phone,
            SlotHoldToken = slotHoldToken
        };
        var appointment = await _appointmentBookingService.BookAsync(customer.Id, dto);
        return new ChatbotBookingResultDto
        {
            AppointmentId = appointment.Id,
            Message = $"Đặt lịch thành công. Mã lịch hẹn: {appointment.Id}."
        };
    }

    private async Task<ChatbotBookingResultDto> BookGroupAsync(
        Customer customer,
        long branchId,
        long vehicleId,
        IReadOnlyList<long> serviceIds,
        DateTime start,
        string? slotHoldToken,
        CancellationToken ct)
    {
        var map = await _store.GetServiceDurationsAsync(serviceIds, ct);
        var cursor = start;
        var items = new List<BookAppointmentRequestDto>();
        foreach (var serviceId in serviceIds)
        {
            var end = cursor.AddMinutes(map.TryGetValue(serviceId, out var m) ? m : 60);
            items.Add(new BookAppointmentRequestDto
            {
                BranchId = branchId,
                VehicleId = vehicleId,
                StartTime = cursor,
                EndTime = end,
                BookingFlow = "FIXED",
                RequestedServiceIds = serviceId.ToString(CultureInfo.InvariantCulture),
                CustomerFullName = customer.FullName,
                CustomerPhone = customer.Phone,
                SlotHoldToken = items.Count == 0 ? slotHoldToken : null
            });
            cursor = end;
        }

        var batch = new BookAppointmentBatchRequestDto
        {
            BranchId = branchId,
            CustomerNote = "Created by chatbot sequential booking workflow.",
            Items = items
        };
        var group = await _appointmentBookingService.BookBatchForCustomerAsync(customer.Id, customer.Id, batch);
        return new ChatbotBookingResultDto
        {
            BookingGroupId = group.Id,
            Message = $"Đặt lịch nhóm thành công. Mã nhóm lịch hẹn: {group.Id}."
        };
    }

    private async Task<decimal> ResolveBookingEligibleAmountAsync(IReadOnlyList<long> serviceIds, CancellationToken ct)
    {
        var normalizedIds = serviceIds.Where(id => id > 0).Distinct().ToList();
        if (normalizedIds.Count == 0)
        {
            return 0m;
        }

        return await _store.GetServicesBasePriceSumAsync(normalizedIds, ct);
    }
}
