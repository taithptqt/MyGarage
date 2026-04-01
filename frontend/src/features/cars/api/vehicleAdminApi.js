import { request } from "../../../app/apiClient";

export const vehicleAdminApi = {
  getLookups: (params = {}) => request({ url: "/api/vehicle-admin/lookups", method: "GET", params }),
  getList: (params = {}) => request({ url: "/api/vehicle-admin/list", method: "GET", params }),
  getSummary: (params = {}) => request({ url: "/api/vehicle-admin/summary", method: "GET", params }),
  getAlerts: (params = {}) => request({ url: "/api/vehicle-admin/alerts", method: "GET", params }),
  getHistory: (params = {}) => request({ url: "/api/vehicle-admin/history", method: "GET", params }),
  getVehicleDetail: (id, params = {}) =>
    request({ url: `/api/vehicle-admin/vehicles/${id}`, method: "GET", params }),

  createVehicle: (data) => request({ url: "/api/vehicle-admin/vehicles", method: "POST", data }),
  updateVehicle: (id, data) => request({ url: `/api/vehicle-admin/vehicles/${id}`, method: "PUT", data }),
  lockEdit: (id) => request({ url: `/api/vehicle-admin/vehicles/${id}/lock-edit`, method: "POST" }),
  unlockEdit: (id) => request({ url: `/api/vehicle-admin/vehicles/${id}/unlock-edit`, method: "POST" }),
  hideVehicle: (id) => request({ url: `/api/vehicle-admin/vehicles/${id}/hide`, method: "POST" }),
  updateStatus: (id, status, note) =>
    request({ url: `/api/vehicle-admin/vehicles/${id}/status`, method: "POST", data: { status, note } }),
  changeOwner: (id, customerId, note) =>
    request({ url: `/api/vehicle-admin/vehicles/${id}/owner`, method: "POST", data: { customerId, note } }),
  assignStaff: (id, data) =>
    request({ url: `/api/vehicle-admin/vehicles/${id}/assign-staff`, method: "POST", data }),
  assignBay: (id, data) =>
    request({ url: `/api/vehicle-admin/vehicles/${id}/assign-bay`, method: "POST", data }),
  moveBay: (id, data) =>
    request({ url: `/api/vehicle-admin/vehicles/${id}/move-bay`, method: "POST", data }),
  removeBay: (id, data = {}) =>
    request({ url: `/api/vehicle-admin/vehicles/${id}/remove-bay`, method: "POST", data }),
};
