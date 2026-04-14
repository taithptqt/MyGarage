using System;
using System.Collections.Generic;
using System.Linq;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public class ChatbotToolAllowlist : IChatbotToolAllowlist
{
    private static readonly HashSet<string> ReadOnlyTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "product_pricing_inventory_lookup",
        "get_customer_benefits",
        "get_customer_vouchers",
        "list_customer_vehicles",
        "get_branch_list",
        "search_standard_services",
        "get_available_slots",
        "read_booking_state",
        "get_customer_service_history",
        "get_customer_service_order_status",
        "get_customer_invoice_payment_status",
        "get_customer_warranty_history"
    };

    private static readonly HashSet<string> WriteCapableTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "update_booking_state",
        "create_appointment_draft",
        "confirm_appointment_booking",
        "create_service_order_draft",
        "create_retail_order_draft"
    };

    private static readonly HashSet<string> LegacyDisabledTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "product_price_lookup",
        "inventory_stock_lookup"
    };

    private static readonly HashSet<string> AllowedTools = new(
        ReadOnlyTools.Concat(WriteCapableTools),
        StringComparer.OrdinalIgnoreCase);

    public bool IsAllowed(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return false;
        }

        var normalized = toolName.Trim();
        return AllowedTools.Contains(normalized) && !LegacyDisabledTools.Contains(normalized);
    }

    public bool IsAllowedForLlm(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return false;
        }

        var normalized = toolName.Trim();
        return ReadOnlyTools.Contains(normalized) && !LegacyDisabledTools.Contains(normalized);
    }

    public bool IsWriteCapable(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return false;
        }

        return WriteCapableTools.Contains(toolName.Trim());
    }
}
