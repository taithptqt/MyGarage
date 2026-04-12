import { request } from "@app/apiClient";

const BASE = "/api/customers/me";

export function getCustomerDashboard() {
  return request({ url: `${BASE}/dashboard`, method: "GET" });
}

export function getCustomerVehicles() {
  return request({ url: `${BASE}/vehicles`, method: "GET" });
}

export function createCustomerVehicle(body) {
  return request({ url: `${BASE}/vehicles`, method: "POST", data: body });
}

export function updateCustomerVehicle(vehicleId, body) {
  return request({ url: `${BASE}/vehicles/${vehicleId}`, method: "PUT", data: body });
}

export function getVehicleHistory(vehicleId) {
  return request({ url: `${BASE}/vehicles/${vehicleId}/history`, method: "GET" });
}

export function getCustomerNotifications({ unreadOnly } = {}) {
  const params = {};
  if (unreadOnly) params.unreadOnly = true;
  return request({ url: `${BASE}/notifications`, method: "GET", params });
}

export function markNotificationRead(id) {
  return request({ url: `${BASE}/notifications/${id}/read`, method: "POST" });
}

export function markAllNotificationsRead() {
  return request({ url: `${BASE}/notifications/read-all`, method: "POST" });
}

export function getVehicleLookups() {
  return request({ url: `${BASE}/vehicle-lookups`, method: "GET" });
}

export function getServiceOrderLinkContext(serviceOrderId) {
  return request({
    url: `${BASE}/service-orders/${serviceOrderId}/link-context`,
    method: "GET",
  });
}
