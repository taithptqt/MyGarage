import { request } from "@app/apiClient";

/**
 * Executes a request targeting the modern /api/v1 route first,
 * falling back to the legacy /api route if 404 is encountered.
 */
async function requestWithDualRoute(v1Url, legacyUrl, options = {}) {
  try {
    return await request({ ...options, url: v1Url });
  } catch (err) {
    if ((err?.status === 404 || err?.response?.status === 404) && legacyUrl) {
      return await request({ ...options, url: legacyUrl });
    }
    throw err;
  }
}

export const getVoucherPage = (params) =>
  requestWithDualRoute("/api/v1/vouchers/admin-list", "/api/v1/vouchers/admin-list", { method: "GET", params });

export const getRedemptionPage = (params) =>
  requestWithDualRoute("/api/v1/voucher-redemptions/admin-page", "/api/v1/voucher-redemptions/admin-page", { method: "GET", params });

export const getApprovalPage = (params) =>
  requestWithDualRoute("/api/v1/voucher-approval-requests/admin-page", "/api/v1/voucher-approval-requests/admin-page", { method: "GET", params });

export const getVoucherReportPage = (params) =>
  requestWithDualRoute("/api/v1/vouchers/report-page", "/api/v1/vouchers/report-page", { method: "GET", params });

export const getCampaignPage = (params) =>
  requestWithDualRoute("/api/v1/voucher-campaigns/admin-page", "/api/v1/voucher-campaigns/admin-page", { method: "GET", params });

export const getVoucherDashboard = () =>
  requestWithDualRoute("/api/v1/vouchers/dashboard", "/api/v1/vouchers/dashboard", { method: "GET" });
