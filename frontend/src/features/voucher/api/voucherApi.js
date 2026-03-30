import { request, requestSafe } from "../../../app/apiClient";

export const voucherApi = {
  getVouchers: () => request({ url: "/api/Voucher", method: "GET" }),
  getVoucherById: (id) => request({ url: `/api/Voucher/${id}`, method: "GET" }),
  createVoucher: (data) => request({ url: "/api/Voucher", method: "POST", data }),
  updateVoucher: (id, data) => request({ url: `/api/Voucher/${id}`, method: "PUT", data }),
  activateVoucher: (id) => request({ url: `/api/Voucher/${id}/activate`, method: "POST" }),
  pauseVoucher: (id) => request({ url: `/api/Voucher/${id}/pause`, method: "POST" }),
  revokeVoucher: (id) => request({ url: `/api/Voucher/${id}/revoke`, method: "POST" }),
  getCampaigns: (params) => request({ url: "/api/voucher-campaigns", method: "GET", params }),
  getCampaignById: (id) => request({ url: `/api/voucher-campaigns/${id}`, method: "GET" }),
  createCampaign: (data) => request({ url: "/api/voucher-campaigns", method: "POST", data }),
  updateCampaign: (id, data) => request({ url: `/api/voucher-campaigns/${id}`, method: "PUT", data }),
  deleteCampaign: (id) => request({ url: `/api/voucher-campaigns/${id}`, method: "DELETE" }),
  assignCampaignVouchers: (id, data) =>
    request({ url: `/api/voucher-campaigns/${id}/vouchers`, method: "POST", data }),

  getRedemptions: (status) =>
    request({
      url: "/api/voucher-redemptions",
      method: "GET",
      params: status ? { status } : undefined
    }),
  checkVoucher: (data) => requestSafe({ url: "/api/voucher-redemptions/check", method: "POST", data }),
  redeemVoucher: (data) => requestSafe({ url: "/api/voucher-redemptions/redeem", method: "POST", data }),
  reverseRedemption: (id, data) =>
    requestSafe({ url: `/api/voucher-redemptions/${id}/reverse`, method: "POST", data }),

  getApprovalRequests: (status) =>
    request({
      url: "/api/voucher-approval-requests",
      method: "GET",
      params: status ? { status } : undefined
    }),
  createApprovalRequest: (data) => request({ url: "/api/voucher-approval-requests", method: "POST", data }),
  approveRequest: (id, data) =>
    request({ url: `/api/voucher-approval-requests/${id}/approve`, method: "POST", data }),
  rejectRequest: (id, data) =>
    request({ url: `/api/voucher-approval-requests/${id}/reject`, method: "POST", data })
};
