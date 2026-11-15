using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.Interfaces;

namespace MyGarage.Services;

/// <summary>
/// Provides customer service order, invoice, warranty lookup, and order draft preview tools.
/// </summary>
public sealed class ChatbotOrderToolProvider : IChatbotToolDefinitionProvider
{
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

    private static readonly HashSet<string> SupportedTools = Definitions
        .Select(x => x.ToolName)
        .ToHashSet(StringComparer.Ordinal);

    private readonly IChatbotStore _store;

    public ChatbotOrderToolProvider(IChatbotStore store)
    {
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
            GetCustomerServiceHistoryTool => ExecuteGetCustomerServiceHistoryPayloadAsync(request, argsRoot, cancellationToken),
            GetCustomerServiceOrderStatusTool => ExecuteGetCustomerServiceOrderStatusPayloadAsync(request, argsRoot, cancellationToken),
            GetCustomerInvoicePaymentStatusTool => ExecuteGetCustomerInvoicePaymentStatusPayloadAsync(request, argsRoot, cancellationToken),
            GetCustomerWarrantyHistoryTool => ExecuteGetCustomerWarrantyHistoryPayloadAsync(request, argsRoot, cancellationToken),
            CreateServiceOrderDraftTool => ExecuteCreateServiceOrderDraftPayloadAsync(request, argsRoot, cancellationToken),
            CreateRetailOrderDraftTool => ExecuteCreateRetailOrderDraftPayloadAsync(request, argsRoot, cancellationToken),
            _ => throw new ChatbotToolExecutionException("TOOL_NOT_IMPLEMENTED", "Tool is allowlisted but no executor is registered.")
        };
    }

    private async Task<object> ExecuteGetCustomerServiceHistoryPayloadAsync(
        ChatbotToolExecutionRequest request,
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(ChatbotToolJsonHelper.ReadInt(argsRoot, "limit") ?? 10, 1, 20);
        var orders = await _store.GetCustomerServiceHistoryForToolAsync(request.CustomerId, limit, cancellationToken);

        return new
        {
            count = orders.Count,
            serviceOrders = orders
        };
    }

    private async Task<object> ExecuteGetCustomerServiceOrderStatusPayloadAsync(
        ChatbotToolExecutionRequest request,
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var orderId = ChatbotToolJsonHelper.ReadLong(argsRoot, "orderId");
        var orderCode = ChatbotToolJsonHelper.ReadString(argsRoot, "orderCode");
        var limit = Math.Clamp(ChatbotToolJsonHelper.ReadInt(argsRoot, "limit") ?? 10, 1, 20);

        var orders = await _store.GetCustomerServiceOrderStatusForToolAsync(
            request.CustomerId,
            orderId,
            orderCode,
            limit,
            cancellationToken);

        return new
        {
            count = orders.Count,
            serviceOrders = orders,
            hasExactFilter = orderId.HasValue || !string.IsNullOrWhiteSpace(orderCode)
        };
    }

    private async Task<object> ExecuteGetCustomerInvoicePaymentStatusPayloadAsync(
        ChatbotToolExecutionRequest request,
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var invoiceId = ChatbotToolJsonHelper.ReadLong(argsRoot, "invoiceId");
        var orderCode = ChatbotToolJsonHelper.ReadString(argsRoot, "orderCode");
        var limit = Math.Clamp(ChatbotToolJsonHelper.ReadInt(argsRoot, "limit") ?? 10, 1, 20);

        var invoices = await _store.GetCustomerInvoiceStatusForToolAsync(
            request.CustomerId,
            invoiceId,
            orderCode,
            limit,
            cancellationToken);

        return new
        {
            count = invoices.Count,
            invoices
        };
    }

    private async Task<object> ExecuteGetCustomerWarrantyHistoryPayloadAsync(
        ChatbotToolExecutionRequest request,
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var vehicleId = ChatbotToolJsonHelper.ReadLong(argsRoot, "vehicleId");
        var limit = Math.Clamp(ChatbotToolJsonHelper.ReadInt(argsRoot, "limit") ?? 10, 1, 20);

        var warrantyItems = await _store.GetCustomerWarrantyHistoryForToolAsync(
            request.CustomerId,
            vehicleId,
            limit,
            cancellationToken);

        return new
        {
            count = warrantyItems.Count,
            items = warrantyItems
        };
    }

    private async Task<object> ExecuteCreateServiceOrderDraftPayloadAsync(
        ChatbotToolExecutionRequest request,
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var vehicleId = ChatbotToolJsonHelper.ReadLong(argsRoot, "vehicleId");
        var originBranchId = ChatbotToolJsonHelper.ReadLong(argsRoot, "originBranchId") ?? request.BranchId;
        var executeBranchId = ChatbotToolJsonHelper.ReadLong(argsRoot, "executeBranchId") ?? originBranchId;
        var serviceIds = ChatbotToolJsonHelper.ReadLongArray(argsRoot, "serviceIds");
        var customerNote = ChatbotToolJsonHelper.ReadString(argsRoot, "customerNote");

        var missing = new List<string>();
        if (!vehicleId.HasValue || vehicleId.Value <= 0) missing.Add("vehicleId");
        if (!originBranchId.HasValue || originBranchId.Value <= 0) missing.Add("originBranchId");
        if (!executeBranchId.HasValue || executeBranchId.Value <= 0) missing.Add("executeBranchId");
        if (serviceIds.Count == 0) missing.Add("serviceIds");

        if (missing.Count > 0)
        {
            return new { canProceed = false, reasonCode = "MISSING_REQUIRED_FIELDS", missingFields = missing };
        }

        var resolvedVehicleId = vehicleId!.Value;
        var resolvedOriginBranchId = originBranchId!.Value;
        var resolvedExecuteBranchId = executeBranchId!.Value;

        var ownsVehicle = await _store.CustomerOwnsVehicleAsync(request.CustomerId, resolvedVehicleId, cancellationToken);

        if (!ownsVehicle)
        {
            return new { canProceed = false, reasonCode = "VEHICLE_NOT_OWNED" };
        }

        var requiredBranchIds = new[] { resolvedOriginBranchId, resolvedExecuteBranchId }
            .Distinct()
            .ToArray();
        var activeBranchIds = await _store.GetActiveBranchIdsAsync(requiredBranchIds, cancellationToken);
        if (activeBranchIds.Count != requiredBranchIds.Length)
        {
            return new { canProceed = false, reasonCode = "BRANCH_NOT_FOUND_OR_INACTIVE" };
        }

        var distinctServiceIds = serviceIds.Distinct().ToList();
        var services = await _store.GetActiveStandardServicesByIdsAsync(distinctServiceIds, cancellationToken);
        if (services.Count != distinctServiceIds.Count)
        {
            return new { canProceed = false, reasonCode = "SERVICE_NOT_FOUND_OR_INACTIVE" };
        }

        var servicePreviews = services.Cast<object>().ToArray();
        var estimatedTotal = services.Sum(x => x.BasePrice);

        return new
        {
            canProceed = true,
            preview = new
            {
                vehicleId,
                originBranchId,
                executeBranchId,
                serviceIds,
                services = servicePreviews,
                estimatedTotal,
                customerNote
            }
        };
    }

    private async Task<object> ExecuteCreateRetailOrderDraftPayloadAsync(
        ChatbotToolExecutionRequest request,
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var branchId = ChatbotToolJsonHelper.ReadLong(argsRoot, "branchId") ?? request.BranchId;
        var customerNote = ChatbotToolJsonHelper.ReadString(argsRoot, "customerNote");

        var draftItems = new List<(long ProductId, decimal Quantity)>();
        if (argsRoot.TryGetPropertyValue("items", out var itemsNode) && itemsNode is JsonArray itemsArray)
        {
            foreach (var itemNode in itemsArray)
            {
                if (itemNode is not JsonObject item)
                {
                    continue;
                }

                var productId = ChatbotToolJsonHelper.ReadLong(item, "productId");
                var quantity = ChatbotToolJsonHelper.ReadDecimal(item, "quantity");
                if (productId.HasValue && productId.Value > 0 && quantity.HasValue && quantity.Value > 0)
                {
                    draftItems.Add((productId.Value, quantity.Value));
                }
            }
        }

        var missing = new List<string>();
        if (!branchId.HasValue || branchId.Value <= 0) missing.Add("branchId");
        if (draftItems.Count == 0) missing.Add("items");
        if (missing.Count > 0)
        {
            return new { canProceed = false, reasonCode = "MISSING_REQUIRED_FIELDS", missingFields = missing };
        }

        var resolvedBranchId = branchId!.Value;

        var activeBranchIds = await _store.GetActiveBranchIdsAsync([resolvedBranchId], cancellationToken);
        if (activeBranchIds.Count == 0)
        {
            return new { canProceed = false, reasonCode = "BRANCH_NOT_FOUND_OR_INACTIVE" };
        }

        var productIds = draftItems.Select(x => x.ProductId).Distinct().ToList();
        var products = await _store.GetActiveProductsByIdsAsync(productIds, cancellationToken);

        var stockMap = await _store.GetAvailableStockByProductIdsAsync(resolvedBranchId, productIds, cancellationToken);
        var productMap = products.ToDictionary(x => x.Id, x => x);

        var lineItems = draftItems
            .Select(item =>
            {
                var hasProduct = productMap.TryGetValue(item.ProductId, out var product);
                var availableStock = stockMap.TryGetValue(item.ProductId, out var stock) ? stock : 0m;
                var unitPrice = hasProduct ? product!.UnitPrice : 0m;
                var lineTotal = unitPrice * item.Quantity;
                return new
                {
                    productId = item.ProductId,
                    sku = hasProduct ? product!.Sku : null,
                    productName = hasProduct ? product!.Name : null,
                    requestedQuantity = item.Quantity,
                    unitPrice,
                    lineTotal,
                    availableStock,
                    isProductFound = hasProduct,
                    isStockSufficient = availableStock >= item.Quantity
                };
            })
            .ToList();

        var canProceed = lineItems.Count > 0 &&
            lineItems.All(x => x.isProductFound && x.isStockSufficient);
        var subTotal = lineItems.Sum(x => x.lineTotal);

        return new
        {
            canProceed,
            reasonCode = canProceed ? null : "DRAFT_VALIDATION_FAILED",
            preview = new
            {
                branchId,
                customerId = request.CustomerId,
                customerNote,
                subTotal,
                items = lineItems
            }
        };
    }
}
