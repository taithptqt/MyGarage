import { request, requestSafe } from "@app/apiClient";

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

/**
 * Safe version of requestWithDualRoute using requestSafe.
 */
async function requestSafeWithDualRoute(v1Url, legacyUrl, options = {}) {
  const result = await requestSafe({ ...options, url: v1Url });
  if (!result.ok && (result.status === 404 || result.response?.status === 404) && legacyUrl) {
    return await requestSafe({ ...options, url: legacyUrl });
  }
  return result;
}

export const voucherApi = {
  getDashboard: () =>
    requestWithDualRoute("/api/v1/vouchers/dashboard", "/api/vouchers/dashboard", { method: "GET" }),

  getVouchers: (params) =>
    requestWithDualRoute("/api/v1/vouchers", "/api/vouchers", { method: "GET", params }),

  getVoucherById: (id) =>
    requestWithDualRoute(`/api/v1/vouchers/${id}`, `/api/vouchers/${id}`, { method: "GET" }),

  createVoucher: (data) =>
    requestWithDualRoute("/api/v1/vouchers", "/api/vouchers", { method: "POST", data }),

  updateVoucher: (id, data) =>
    requestWithDualRoute(`/api/v1/vouchers/${id}`, `/api/vouchers/${id}`, { method: "PUT", data }),

  activateVoucher: (id) =>
    requestWithDualRoute(`/api/v1/vouchers/${id}`, `/api/vouchers/${id}`, { method: "PATCH", data: { status: "ACTIVE" } }),

  pauseVoucher: (id) =>
    requestWithDualRoute(`/api/v1/vouchers/${id}`, `/api/vouchers/${id}`, { method: "PATCH", data: { status: "PAUSED" } }),

  revokeVoucher: (id) =>
    requestWithDualRoute(`/api/v1/vouchers/${id}`, `/api/vouchers/${id}`, { method: "PATCH", data: { status: "REVOKED" } }),

  getCampaigns: (params) =>
    requestWithDualRoute("/api/v1/voucher-campaigns", "/api/voucher-campaigns", { method: "GET", params }),

  getCampaignSummary: (id) =>
    requestWithDualRoute(`/api/v1/voucher-campaigns/${id}/summary`, `/api/voucher-campaigns/${id}/summary`, { method: "GET" }),

  getCampaignVoucherPage: (id, params) =>
    requestWithDualRoute(`/api/v1/voucher-campaigns/${id}/voucher-page`, `/api/voucher-campaigns/${id}/voucher-page`, { method: "GET", params }),

  getCampaignById: (id) =>
    requestWithDualRoute(`/api/v1/voucher-campaigns/${id}`, `/api/voucher-campaigns/${id}`, { method: "GET" }),

  createCampaign: (data) =>
    requestWithDualRoute("/api/v1/voucher-campaigns", "/api/voucher-campaigns", { method: "POST", data }),

  updateCampaign: (id, data) =>
    requestWithDualRoute(`/api/v1/voucher-campaigns/${id}`, `/api/voucher-campaigns/${id}`, { method: "PUT", data }),

  deleteCampaign: (id) =>
    requestWithDualRoute(`/api/v1/voucher-campaigns/${id}`, `/api/voucher-campaigns/${id}`, { method: "DELETE" }),

  assignCampaignVouchers: (id, data) =>
    requestWithDualRoute(`/api/v1/voucher-campaigns/${id}/voucher-links`, `/api/voucher-campaigns/${id}/voucher-links`, { method: "POST", data }),

  getRedemptions: (params) =>
    requestWithDualRoute("/api/v1/voucher-redemptions", "/api/voucher-redemptions", {
      method: "GET",
      params: typeof params === "string" ? { status: params } : params
    }),

  checkVoucher: (data) =>
    requestSafeWithDualRoute("/api/v1/voucher-redemptions/validations", "/api/voucher-redemptions/validations", { method: "POST", data }),

  redeemVoucher: (data) =>
    requestSafeWithDualRoute("/api/v1/voucher-redemptions/transactions", "/api/voucher-redemptions/transactions", { method: "POST", data }),

  reverseRedemption: (id, data) =>
    requestSafeWithDualRoute(`/api/v1/voucher-redemptions/transactions/${id}/reversals`, `/api/voucher-redemptions/transactions/${id}/reversals`, { method: "POST", data }),

  getApprovalRequests: (params) =>
    requestWithDualRoute("/api/v1/voucher-approval-requests", "/api/voucher-approval-requests", {
      method: "GET",
      params: typeof params === "string" ? { status: params } : params
    }),

  createApprovalRequest: (data) =>
    requestWithDualRoute("/api/v1/voucher-approval-requests", "/api/voucher-approval-requests", { method: "POST", data }),

  approveRequest: (id, data) =>
    requestWithDualRoute(`/api/v1/voucher-approval-requests/${id}`, `/api/voucher-approval-requests/${id}`, { method: "PATCH", data: { ...data, decision: "APPROVE" } }),

  rejectRequest: (id, data) =>
    requestWithDualRoute(`/api/v1/voucher-approval-requests/${id}`, `/api/voucher-approval-requests/${id}`, { method: "PATCH", data: { ...data, decision: "REJECT" } })
};
