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

export const purchaseOrderApi = {
  getPurchaseOrders: (params = {}) =>
    requestWithDualRoute("/api/v1/purchase-orders", "/api/purchase-orders", {
      method: "GET",
      params,
    }),

  getPurchaseOrderById: (id) =>
    requestWithDualRoute(`/api/v1/purchase-orders/${id}`, `/api/purchase-orders/${id}`, {
      method: "GET",
    }),

  createPurchaseOrder: (data) =>
    requestWithDualRoute("/api/v1/purchase-orders", "/api/purchase-orders", {
      method: "POST",
      data,
    }),

  approvePurchaseOrder: (id, data = {}) =>
    requestWithDualRoute(`/api/v1/purchase-orders/${id}/approve`, `/api/purchase-orders/${id}/approve`, {
      method: "PUT",
      data,
    }),

  getReceiveHistory: (id) =>
    requestWithDualRoute(`/api/v1/purchase-orders/${id}/receive-history`, `/api/purchase-orders/${id}/receive-history`, {
      method: "GET",
    }),

  deletePurchaseOrder: (id) =>
    requestWithDualRoute(`/api/v1/purchase-orders/${id}`, `/api/purchase-orders/${id}`, {
      method: "DELETE",
    }),
};
