import { request } from "../../../app/apiClient";

export const bayApi = {
  getLookups: (params = {}) => request({ url: "/api/v1/admin/bays/lookups", method: "GET", params }),
  getList: (params = {}) => request({ url: "/api/v1/admin/bays", method: "GET", params }),
  getSummary: (params = {}) => request({ url: "/api/v1/admin/bays/summary", method: "GET", params }),
  getUsage: (params = {}) => request({ url: "/api/v1/admin/bays/usage", method: "GET", params }),
  getLayout: (params = {}) => request({ url: "/api/v1/admin/bays/layout", method: "GET", params }),
  getAlerts: (params = {}) => request({ url: "/api/v1/admin/bays/alerts", method: "GET", params }),
  getBayVehicles: (bayId) => request({ url: `/api/v1/admin/bays/${bayId}/vehicles`, method: "GET" }),
  getBayHistory: (bayId, params = {}) =>
    request({ url: `/api/v1/admin/bays/${bayId}/history`, method: "GET", params }),
  getSuggestions: (params = {}) => request({ url: "/api/v1/admin/bays/suggestions", method: "GET", params }),

  createBay: (data) => request({ url: "/api/v1/admin/bays", method: "POST", data }),
  updateBay: (id, data) => request({ url: `/api/v1/admin/bays/${id}`, method: "PUT", data }),
  hideBay: (id) => request({ url: `/api/v1/admin/bays/${id}`, method: "DELETE" }),
  lockBay: (id) => request({ url: `/api/v1/admin/bays/${id}/locks`, method: "PUT" }),
  unlockBay: (id) => request({ url: `/api/v1/admin/bays/${id}/locks`, method: "DELETE" }),
  updateBayStatus: (id, status) =>
    request({ url: `/api/v1/admin/bays/${id}/status`, method: "PATCH", data: { status } }),

  assignVehicle: (bayId, data) =>
    request({ url: `/api/v1/admin/bays/${bayId}/vehicle-assignments`, method: "POST", data }),
  removeVehicle: (bayId, data = {}) =>
    request({ url: `/api/v1/admin/bays/${bayId}/vehicle-removals`, method: "POST", data }),
  moveVehicle: (data) => request({ url: "/api/v1/admin/bays/bay-transfers", method: "POST", data }),
};

