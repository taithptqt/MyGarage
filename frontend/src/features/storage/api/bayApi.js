import { request } from "../../../app/apiClient";

export const bayApi = {
  getLookups: (params = {}) => request({ url: "/api/bay-admin/lookups", method: "GET", params }),
  getList: (params = {}) => request({ url: "/api/bay-admin/list", method: "GET", params }),
  getSummary: (params = {}) => request({ url: "/api/bay-admin/summary", method: "GET", params }),
  getUsage: (params = {}) => request({ url: "/api/bay-admin/usage", method: "GET", params }),
  getLayout: (params = {}) => request({ url: "/api/bay-admin/layout", method: "GET", params }),
  getAlerts: (params = {}) => request({ url: "/api/bay-admin/alerts", method: "GET", params }),
  getBayVehicles: (bayId) => request({ url: `/api/bay-admin/bays/${bayId}/vehicles`, method: "GET" }),
  getBayHistory: (bayId, params = {}) =>
    request({ url: `/api/bay-admin/bays/${bayId}/history`, method: "GET", params }),
  getSuggestions: (params = {}) => request({ url: "/api/bay-admin/suggestions", method: "GET", params }),

  createBay: (data) => request({ url: "/api/bay-admin/bays", method: "POST", data }),
  updateBay: (id, data) => request({ url: `/api/bay-admin/bays/${id}`, method: "PUT", data }),
  hideBay: (id) => request({ url: `/api/bay-admin/bays/${id}/hide`, method: "POST" }),
  lockBay: (id) => request({ url: `/api/bay-admin/bays/${id}/lock`, method: "POST" }),
  unlockBay: (id) => request({ url: `/api/bay-admin/bays/${id}/unlock`, method: "POST" }),
  updateBayStatus: (id, status) =>
    request({ url: `/api/bay-admin/bays/${id}/status`, method: "POST", data: { status } }),

  assignVehicle: (bayId, data) =>
    request({ url: `/api/bay-admin/bays/${bayId}/assign-vehicle`, method: "POST", data }),
  removeVehicle: (bayId, data = {}) =>
    request({ url: `/api/bay-admin/bays/${bayId}/remove-vehicle`, method: "POST", data }),
  moveVehicle: (data) => request({ url: "/api/bay-admin/move-vehicle", method: "POST", data }),
};

