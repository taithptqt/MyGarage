using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MyGarage.DTOs.Responses.Chatbot;

public class ChatbotBookingWorkflowResponseDto
{
    public string Step { get; set; } = "IDENTIFY_OR_CREATE_VEHICLE";

    public bool RequiresUserInput { get; set; }

    public string Prompt { get; set; } = string.Empty;

    public string? NoShowWarning { get; set; }

    public IReadOnlyList<ChatbotBookingVehicleOptionDto> VehicleOptions { get; set; } = Array.Empty<ChatbotBookingVehicleOptionDto>();

    public IReadOnlyList<ChatbotBookingServiceOptionDto> ServiceOptions { get; set; } = Array.Empty<ChatbotBookingServiceOptionDto>();

    public IReadOnlyList<ChatbotBookingServiceCategoryOptionDto> ServiceCategoryOptions { get; set; } = Array.Empty<ChatbotBookingServiceCategoryOptionDto>();

    public IReadOnlyList<ChatbotBookingBranchOptionDto> BranchOptions { get; set; } = Array.Empty<ChatbotBookingBranchOptionDto>();

    public IReadOnlyList<ChatbotBookingSlotOptionDto> SlotOptions { get; set; } = Array.Empty<ChatbotBookingSlotOptionDto>();

    public IReadOnlyList<ChatbotQuickReplyOptionDto> QuickReplies { get; set; } = Array.Empty<ChatbotQuickReplyOptionDto>();

    [JsonIgnore]
    public string? SlotHoldToken { get; set; }

    public DateTime? SlotHoldExpiresAtUtc { get; set; }

    public int? SlotHoldSecondsRemaining { get; set; }

    public ChatbotBookingResultDto? BookingResult { get; set; }

    public ChatbotVoucherResolutionDto? VoucherSuggestion { get; set; }

    [JsonIgnore]
    public string UpdatedIntentState { get; set; } = "{}";
}

public class ChatbotQuickReplyOptionDto
{
    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

public class ChatbotBookingServiceCategoryOptionDto
{
    public long CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;
}

public class ChatbotBookingVehicleOptionDto
{
    public long VehicleId { get; set; }

    public string LicensePlate { get; set; } = string.Empty;

    public string? VehicleModelName { get; set; }
}

public class ChatbotBookingServiceOptionDto
{
    public long ServiceId { get; set; }

    public string ServiceCode { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;
}

public class ChatbotBookingBranchOptionDto
{
    public long BranchId { get; set; }

    public string BranchName { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string? Hotline { get; set; }
}

public class ChatbotBookingSlotOptionDto
{
    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int RemainingCapacity { get; set; }

    public int MaxCapacity { get; set; }
}

public class ChatbotBookingResultDto
{
    public long? AppointmentId { get; set; }

    public long? BookingGroupId { get; set; }

    public string Message { get; set; } = string.Empty;
}
