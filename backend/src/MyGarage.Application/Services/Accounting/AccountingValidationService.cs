using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MyGarage.Application.Common;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public interface IAccountingValidationService
{
    Task<string?> ValidateSupplierInvoice3WayMatchingAsync(JsonElement payload, long? branchId, long? existingId = null);
    Task<string?> ValidateSupplierInvoiceDetail3WayMatchingAsync(JsonElement payload, long? branchId, long? existingId = null);
    Task<string?> ValidateSupplierPaymentAsync(JsonElement payload, long? branchId, long? existingId = null);
}

public sealed class AccountingValidationService : IAccountingValidationService
{
    private readonly IAccountingTableQueryService _tableQuery;
    private readonly IAccountingSqlQueryService _sqlQuery;

    public AccountingValidationService(
        IAccountingTableQueryService tableQuery,
        IAccountingSqlQueryService sqlQuery)
    {
        _tableQuery = tableQuery;
        _sqlQuery = sqlQuery;
    }

    public async Task<string?> ValidateSupplierInvoice3WayMatchingAsync(JsonElement payload, long? branchId, long? existingId = null)
    {
        long? supplierId = TryGetLong(payload, "supplierId", "supplier_id");
        long? purchaseOrderId = TryGetLong(payload, "purchaseOrderId", "purchase_order_id");
        decimal? totalAmount = TryGetDecimal(payload, "totalAmount", "total_amount");

        if (existingId.HasValue)
        {
            var existing = await _tableQuery.GetByIdAsync("supplier-invoices", existingId.Value, branchId);
            supplierId ??= AsLong(existing, "supplierId");
            purchaseOrderId ??= AsLong(existing, "purchaseOrderId");
            totalAmount ??= AsDecimal(existing, "totalAmount");
        }

        if (!supplierId.HasValue || supplierId.Value <= 0)
        {
            return "supplierId is required.";
        }

        if (!purchaseOrderId.HasValue || purchaseOrderId.Value <= 0)
        {
            return "purchaseOrderId is required for 3-way matching.";
        }

        if (!totalAmount.HasValue || totalAmount.Value <= 0m)
        {
            return "totalAmount must be > 0 for 3-way matching.";
        }

        var purchaseOrderRow = (await _sqlQuery.QueryAsync(
            """
            SELECT TOP 1
                po.id,
                po.supplier_id AS supplierId,
                po.branch_id AS branchId
            FROM dbo.purchase_orders po
            WHERE po.id = @purchaseOrderId
              AND (@branchId IS NULL OR po.branch_id = @branchId);
            """,
            [
                new QueryParam("@purchaseOrderId", purchaseOrderId.Value),
                new QueryParam("@branchId", (object?)branchId ?? DBNull.Value)
            ])).FirstOrDefault();

        if (purchaseOrderRow is null)
        {
            return $"purchase_order #{purchaseOrderId.Value} not found in current branch scope.";
        }

        var poSupplierId = AsLong(purchaseOrderRow, "supplierId");
        if (!poSupplierId.HasValue || poSupplierId.Value != supplierId.Value)
        {
            return "supplierId does not match the supplier on purchase order.";
        }

        var totalsRow = (await _sqlQuery.QueryAsync(
            """
            ;WITH po_total AS (
                SELECT
                    CAST(SUM(
                        COALESCE(
                            poi.total_amount,
                            poi.TotalAmount,
                            (COALESCE(poi.ordered_quantity, poi.OrderedQuantity, poi.quantity, poi.Quantity, 0) *
                             COALESCE(poi.unit_cost, poi.UnitCost, poi.unit_price, poi.UnitPrice, 0))
                        )
                    ) AS DECIMAL(18,2)) AS po_total
                FROM dbo.purchase_order_items poi
                WHERE poi.purchase_order_id = @purchaseOrderId
            ),
            grn_total AS (
                SELECT
                    CAST(SUM(
                        COALESCE(
                            grni.line_total,
                            grni.LineTotal,
                            (COALESCE(grni.received_quantity, grni.ReceivedQuantity, grni.quantity, grni.Quantity, 0) *
                             COALESCE(grni.unit_cost, grni.UnitCost, 0))
                        )
                    ) AS DECIMAL(18,2)) AS grn_total
                FROM dbo.goods_receipt_note_items grni
                INNER JOIN dbo.goods_receipt_notes grn ON grn.id = grni.grn_id
                WHERE grn.purchase_order_id = @purchaseOrderId
            )
            SELECT
                COALESCE((SELECT po_total FROM po_total), 0) AS poTotal,
                COALESCE((SELECT grn_total FROM grn_total), 0) AS grnTotal;
            """,
            [new QueryParam("@purchaseOrderId", purchaseOrderId.Value)])).FirstOrDefault();

        var poTotal = AsDecimal(totalsRow, "poTotal");
        var grnTotal = AsDecimal(totalsRow, "grnTotal");

        if (poTotal <= 0m)
        {
            return "purchase_order does not have valid line totals for matching.";
        }

        if (grnTotal <= 0m)
        {
            return "GRN is required before creating supplier invoice (3-way matching).";
        }

        if (totalAmount.Value > poTotal + 0.01m)
        {
            return $"Supplier invoice total ({totalAmount.Value:0.##}) exceeds PO total ({poTotal:0.##}).";
        }

        if (totalAmount.Value > grnTotal + 0.01m)
        {
            return $"Supplier invoice total ({totalAmount.Value:0.##}) exceeds received GRN total ({grnTotal:0.##}).";
        }

        // Optional deep validation when header API receives line details.
        if (payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty("details", out var detailItems) &&
            detailItems.ValueKind == JsonValueKind.Array)
        {
            foreach (var line in detailItems.EnumerateArray())
            {
                var lineError = await ValidateSupplierInvoiceDetail3WayMatchingAsync(line, branchId);
                if (!string.IsNullOrWhiteSpace(lineError))
                {
                    return lineError;
                }
            }
        }

        return null;
    }

    public async Task<string?> ValidateSupplierInvoiceDetail3WayMatchingAsync(JsonElement payload, long? branchId, long? existingId = null)
    {
        long? supplierInvoiceId = TryGetLong(payload, "supplierInvoiceId", "supplier_invoice_id");
        long? purchaseOrderItemId = TryGetLong(payload, "purchaseOrderItemId", "purchase_order_item_id");
        decimal? quantity = TryGetDecimal(payload, "quantity");
        decimal? unitCost = TryGetDecimal(payload, "unitCost", "unit_cost", "unitPrice", "unit_price");
        decimal? lineTotal = TryGetDecimal(payload, "lineTotal", "line_total", "totalAmount", "total_amount");

        if (existingId.HasValue)
        {
            var existing = await _tableQuery.GetByIdAsync("supplier-invoice-details", existingId.Value, branchId);
            supplierInvoiceId ??= AsLong(existing, "supplierInvoiceId");
            purchaseOrderItemId ??= AsLong(existing, "purchaseOrderItemId");
            quantity ??= AsDecimal(existing, "quantity");
            unitCost ??= AsDecimal(existing, "unitCost");
            lineTotal ??= AsDecimal(existing, "lineTotal");
        }

        if (!supplierInvoiceId.HasValue || supplierInvoiceId.Value <= 0)
        {
            return "supplierInvoiceId is required.";
        }

        if (!purchaseOrderItemId.HasValue || purchaseOrderItemId.Value <= 0)
        {
            return "purchaseOrderItemId is required for 3-way matching.";
        }

        if (!quantity.HasValue || quantity.Value <= 0m)
        {
            return "quantity must be > 0.";
        }

        if (!unitCost.HasValue || unitCost.Value < 0m)
        {
            return "unitCost must be >= 0.";
        }

        var referenceRow = (await _sqlQuery.QueryAsync(
            """
            SELECT TOP 1
                sid.id AS supplierInvoiceDetailId,
                sid.supplier_invoice_id AS supplierInvoiceId,
                si.purchase_order_id AS purchaseOrderId,
                si.branch_id AS supplierInvoiceBranchId,
                poi.id AS purchaseOrderItemId,
                CAST(COALESCE(poi.ordered_quantity, poi.OrderedQuantity, poi.quantity, poi.Quantity, 0) AS DECIMAL(18,4)) AS orderedQuantity,
                CAST(COALESCE(poi.unit_cost, poi.UnitCost, poi.unit_price, poi.UnitPrice, 0) AS DECIMAL(18,4)) AS poUnitCost
            FROM dbo.supplier_invoices si
            LEFT JOIN dbo.supplier_invoice_details sid ON sid.supplier_invoice_id = si.id
            INNER JOIN dbo.purchase_order_items poi ON poi.id = @purchaseOrderItemId
            WHERE si.id = @supplierInvoiceId
              AND (@branchId IS NULL OR si.branch_id = @branchId);
            """,
            [
                new QueryParam("@supplierInvoiceId", supplierInvoiceId.Value),
                new QueryParam("@purchaseOrderItemId", purchaseOrderItemId.Value),
                new QueryParam("@branchId", (object?)branchId ?? DBNull.Value)
            ])).FirstOrDefault();

        if (referenceRow is null)
        {
            return "Supplier invoice or purchase order item not found in current branch scope.";
        }

        var purchaseOrderId = AsLong(referenceRow, "purchaseOrderId");
        var orderedQuantity = AsDecimal(referenceRow, "orderedQuantity");
        var poUnitCost = AsDecimal(referenceRow, "poUnitCost");

        if (!purchaseOrderId.HasValue || purchaseOrderId.Value <= 0)
        {
            return "Supplier invoice is missing purchaseOrderId reference.";
        }

        var receivedQuantity = await _sqlQuery.ScalarAsync<decimal?>(
            """
            SELECT CAST(COALESCE(SUM(COALESCE(grni.received_quantity, grni.ReceivedQuantity, grni.quantity, grni.Quantity, 0)), 0) AS DECIMAL(18,4))
            FROM dbo.goods_receipt_note_items grni
            INNER JOIN dbo.goods_receipt_notes grn ON grn.id = grni.grn_id
            WHERE grn.purchase_order_id = @purchaseOrderId
              AND grni.purchase_order_item_id = @purchaseOrderItemId;
            """,
            [
                new QueryParam("@purchaseOrderId", purchaseOrderId.Value),
                new QueryParam("@purchaseOrderItemId", purchaseOrderItemId.Value)
            ]) ?? 0m;

        var alreadyInvoicedQty = await _sqlQuery.ScalarAsync<decimal?>(
            """
            SELECT CAST(COALESCE(SUM(COALESCE(sid.quantity, sid.Quantity, 0)), 0) AS DECIMAL(18,4))
            FROM dbo.supplier_invoice_details sid
            WHERE sid.supplier_invoice_id = @supplierInvoiceId
              AND sid.purchase_order_item_id = @purchaseOrderItemId
              AND (@existingId IS NULL OR sid.id <> @existingId);
            """,
            [
                new QueryParam("@supplierInvoiceId", supplierInvoiceId.Value),
                new QueryParam("@purchaseOrderItemId", purchaseOrderItemId.Value),
                new QueryParam("@existingId", (object?)existingId ?? DBNull.Value)
            ]) ?? 0m;

        var effectiveQtyAfterSave = alreadyInvoicedQty + quantity.Value;
        if (orderedQuantity > 0m && effectiveQtyAfterSave > orderedQuantity + 0.0001m)
        {
            return $"Invoiced quantity ({effectiveQtyAfterSave:0.####}) exceeds PO ordered quantity ({orderedQuantity:0.####}) for purchaseOrderItemId={purchaseOrderItemId.Value}.";
        }

        if (receivedQuantity <= 0m)
        {
            return $"GRN quantity is missing for purchaseOrderItemId={purchaseOrderItemId.Value}.";
        }

        if (effectiveQtyAfterSave > receivedQuantity + 0.0001m)
        {
            return $"Invoiced quantity ({effectiveQtyAfterSave:0.####}) exceeds received GRN quantity ({receivedQuantity:0.####}) for purchaseOrderItemId={purchaseOrderItemId.Value}.";
        }

        if (poUnitCost > 0m && unitCost.Value > poUnitCost + 0.01m)
        {
            return $"unitCost ({unitCost.Value:0.####}) exceeds PO unit cost ({poUnitCost:0.####}) for purchaseOrderItemId={purchaseOrderItemId.Value}.";
        }

        var calculatedLineTotal = Math.Round(quantity.Value * unitCost.Value, 2, MidpointRounding.AwayFromZero);
        if (lineTotal.HasValue && Math.Abs(lineTotal.Value - calculatedLineTotal) > 0.01m)
        {
            return $"lineTotal mismatch for purchaseOrderItemId={purchaseOrderItemId.Value}. Expected {calculatedLineTotal:0.##}.";
        }

        return null;
    }

    public async Task<string?> ValidateSupplierPaymentAsync(JsonElement payload, long? branchId, long? existingId = null)
    {
        long? apId = TryGetLong(payload, "accountsPayableId", "accounts_payable_id");
        decimal? paidAmount = TryGetDecimal(payload, "paidAmount", "paid_amount");
        if (existingId.HasValue)
        {
            var existing = await _tableQuery.GetByIdAsync("supplier-payments", existingId.Value, branchId);
            apId ??= AsLong(existing, "accountsPayableId");
            paidAmount ??= AsDecimal(existing, "paidAmount");
        }
        if (!apId.HasValue || apId.Value <= 0) return "accountsPayableId is required.";
        if (!paidAmount.HasValue || paidAmount.Value <= 0) return "paidAmount must be > 0.";

        var ap = (await _sqlQuery.QueryAsync(
            "SELECT TOP 1 outstanding_amount FROM dbo.accounts_payable WHERE id=@id AND (@branchId IS NULL OR branch_id=@branchId);",
            [new QueryParam("@id", apId.Value), new QueryParam("@branchId", (object?)branchId ?? DBNull.Value)])).FirstOrDefault();
        if (ap is null) return $"accounts_payable #{apId.Value} not found.";
        if (paidAmount.Value > AsDecimal(ap, "outstandingAmount") + 0.005m) return "paidAmount cannot exceed outstandingAmount.";
        return null;
    }

    private static long? TryGetLong(JsonElement payload, params string[] keys)
    {
        if (payload.ValueKind != JsonValueKind.Object) return null;
        foreach (var prop in payload.EnumerateObject())
            foreach (var key in keys)
                if (prop.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt64(out var n)) return n;
                    if (prop.Value.ValueKind == JsonValueKind.String && long.TryParse(prop.Value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out n)) return n;
                }
        return null;
    }

    private static decimal? TryGetDecimal(JsonElement payload, params string[] keys)
    {
        if (payload.ValueKind != JsonValueKind.Object) return null;
        foreach (var prop in payload.EnumerateObject())
            foreach (var key in keys)
                if (prop.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDecimal(out var n)) return n;
                    if (prop.Value.ValueKind == JsonValueKind.String && decimal.TryParse(prop.Value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out n)) return n;
                }
        return null;
    }

    private static long? AsLong(IDictionary<string, object?>? row, string key) =>
        row != null && row.TryGetValue(key, out var v) && v != null && v != DBNull.Value ? Convert.ToInt64(v) : null;

    private static decimal AsDecimal(IDictionary<string, object?>? row, string key) =>
        row != null && row.TryGetValue(key, out var v) && v != null && v != DBNull.Value ? Convert.ToDecimal(v) : 0m;
}
