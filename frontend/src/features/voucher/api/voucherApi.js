import { request, requestSafe } from "@app/apiClient";

export const voucherApi = {
  getVouchers: () => request({ url: "/api/vouchers", method: "GET" }),
  getVoucherById: (id) => request({ url: `/api/vouchers/${id}`, method: "GET" }),
  createVoucher: (data) => request({ url: "/api/vouchers", method: "POST", data }),
  updateVoucher: (id, data) => request({ url: `/api/vouchers/${id}`, method: "PUT", data }),
  activateVoucher: (id) => request({ url: `/api/vouchers/${id}`, method: "PATCH", data: { status: "ACTIVE" } }),
  pauseVoucher: (id) => request({ url: `/api/vouchers/${id}`, method: "PATCH", data: { status: "PAUSED" } }),
  revokeVoucher: (id) => request({ url: `/api/vouchers/${id}`, method: "PATCH", data: { status: "REVOKED" } }),
  getCampaigns: (params) => request({ url: "/api/voucher-campaigns", method: "GET", params }),
  getCampaignById: (id) => request({ url: `/api/voucher-campaigns/${id}`, method: "GET" }),
  createCampaign: (data) => request({ url: "/api/voucher-campaigns", method: "POST", data }),
  updateCampaign: (id, data) => request({ url: `/api/voucher-campaigns/${id}`, method: "PUT", data }),
  deleteCampaign: (id) => request({ url: `/api/voucher-campaigns/${id}`, method: "DELETE" }),
  assignCampaignVouchers: (id, data) =>
    request({ url: `/api/voucher-campaigns/${id}/voucher-links`, method: "POST", data }),

  getRedemptions: (status) =>
    request({
      url: "/api/voucher-redemptions",
      method: "GET",
      params: status ? { status } : undefined
    }),
  checkVoucher: (data) => requestSafe({ url: "/api/voucher-redemptions/validations", method: "POST", data }),
  redeemVoucher: (data) => requestSafe({ url: "/api/voucher-redemptions/transactions", method: "POST", data }),
  reverseRedemption: (id, data) =>
    requestSafe({ url: `/api/voucher-redemptions/transactions/${id}/reversals`, method: "POST", data }),

  getApprovalRequests: (status) =>
    request({
      url: "/api/voucher-approval-requests",
      method: "GET",
      params: status ? { status } : undefined
    }),
  createApprovalRequest: (data) => request({ url: "/api/voucher-approval-requests", method: "POST", data }),
  approveRequest: (id, data) =>
    request({ url: `/api/voucher-approval-requests/${id}`, method: "PATCH", data: { ...data, decision: "APPROVE" } }),
  rejectRequest: (id, data) =>
    request({ url: `/api/voucher-approval-requests/${id}`, method: "PATCH", data: { ...data, decision: "REJECT" } })
};
