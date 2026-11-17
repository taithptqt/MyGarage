import { request, requestSafe } from "../../../../../app/apiClient";

const isCount = (value) => Number.isSafeInteger(value) && value >= 0;

/**
 * Executes a request targeting the modern /api/v1 route first,
 * falling back to the legacy /api route if 404 is encountered.
 */
export async function requestWithDualRoute(v1Url, legacyUrl, options = {}) {
  try {
    return await request({ ...options, url: v1Url });
  } catch (err) {
    if ((err?.status === 404 || err?.response?.status === 404) && legacyUrl) {
      return await request({ ...options, url: legacyUrl });
    }
    throw err;
  }
}

/**
 * Safe version of requestWithDualRoute using requestSafe.
 */
export async function requestSafeWithDualRoute(v1Url, legacyUrl, options = {}) {
  const result = await requestSafe({ ...options, url: v1Url });
  if (!result.ok && (result.status === 404 || result.response?.status === 404) && legacyUrl) {
    return await requestSafe({ ...options, url: legacyUrl });
  }
  return result;
}

export async function getInvoiceListPage(params) {
  const data = await requestWithDualRoute(
    "/api/v1/invoices/list-page",
    "/api/v1/invoices/list-page",
    { method: "GET", params }
  );
  const summary = data?.summary;
  if (!Array.isArray(data?.items) || !isCount(data.total) || !isCount(data.page) || data.page < 1 ||
      !isCount(data.pageSize) || data.pageSize < 1 || !summary ||
      ![summary.totalInvoice, summary.issuedCount, summary.paidCount, summary.unknownStatusCount].every(isCount)) {
    throw new Error("Không tải được danh sách hóa đơn.");
  }
  return data;
}

export async function getInvoiceViewDetail(id, params) {
  const data = await requestWithDualRoute(
    `/api/v1/invoices/${id}/view-detail`,
    `/api/v1/invoices/${id}/view-detail`,
    { method: "GET", params }
  );
  if (!data?.invoice || !Array.isArray(data.details) || !Array.isArray(data.payments) ||
      ![data.detailsTotal, data.paymentsTotal].every(isCount) ||
      ![data.detailsPage, data.paymentsPage, data.pageSize].every((value) => isCount(value) && value > 0)) {
    throw new Error("Không tải được chi tiết hóa đơn.");
  }
  return data;
}
