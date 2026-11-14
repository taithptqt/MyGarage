export function parseInvoiceTaxSummary(data) {
  if (!data || data.totalTax == null || data.taxedCount == null || !Array.isArray(data.items) ||
      !Number.isFinite(Number(data.totalTax)) || !Number.isSafeInteger(Number(data.taxedCount)) ||
      Number(data.taxedCount) < 0 || data.items.length > 30 || data.items.length > Number(data.taxedCount)) {
    throw new Error("Không tải được tổng hợp thuế hóa đơn.");
  }
  return data;
}
