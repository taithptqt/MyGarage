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
/// Provides catalog, pricing/inventory, branch, vehicle, loyalty, and voucher lookup tools.
/// </summary>
public sealed class ChatbotCatalogToolProvider : IChatbotToolDefinitionProvider
{
    private const string PricingInventoryLookupTool = "product_pricing_inventory_lookup";
    private const string GetCustomerBenefitsTool = "get_customer_benefits";
    private const string GetCustomerVouchersTool = "get_customer_vouchers";
    private const string ListCustomerVehiclesTool = "list_customer_vehicles";
    private const string GetBranchListTool = "get_branch_list";
    private const string SearchStandardServicesTool = "search_standard_services";

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
        }
    ];

    private static readonly HashSet<string> SupportedTools = Definitions
        .Select(x => x.ToolName)
        .ToHashSet(StringComparer.Ordinal);

    private readonly IChatbotPricingInventoryGuard _pricingInventoryGuard;
    private readonly IChatbotVoucherStatusLookupService _voucherStatusLookupService;
    private readonly IChatbotLoyaltyStatusLookupService _loyaltyStatusLookupService;
    private readonly IChatbotStore _store;

    public ChatbotCatalogToolProvider(
        IChatbotPricingInventoryGuard pricingInventoryGuard,
        IChatbotVoucherStatusLookupService voucherStatusLookupService,
        IChatbotLoyaltyStatusLookupService loyaltyStatusLookupService,
        IChatbotStore store)
    {
        _pricingInventoryGuard = pricingInventoryGuard;
        _voucherStatusLookupService = voucherStatusLookupService;
        _loyaltyStatusLookupService = loyaltyStatusLookupService;
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
            PricingInventoryLookupTool => ExecutePricingLookupPayloadAsync(request, argsRoot, cancellationToken),
            GetCustomerBenefitsTool => ExecuteGetCustomerBenefitsPayloadAsync(request, cancellationToken),
            GetCustomerVouchersTool => ExecuteGetCustomerVouchersPayloadAsync(request, argsRoot, cancellationToken),
            ListCustomerVehiclesTool => ExecuteListCustomerVehiclesPayloadAsync(request, argsRoot, cancellationToken),
            GetBranchListTool => ExecuteGetBranchListPayloadAsync(argsRoot, cancellationToken),
            SearchStandardServicesTool => ExecuteSearchStandardServicesPayloadAsync(argsRoot, cancellationToken),
            _ => throw new ChatbotToolExecutionException("TOOL_NOT_IMPLEMENTED", "Tool is allowlisted but no executor is registered.")
        };
    }

    private async Task<object> ExecutePricingLookupPayloadAsync(
        ChatbotToolExecutionRequest request,
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var productHint = ChatbotToolJsonHelper.ReadString(argsRoot, "productHint");
        if (string.IsNullOrWhiteSpace(productHint) || productHint.Length > 200)
        {
            throw new ChatbotToolExecutionException("INVALID_TOOL_ARGS", "productHint is required.");
        }

        var lookup = await _pricingInventoryGuard.ResolveAsync(
            request.CustomerId,
            request.BranchId,
            productHint,
            cancellationToken);

        return new
        {
            lookup.ProductFound,
            lookup.ProductHint,
            lookup.ProductId,
            lookup.ProductName,
            lookup.ProductSku,
            lookup.PriceAvailable,
            lookup.Price,
            lookup.PriceSource,
            lookup.StockAvailable,
            lookup.StockQuantity,
            lookup.StockSource
        };
    }

    private async Task<object> ExecuteGetCustomerBenefitsPayloadAsync(
        ChatbotToolExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var loyalty = await _loyaltyStatusLookupService.TryLookupAsync(
            request.CustomerId,
            "uu dai thanh vien",
            cancellationToken);
        var vouchers = await _voucherStatusLookupService.TryLookupAsync(
            request.CustomerId,
            "voucher",
            cancellationToken);

        return new
        {
            loyaltySummary = loyalty?.Summary,
            loyalty = loyalty?.LoyaltyStatus,
            voucherSummary = vouchers?.Summary,
            vouchers = vouchers?.VoucherStatus?.AvailableVouchers
                ?? Array.Empty<MyGarage.DTOs.Responses.Chatbot.ChatbotVoucherStatusItemDto>()
        };
    }

    private async Task<object> ExecuteGetCustomerVouchersPayloadAsync(
        ChatbotToolExecutionRequest request,
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var includeUpcoming = ChatbotToolJsonHelper.ReadBool(argsRoot, "includeUpcoming") ?? true;
        var lookup = await _voucherStatusLookupService.TryLookupAsync(
            request.CustomerId,
            "voucher",
            cancellationToken);
        var items = lookup?.VoucherStatus?.AvailableVouchers ?? Array.Empty<DTOs.Responses.Chatbot.ChatbotVoucherStatusItemDto>();
        if (!includeUpcoming)
        {
            items = items.Where(x => x.IsCurrentlyActive).ToArray();
        }

        return new
        {
            summary = lookup?.Summary,
            count = items.Count,
            vouchers = items
        };
    }

    private async Task<object> ExecuteListCustomerVehiclesPayloadAsync(
        ChatbotToolExecutionRequest request,
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var includeInactiveOwnership = ChatbotToolJsonHelper.ReadBool(argsRoot, "includeInactiveOwnership") ?? false;
        var vehicles = await _store.GetCustomerVehiclesForToolAsync(
            request.CustomerId,
            includeInactiveOwnership,
            30,
            cancellationToken);

        return new { count = vehicles.Count, vehicles };
    }

    private async Task<object> ExecuteGetBranchListPayloadAsync(
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var onlyActive = ChatbotToolJsonHelper.ReadBool(argsRoot, "onlyActive") ?? true;
        var branches = await _store.GetBranchesForToolAsync(onlyActive, cancellationToken);
        return new { count = branches.Count, branches };
    }

    private async Task<object> ExecuteSearchStandardServicesPayloadAsync(
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var keyword = ChatbotToolJsonHelper.ReadString(argsRoot, "keyword");
        var limit = Math.Clamp(ChatbotToolJsonHelper.ReadInt(argsRoot, "limit") ?? 10, 1, 30);
        var services = await _store.SearchStandardServicesForToolAsync(keyword, limit, cancellationToken);
        return new { count = services.Count, services };
    }
}
