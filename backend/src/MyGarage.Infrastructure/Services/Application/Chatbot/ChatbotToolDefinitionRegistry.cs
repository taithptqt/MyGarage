using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyGarage.DTOs.Requests.Chatbot;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed partial class ChatbotToolDefinitionRegistry : IChatbotToolDefinitionRegistry
{
    private const string PricingInventoryLookupTool = "product_pricing_inventory_lookup";
    private const string GetCustomerBenefitsTool = "get_customer_benefits";
    private const string GetCustomerVouchersTool = "get_customer_vouchers";
    private const string ListCustomerVehiclesTool = "list_customer_vehicles";
    private const string GetBranchListTool = "get_branch_list";
    private const string SearchStandardServicesTool = "search_standard_services";
    private const string GetAvailableSlotsTool = "get_available_slots";
    private const string ReadBookingStateTool = "read_booking_state";
    private const string UpdateBookingStateTool = "update_booking_state";
    private const string CreateAppointmentDraftTool = "create_appointment_draft";
    private const string ConfirmAppointmentBookingTool = "confirm_appointment_booking";
    private const string GetCustomerServiceHistoryTool = "get_customer_service_history";
    private const string GetCustomerServiceOrderStatusTool = "get_customer_service_order_status";
    private const string GetCustomerInvoicePaymentStatusTool = "get_customer_invoice_payment_status";
    private const string GetCustomerWarrantyHistoryTool = "get_customer_warranty_history";
    private const string CreateServiceOrderDraftTool = "create_service_order_draft";
    private const string CreateRetailOrderDraftTool = "create_retail_order_draft";

    private static readonly IReadOnlyList<ChatbotToolDefinition> Definitions =
    [
        new ChatbotToolDefinition
        {
            ToolName = PricingInventoryLookupTool,
            Description = "Lookup product pricing and stock from authoritative MyGarage records. Mandatory for parts/product price or stock answers.",
            JsonSchema =
                """
                {
                  "type": "object",
                  "properties": {
                    "productHint": {
                      "type": "string",
                      "description": "SKU or product name hint."
                    }
                  },
                  "required": ["productHint"],
                  "additionalProperties": false
                }
                """
        },
        new ChatbotToolDefinition
        {
            ToolName = GetCustomerBenefitsTool,
            Description = "Get authenticated customer benefits including tier and voucher summary.",
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
            ToolName = GetCustomerVouchersTool,
            Description = "Get authenticated customer vouchers from database records.",
            JsonSchema =
                """
                {
                  "type": "object",
                  "properties": {
                    "includeUpcoming": { "type": "boolean" }
                  },
                  "additionalProperties": false
                }
                """
        },
        new ChatbotToolDefinition
        {
            ToolName = ListCustomerVehiclesTool,
            Description = "List vehicles of authenticated customer.",
            JsonSchema =
                """
                {
                  "type": "object",
                  "properties": {
                    "includeInactiveOwnership": { "type": "boolean" }
                  },
                  "additionalProperties": false
                }
                """
        },
        new ChatbotToolDefinition
        {
            ToolName = GetBranchListTool,
            Description = "Get branch list from database.",
            JsonSchema =
                """
                {
                  "type": "object",
                  "properties": {
                    "onlyActive": { "type": "boolean" }
                  },
                  "additionalProperties": false
                }
                """
        },
        new ChatbotToolDefinition
        {
            ToolName = SearchStandardServicesTool,
            Description = "Search active standard services by code/name and return basePrice from database.",
            JsonSchema =
                """
                {
                  "type": "object",
                  "properties": {
                    "keyword": { "type": "string" },
                    "limit": { "type": "integer", "minimum": 1, "maximum": 30 }
                  },
                  "additionalProperties": false
                }
                """
        },
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
        },
        new ChatbotToolDefinition
        {
            ToolName = GetCustomerServiceHistoryTool,
            Description = "Get authenticated customer service order history.",
            JsonSchema =
                """
                {
                  "type": "object",
                  "properties": {
                    "limit": { "type": "integer", "minimum": 1, "maximum": 20 }
                  },
                  "additionalProperties": false
                }
                """
        },
        new ChatbotToolDefinition
        {
            ToolName = GetCustomerServiceOrderStatusTool,
            Description = "Get authenticated customer service order progress/status.",
            JsonSchema =
                """
                {
                  "type": "object",
                  "properties": {
                    "orderId": { "type": "integer" },
                    "orderCode": { "type": "string" },
                    "limit": { "type": "integer", "minimum": 1, "maximum": 20 }
                  },
                  "additionalProperties": false
                }
                """
        },
        new ChatbotToolDefinition
        {
            ToolName = GetCustomerInvoicePaymentStatusTool,
            Description = "Get authenticated customer invoice/payment status.",
            JsonSchema =
                """
                {
                  "type": "object",
                  "properties": {
                    "invoiceId": { "type": "integer" },
                    "orderCode": { "type": "string" },
                    "limit": { "type": "integer", "minimum": 1, "maximum": 20 }
                  },
                  "additionalProperties": false
                }
                """
        },
        new ChatbotToolDefinition
        {
            ToolName = GetCustomerWarrantyHistoryTool,
            Description = "Get authenticated customer warranty-related history from service records.",
            JsonSchema =
                """
                {
                  "type": "object",
                  "properties": {
                    "vehicleId": { "type": "integer" },
                    "limit": { "type": "integer", "minimum": 1, "maximum": 20 }
                  },
                  "additionalProperties": false
                }
                """
        },
        new ChatbotToolDefinition
        {
            ToolName = CreateServiceOrderDraftTool,
            Description = "Create a non-persistent service order draft preview for authenticated customer.",
            JsonSchema =
                """
                {
                  "type": "object",
                  "properties": {
                    "vehicleId": { "type": "integer" },
                    "originBranchId": { "type": "integer" },
                    "executeBranchId": { "type": "integer" },
                    "serviceIds": { "type": "array", "items": { "type": "integer" } },
                    "customerNote": { "type": "string" }
                  },
                  "additionalProperties": false
                }
                """
        },
        new ChatbotToolDefinition
        {
            ToolName = CreateRetailOrderDraftTool,
            Description = "Create a non-persistent retail order draft preview for authenticated customer.",
            JsonSchema =
                """
                {
                  "type": "object",
                  "properties": {
                    "branchId": { "type": "integer" },
                    "customerNote": { "type": "string" },
                    "items": {
                      "type": "array",
                      "items": {
                        "type": "object",
                        "properties": {
                          "productId": { "type": "integer" },
                          "quantity": { "type": "number", "minimum": 0.01 }
                        },
                        "required": ["productId", "quantity"],
                        "additionalProperties": false
                      }
                    }
                  },
                  "additionalProperties": false
                }
                """
        }
    ];

    private readonly IChatbotToolAllowlist _toolAllowlist;
    private readonly IChatbotPricingInventoryGuard _pricingInventoryGuard;
    private readonly IChatbotVoucherStatusLookupService _voucherStatusLookupService;
    private readonly IChatbotLoyaltyStatusLookupService _loyaltyStatusLookupService;
    private readonly IAppointmentService _appointmentService;
    private readonly IChatbotService _chatbotService;
    private readonly IGarageContext _context;
    private readonly ILogger<ChatbotToolDefinitionRegistry> _logger;

    public ChatbotToolDefinitionRegistry(
        IChatbotToolAllowlist toolAllowlist,
        IChatbotPricingInventoryGuard pricingInventoryGuard,
        IChatbotVoucherStatusLookupService voucherStatusLookupService,
        IChatbotLoyaltyStatusLookupService loyaltyStatusLookupService,
        IAppointmentService appointmentService,
        IChatbotService chatbotService,
        IGarageContext context,
        ILogger<ChatbotToolDefinitionRegistry> logger)
    {
        _toolAllowlist = toolAllowlist;
        _pricingInventoryGuard = pricingInventoryGuard;
        _voucherStatusLookupService = voucherStatusLookupService;
        _loyaltyStatusLookupService = loyaltyStatusLookupService;
        _appointmentService = appointmentService;
        _chatbotService = chatbotService;
        _context = context;
        _logger = logger;
    }

    public IReadOnlyList<ChatbotToolDefinition> GetToolDefinitions()
    {
        return Definitions
            .Where(x => _toolAllowlist.IsAllowedForLlm(x.ToolName))
            .ToList();
    }

    public async Task<ChatbotToolExecutionResult> ExecuteAsync(
        ChatbotToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var toolName = request.ToolCall.ToolName?.Trim() ?? string.Empty;
        var normalizedToolName = toolName.ToLowerInvariant();
        var toolArgs = request.ToolCall.ToolArgs?.Trim();
        var normalizedArgs = string.IsNullOrWhiteSpace(toolArgs) ? "{}" : toolArgs;

        if (!_toolAllowlist.IsAllowed(toolName))
        {
            return BuildFailure(
                toolName,
                normalizedArgs,
                request.ToolCall.ToolCallId,
                "TOOL_NOT_ALLOWED",
                "Tool is not allowlisted for chatbot execution.");
        }

        JsonObject argsRoot;
        try
        {
            argsRoot = JsonNode.Parse(normalizedArgs) as JsonObject ?? new JsonObject();
        }
        catch
        {
            return BuildFailure(
                toolName,
                normalizedArgs,
                request.ToolCall.ToolCallId,
                "INVALID_TOOL_ARGS",
                "Tool arguments must be a valid JSON object.");
        }

        try
        {
            var payload = await ExecuteToolPayloadAsync(
                request,
                normalizedToolName,
                argsRoot,
                cancellationToken);
            return new ChatbotToolExecutionResult
            {
                Succeeded = true,
                ToolName = toolName,
                ToolArgs = normalizedArgs,
                ToolResult = JsonSerializer.Serialize(payload),
                ToolCallId = request.ToolCall.ToolCallId
            };
        }
        catch (ToolExecutionException ex)
        {
            return BuildFailure(
                toolName,
                normalizedArgs,
                request.ToolCall.ToolCallId,
                ex.Code,
                ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Chatbot tool execution crashed. SessionId={SessionId}, CustomerId={CustomerId}, Tool={ToolName}",
                request.SessionId,
                request.CustomerId,
                toolName);
            return BuildFailure(
                toolName,
                normalizedArgs,
                request.ToolCall.ToolCallId,
                "TOOL_EXECUTION_ERROR",
                ex.Message);
        }
    }

    private ChatbotToolExecutionResult BuildFailure(
        string toolName,
        string toolArgs,
        string? toolCallId,
        string errorCode,
        string errorMessage)
    {
        _logger.LogWarning(
            "Chatbot tool execution rejected. ToolName={ToolName}, ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}",
            toolName,
            errorCode,
            errorMessage);

        return new ChatbotToolExecutionResult
        {
            Succeeded = false,
            ToolName = toolName,
            ToolArgs = toolArgs,
            ToolResult = JsonSerializer.Serialize(new
            {
                success = false,
                errorCode,
                errorMessage
            }),
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            ToolCallId = toolCallId
        };
    }

    private sealed class ToolExecutionException : Exception
    {
        public ToolExecutionException(string code, string message)
            : base(message)
        {
            Code = code;
        }

        public string Code { get; }
    }
}
