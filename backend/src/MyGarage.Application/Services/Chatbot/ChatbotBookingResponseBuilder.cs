using System.Globalization;
using System.Text.Json.Nodes;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Services;

internal static class ChatbotBookingResponseBuilder
{
    public static string? Warning(int? noShowCount) =>
        noShowCount.HasValue && noShowCount.Value >= 2
            ? $"Cảnh báo: tài khoản có {noShowCount.Value} lần no-show gần đây."
            : null;

    public static JsonObject ParseRoot(string? stateJson)
    {
        try
        {
            return string.IsNullOrWhiteSpace(stateJson)
                ? new JsonObject()
                : JsonNode.Parse(stateJson) as JsonObject ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }

    public static ChatbotBookingWorkflowResponseDto Build(
        JsonObject root,
        ChatbotBookingState state,
        DateTime utcNow,
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
        root["flow"] = new JsonObject
        {
            ["intent"] = nameof(ChatbotIntentType.BOOKING),
            ["step"] = state.Step.ToLabel(),
            ["updated_at"] = DateTime.UtcNow
        };
        var finalPrompt = requiresUserInput && state.Step != ChatbotBookingStep.Completed
            ? $"{prompt}{Environment.NewLine}{Environment.NewLine}Lệnh nhanh: `quay lại`, `đổi xe`, `đổi dịch vụ`, `đổi ngày`, `đặt lịch mới`, `hủy`."
            : prompt;
        var holdExpiresAtUtc = DateTime.TryParse(state.SlotHoldExpiresAtUtc, null, DateTimeStyles.RoundtripKind, out var holdExpires)
            ? holdExpires.ToUniversalTime()
            : (DateTime?)null;
        var holdSecondsRemaining = holdExpiresAtUtc.HasValue
            ? Math.Max(0, (int)Math.Ceiling((holdExpiresAtUtc.Value - utcNow).TotalSeconds))
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
        ChatbotBookingState state,
        IReadOnlyList<ChatbotBookingVehicleOptionDto> vehicleOptions,
        IReadOnlyList<ChatbotBookingServiceCategoryOptionDto> serviceCategoryOptions,
        IReadOnlyList<ChatbotBookingServiceOptionDto> serviceOptions,
        IReadOnlyList<ChatbotBookingBranchOptionDto> branchOptions,
        IReadOnlyList<ChatbotBookingSlotOptionDto> slotOptions)
    {
        var quickReplies = new List<ChatbotQuickReplyOptionDto>();
        switch (state.Step)
        {
            case ChatbotBookingStep.IdentifyOrCreateVehicle:
                for (var i = 0; i < Math.Min(4, vehicleOptions.Count); i++)
                {
                    AddQuickReply(quickReplies, $"Xe {i + 1}", $"xe {i + 1}");
                }
                AddQuickReply(quickReplies, "Hủy", "huy");
                break;

            case ChatbotBookingStep.SelectServices:
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
                    for (var i = 0; i < serviceOptions.Count; i++)
                    {
                        var option = serviceOptions[i];
                        var label = BuildServiceQuickReplyLabel(option, i + 1);
                        var value = BuildServiceQuickReplyValue(option, i + 1);
                        AddQuickReply(quickReplies, label, value);
                    }
                    AddQuickReply(quickReplies, "Đổi dịch vụ", "doi dich vu");
                    AddQuickReply(quickReplies, "Quay lại", "quay lai");
                    AddQuickReply(quickReplies, "Đặt lịch mới", "dat lich moi");
                    AddQuickReply(quickReplies, "Hủy", "huy");
                }
                break;

            case ChatbotBookingStep.SelectBranch:
                for (var i = 0; i < Math.Min(4, branchOptions.Count); i++)
                {
                    var branch = branchOptions[i];
                    AddQuickReply(quickReplies, branch.BranchName, branch.BranchName);
                }
                AddQuickReply(quickReplies, "Quay lại", "quay lai");
                AddQuickReply(quickReplies, "Đổi dịch vụ", "doi dich vu");
                AddQuickReply(quickReplies, "Đặt lịch mới", "dat lich moi");
                AddQuickReply(quickReplies, "Hủy", "huy");
                break;

            case ChatbotBookingStep.ComputeAvailableSlots:
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

            case ChatbotBookingStep.FinalizeBooking:
                AddQuickReply(quickReplies, "Đồng ý", "dong y");
                AddQuickReply(quickReplies, "Đổi ngày", "doi ngay");
                AddQuickReply(quickReplies, "Quay lại", "quay lai");
                AddQuickReply(quickReplies, "Đặt lịch mới", "dat lich moi");
                AddQuickReply(quickReplies, "Hủy", "huy");
                break;

            case ChatbotBookingStep.Completed:
                AddQuickReply(quickReplies, "Đặt lịch mới", "dat lich");
                break;
        }

        if (state.Step != ChatbotBookingStep.Completed)
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

    private static string BuildServiceQuickReplyLabel(ChatbotBookingServiceOptionDto option, int ordinal)
    {
        var name = option.ServiceName?.Trim();
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Length > 36 ? $"{name[..33]}..." : name;
        }

        if (!string.IsNullOrWhiteSpace(option.ServiceCode))
        {
            return option.ServiceCode.Trim();
        }

        return $"Dịch vụ {ordinal}";
    }

    private static string BuildServiceQuickReplyValue(ChatbotBookingServiceOptionDto option, int ordinal)
    {
        if (!string.IsNullOrWhiteSpace(option.ServiceCode))
        {
            return option.ServiceCode.Trim();
        }

        if (option.ServiceId > 0)
        {
            return option.ServiceId.ToString();
        }

        return $"dich vu {ordinal}";
    }
}

internal static class ChatbotBookingStepLabel
{
    public static string ToLabel(this ChatbotBookingStep step) => step switch
    {
        ChatbotBookingStep.IdentifyOrCreateVehicle => "IDENTIFY_OR_CREATE_VEHICLE",
        ChatbotBookingStep.SelectServices => "SELECT_SERVICES",
        ChatbotBookingStep.SelectBranch => "SELECT_BRANCH",
        ChatbotBookingStep.ComputeAvailableSlots => "COMPUTE_AVAILABLE_SLOTS",
        ChatbotBookingStep.FinalizeBooking => "FINALIZE_BOOKING",
        ChatbotBookingStep.Completed => "COMPLETED",
        _ => "IDENTIFY_OR_CREATE_VEHICLE"
    };
}
