using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Requests.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Services;

/// <summary>
/// Provides appointment slot lookup, booking session state, and booking confirmation tools.
/// </summary>
public sealed class ChatbotBookingToolProvider : IChatbotToolDefinitionProvider
{
    private const string GetAvailableSlotsTool = "get_available_slots";
    private const string ReadBookingStateTool = "read_booking_state";
    private const string UpdateBookingStateTool = "update_booking_state";
    private const string CreateAppointmentDraftTool = "create_appointment_draft";
    private const string ConfirmAppointmentBookingTool = "confirm_appointment_booking";

    private static readonly IReadOnlyList<ChatbotToolDefinition> Definitions =
    [
        new ChatbotToolDefinition
        {
            ToolName = GetAvailableSlotsTool,
            Description = "Get bookable slots by branch/date/services.",
            JsonSchema =
                """
                {
                  "type": "object",
                  "properties": {
                    "branchId": { "type": "integer" },
                    "date": { "type": "string", "description": "yyyy-MM-dd" },
                    "serviceIds": { "type": "array", "items": { "type": "integer" } },
                    "vehicleCount": { "type": "integer", "minimum": 1, "maximum": 10 },
                    "isParallel": { "type": "boolean" },
                    "bookingFlow": { "type": "string" },
                    "limit": { "type": "integer", "minimum": 1, "maximum": 20 }
                  },
                  "required": ["branchId", "date"],
                  "additionalProperties": false
                }
                """
        },
        new ChatbotToolDefinition
        {
            ToolName = ReadBookingStateTool,
            Description = "Read current booking state from chatbot session.",
            JsonSchema =
                """
                {
                  "type": "object",
                  "properties": {},
                  "additionalProperties": false
                }
                """
        },
        new ChatbotToolDefinition
        {
            ToolName = UpdateBookingStateTool,
            Description = "Patch booking state fields in chatbot session.",
            JsonSchema =
                """
                {
                  "type": "object",
                  "properties": {
                    "reset": { "type": "boolean" },
                    "vehicleId": { "type": ["integer", "null"] },
                    "branchId": { "type": ["integer", "null"] },
                    "serviceIds": { "type": ["array", "null"], "items": { "type": "integer" } },
                    "preferredDate": { "type": ["string", "null"] },
                    "preferredStartTimeUtc": { "type": ["string", "null"] },
                    "preferredEndTimeUtc": { "type": ["string", "null"] }
                  },
                  "additionalProperties": false
                }
                """
        },
        new ChatbotToolDefinition
        {
            ToolName = CreateAppointmentDraftTool,
            Description = "Validate whether appointment can be booked. No DB write.",
            JsonSchema =
                """
                {
                  "type": "object",
                  "properties": {
                    "branchId": { "type": "integer" },
                    "vehicleId": { "type": "integer" },
                    "serviceIds": { "type": "array", "items": { "type": "integer" } },
                    "startTimeUtc": { "type": "string" },
                    "endTimeUtc": { "type": "string" },
                    "customerNote": { "type": "string" }
                  },
                  "additionalProperties": false
                }
                """
        },
        new ChatbotToolDefinition
        {
            ToolName = ConfirmAppointmentBookingTool,
            Description = "Create appointment/group booking with validated data.",
            JsonSchema =
                """
                {
                  "type": "object",
                  "properties": {
                    "branchId": { "type": "integer" },
                    "vehicleId": { "type": "integer" },
                    "serviceIds": { "type": "array", "items": { "type": "integer" } },
                    "startTimeUtc": { "type": "string" },
                    "endTimeUtc": { "type": "string" },
                    "customerNote": { "type": "string" }
                  },
                  "additionalProperties": false
                }
                """
        }
    ];

    private static readonly HashSet<string> SupportedTools = Definitions
        .Select(x => x.ToolName)
        .ToHashSet(StringComparer.Ordinal);

    private readonly IAppointmentAvailabilityQueryService _appointmentAvailabilityService;
    private readonly IAppointmentBookingCommandService _appointmentBookingService;
    private readonly IChatbotService _chatbotService;
    private readonly IChatbotStore _store;

    public ChatbotBookingToolProvider(
        IAppointmentAvailabilityQueryService appointmentAvailabilityService,
        IAppointmentBookingCommandService appointmentBookingService,
        IChatbotService chatbotService,
        IChatbotStore store)
    {
        _appointmentAvailabilityService = appointmentAvailabilityService;
        _appointmentBookingService = appointmentBookingService;
        _chatbotService = chatbotService;
        _store = store;
    }

    public IReadOnlyList<ChatbotToolDefinition> GetToolDefinitions() => Definitions;

    public bool CanExecute(string normalizedToolName) => SupportedTools.Contains(normalizedToolName);

    public Task<object> ExecutePayloadAsync(
        ChatbotToolExecutionRequest request,
        string normalizedToolName,
        JsonObject argsRoot,
        CancellationToken cancellationToken = default)
    {
        return normalizedToolName switch
        {
            GetAvailableSlotsTool => ExecuteGetAvailableSlotsPayloadAsync(argsRoot),
            ReadBookingStateTool => ExecuteReadBookingStatePayloadAsync(request, cancellationToken),
            UpdateBookingStateTool => ExecuteUpdateBookingStatePayloadAsync(request, argsRoot, cancellationToken),
            CreateAppointmentDraftTool => ExecuteCreateAppointmentDraftPayloadAsync(argsRoot),
            ConfirmAppointmentBookingTool => ExecuteConfirmAppointmentBookingPayloadAsync(request, argsRoot, cancellationToken),
            _ => throw new ChatbotToolExecutionException("TOOL_NOT_IMPLEMENTED", "Tool is allowlisted but no executor is registered.")
        };
    }

    private async Task<object> ExecuteGetAvailableSlotsPayloadAsync(JsonObject argsRoot)
    {
        var branchId = ChatbotToolJsonHelper.ReadLong(argsRoot, "branchId");
        var dateText = ChatbotToolJsonHelper.ReadString(argsRoot, "date");
        if (!branchId.HasValue || branchId.Value <= 0)
        {
            throw new ChatbotToolExecutionException("INVALID_TOOL_ARGS", "branchId is required.");
        }

        if (!DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new ChatbotToolExecutionException("INVALID_TOOL_ARGS", "date must be in yyyy-MM-dd format.");
        }

        var serviceIds = ChatbotToolJsonHelper.ReadLongArray(argsRoot, "serviceIds");
        var requestedServiceIds = serviceIds.Count == 0 ? null : string.Join(",", serviceIds);
        var vehicleCount = Math.Clamp(ChatbotToolJsonHelper.ReadInt(argsRoot, "vehicleCount") ?? 1, 1, 10);
        var isParallel = ChatbotToolJsonHelper.ReadBool(argsRoot, "isParallel") ?? false;
        var bookingFlow = ChatbotToolJsonHelper.ReadString(argsRoot, "bookingFlow") ?? "FIXED";
        var limit = Math.Clamp(ChatbotToolJsonHelper.ReadInt(argsRoot, "limit") ?? 10, 1, 20);

        var slots = await _appointmentAvailabilityService.GetAvailableSlotsAsync(
            branchId.Value,
            date,
            requestedServiceIds,
            requestedPackageIds: null,
            bookingFlow: bookingFlow,
            vehicleCount: vehicleCount,
            isParallel: isParallel);

        var result = slots
            .Where(x => x.IsAvailable && x.RemainingCapacity > 0)
            .OrderBy(x => x.StartTime)
            .Take(limit)
            .Select(x => new { startTime = x.StartTime, endTime = x.EndTime, remainingCapacity = x.RemainingCapacity, maxCapacity = x.MaxCapacity })
            .ToList();

        return new { date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), count = result.Count, slots = result };
    }

    private async Task<object> ExecuteReadBookingStatePayloadAsync(
        ChatbotToolExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var session = await _chatbotService.GetSessionByIdAsync(request.CustomerId, request.SessionId, cancellationToken)
            ?? throw new ChatbotToolExecutionException("SESSION_NOT_FOUND", "Session not found.");
        var stateRoot = ChatbotToolJsonHelper.ParseIntentStateRoot(session.IntentState);
        var booking = stateRoot["booking"] as JsonObject ?? new JsonObject();
        return new
        {
            step = ChatbotToolJsonHelper.ReadString(booking, "Step") ?? ChatbotToolJsonHelper.ReadString(booking, "step"),
            vehicleId = ChatbotToolJsonHelper.ReadLong(booking, "VehicleId") ?? ChatbotToolJsonHelper.ReadLong(booking, "vehicleId"),
            branchId = ChatbotToolJsonHelper.ReadLong(booking, "BranchId") ?? ChatbotToolJsonHelper.ReadLong(booking, "branchId"),
            serviceIds = ChatbotToolJsonHelper.ReadLongArray(booking, "ServiceIds").Concat(ChatbotToolJsonHelper.ReadLongArray(booking, "serviceIds")).Distinct().ToArray(),
            bookingDate = ChatbotToolJsonHelper.ReadString(booking, "BookingDate") ?? ChatbotToolJsonHelper.ReadString(booking, "bookingDate"),
            selectedSlotStartUtc = ChatbotToolJsonHelper.ReadString(booking, "SelectedSlotStartUtc") ?? ChatbotToolJsonHelper.ReadString(booking, "selectedSlotStartUtc"),
            selectedSlotEndUtc = ChatbotToolJsonHelper.ReadString(booking, "SelectedSlotEndUtc") ?? ChatbotToolJsonHelper.ReadString(booking, "selectedSlotEndUtc")
        };
    }

    private async Task<object> ExecuteUpdateBookingStatePayloadAsync(
        ChatbotToolExecutionRequest request,
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var session = await _chatbotService.GetSessionByIdAsync(request.CustomerId, request.SessionId, cancellationToken)
            ?? throw new ChatbotToolExecutionException("SESSION_NOT_FOUND", "Session not found.");
        var root = ChatbotToolJsonHelper.ParseIntentStateRoot(session.IntentState);
        if (ChatbotToolJsonHelper.ReadBool(argsRoot, "reset") == true)
        {
            root["booking"] = new JsonObject();
        }

        var booking = root["booking"] as JsonObject ?? new JsonObject();
        if (argsRoot.ContainsKey("vehicleId")) booking["VehicleId"] = ChatbotToolJsonHelper.ReadLong(argsRoot, "vehicleId");
        if (argsRoot.ContainsKey("branchId")) booking["BranchId"] = ChatbotToolJsonHelper.ReadLong(argsRoot, "branchId");
        if (argsRoot.ContainsKey("serviceIds")) booking["ServiceIds"] = new JsonArray(ChatbotToolJsonHelper.ReadLongArray(argsRoot, "serviceIds").Select(x => JsonValue.Create(x)).ToArray());
        if (argsRoot.ContainsKey("preferredDate")) booking["BookingDate"] = ChatbotToolJsonHelper.ReadString(argsRoot, "preferredDate");
        if (argsRoot.ContainsKey("preferredStartTimeUtc")) booking["SelectedSlotStartUtc"] = ChatbotToolJsonHelper.ReadString(argsRoot, "preferredStartTimeUtc");
        if (argsRoot.ContainsKey("preferredEndTimeUtc")) booking["SelectedSlotEndUtc"] = ChatbotToolJsonHelper.ReadString(argsRoot, "preferredEndTimeUtc");

        root["booking"] = booking;
        root["flow"] = new JsonObject
        {
            ["intent"] = nameof(ChatbotIntentType.BOOKING),
            ["updated_at"] = DateTime.UtcNow
        };

        await _chatbotService.UpdateSessionStateAsync(
            request.CustomerId,
            request.SessionId,
            new ChatbotSessionStateUpdateRequestDto
            {
                CurrentIntent = nameof(ChatbotIntentType.BOOKING),
                IntentState = root.ToJsonString()
            },
            cancellationToken);

        return await ExecuteReadBookingStatePayloadAsync(request, cancellationToken);
    }

    private async Task<object> ExecuteCreateAppointmentDraftPayloadAsync(JsonObject argsRoot)
    {
        var branchId = ChatbotToolJsonHelper.ReadLong(argsRoot, "branchId");
        var vehicleId = ChatbotToolJsonHelper.ReadLong(argsRoot, "vehicleId");
        var serviceIds = ChatbotToolJsonHelper.ReadLongArray(argsRoot, "serviceIds");
        var startTime = ChatbotToolJsonHelper.ReadDateTime(argsRoot, "startTimeUtc");
        var endTime = ChatbotToolJsonHelper.ReadDateTime(argsRoot, "endTimeUtc") ?? startTime?.AddMinutes(60);

        var missing = new List<string>();
        if (!branchId.HasValue || branchId.Value <= 0) missing.Add("branchId");
        if (!vehicleId.HasValue || vehicleId.Value <= 0) missing.Add("vehicleId");
        if (serviceIds.Count == 0) missing.Add("serviceIds");
        if (!startTime.HasValue) missing.Add("startTimeUtc");

        if (missing.Count > 0)
        {
            return new { canProceed = false, reasonCode = "MISSING_REQUIRED_FIELDS", missingFields = missing };
        }

        var start = startTime!.Value;
        var resolvedBranchId = branchId!.Value;
        var date = DateOnly.FromDateTime(VietnamTime.ToVietnamTime(start));
        var slots = await _appointmentAvailabilityService.GetAvailableSlotsAsync(
            resolvedBranchId,
            date,
            string.Join(",", serviceIds),
            requestedPackageIds: null,
            bookingFlow: "FIXED",
            vehicleCount: 1,
            isParallel: false);
        var target = slots.FirstOrDefault(x =>
            x.IsAvailable &&
            x.RemainingCapacity > 0 &&
            x.StartTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) == start.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

        return new
        {
            canProceed = target is not null,
            reasonCode = target is null ? "SLOT_UNAVAILABLE" : null,
            preview = target is null ? null : new { branchId, vehicleId, serviceIds, startTimeUtc = startTime, endTimeUtc = endTime }
        };
    }

    private async Task<object> ExecuteConfirmAppointmentBookingPayloadAsync(
        ChatbotToolExecutionRequest request,
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var draft = await ExecuteCreateAppointmentDraftPayloadAsync(argsRoot);
        var draftNode = JsonSerializer.SerializeToNode(draft) as JsonObject;
        if (draftNode?["canProceed"]?.GetValue<bool>() != true)
        {
            return new { confirmed = false, draft };
        }

        var branchId = ChatbotToolJsonHelper.ReadLong(argsRoot, "branchId")!.Value;
        var vehicleId = ChatbotToolJsonHelper.ReadLong(argsRoot, "vehicleId")!.Value;
        var serviceIds = ChatbotToolJsonHelper.ReadLongArray(argsRoot, "serviceIds");
        var startTime = ChatbotToolJsonHelper.ReadDateTime(argsRoot, "startTimeUtc")!.Value;
        var endTime = ChatbotToolJsonHelper.ReadDateTime(argsRoot, "endTimeUtc") ?? startTime.AddMinutes(60);
        var customerNote = ChatbotToolJsonHelper.ReadString(argsRoot, "customerNote");

        var customer = await _store.GetCustomerContactAsync(request.CustomerId, cancellationToken)
            ?? throw new ChatbotToolExecutionException("CUSTOMER_NOT_FOUND", "Authenticated customer not found.");

        var created = await _appointmentBookingService.BookAsync(
            request.CustomerId,
            new BookAppointmentRequestDto
            {
                BranchId = branchId,
                VehicleId = vehicleId,
                StartTime = startTime,
                EndTime = endTime,
                BookingFlow = "FIXED",
                RequestedServiceIds = string.Join(",", serviceIds),
                CustomerFullName = customer.FullName,
                CustomerPhone = customer.Phone,
                CustomerNotes = customerNote
            });

        return new
        {
            confirmed = true,
            appointmentId = created.Id,
            message = $"Appointment booked successfully. AppointmentId={created.Id}."
        };
    }
}
