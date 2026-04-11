using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MyGarage.DTOs.Responses.Chatbot;

public class OrchestratedChatbotReplyDto
{
    public long SessionId { get; set; }

    public string SessionToken { get; set; } = null!;

    public string Intent { get; set; } = null!;

    public decimal Confidence { get; set; }

    public string Strategy { get; set; } = null!;

    public bool IsRejected { get; set; }

    public string? RejectionReason { get; set; }

    public bool ShouldHandoff { get; set; }

    public string AssistantReply { get; set; } = null!;

    public ChatbotBookingWorkflowResponseDto? BookingWorkflow { get; set; }

    [JsonIgnore]
    public string UpdatedIntentState { get; set; } = "{}";

    [JsonIgnore]
    public IReadOnlyList<OrchestratedFunctionCallDto> FunctionCalls { get; set; } = Array.Empty<OrchestratedFunctionCallDto>();

    public DateTime OrchestratedAt { get; set; }
}

public class OrchestratedFunctionCallDto
{
    [JsonIgnore]
    public string ToolName { get; set; } = null!;

    [JsonIgnore]
    public string ToolArgs { get; set; } = "{}";
}
