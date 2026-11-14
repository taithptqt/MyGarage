import { request } from "@app/apiClient";
import { APPOINTMENTS_ROUTES } from "@features/appointments/api/appointmentRoutes";

export async function getAllAppointments(status) {
  return request({
    url: APPOINTMENTS_ROUTES.base,
    method: "GET",
    params: status ? { status } : undefined,
  });
}

export async function getBranchAppointments(branchId, status) {
  return request({
    url: APPOINTMENTS_ROUTES.branches(branchId),
    method: "GET",
    params: status ? { status } : undefined,
  });
}

export async function getAppointmentDetail(id) {
  return request({ url: APPOINTMENTS_ROUTES.detail(id), method: "GET" });
}

export async function getAppointmentEditCapabilities(id) {
  return request({ url: APPOINTMENTS_ROUTES.editCapabilities(id), method: "GET" });
}

export async function requestAppointmentEdit(id, payload) {
  return request({
    url: APPOINTMENTS_ROUTES.editRequests(id),
    method: "POST",
    data: payload,
  });
}

export async function getAppointmentEditRequests(id, status) {
  return request({
    url: APPOINTMENTS_ROUTES.editRequests(id),
    method: "GET",
    params: status ? { status } : undefined,
  });
}

export async function decideAppointmentEditRequest(id, requestId, payload) {
  return request({
    url: APPOINTMENTS_ROUTES.editRequestDecision(id, requestId),
    method: "PATCH",
    data: payload,
  });
}

export async function confirmAppointment(id) {
  return request({
    url: APPOINTMENTS_ROUTES.status(id),
    method: "PATCH",
    data: { status: "CONFIRMED" },
  });
}

export async function checkInAppointment(id, payload) {
  return request({
    url: APPOINTMENTS_ROUTES.status(id),
    method: "PATCH",
    data: { status: "CHECKED_IN", arrivedAt: payload?.arrivedAt },
  });
}

export async function arriveAppointment(id, payload) {
  return request({
    url: APPOINTMENTS_ROUTES.status(id),
    method: "PATCH",
    data: { status: "ARRIVED", arrivedAt: payload?.arrivedAt },
  });
}

export async function autoMarkNoShows(branchId, now) {
  return request({
    url: APPOINTMENTS_ROUTES.autoNoShow(branchId),
    method: "POST",
    params: now ? { now } : undefined,
  });
}

export async function getBranchBookingHealth(branchId, at) {
  return request({
    url: APPOINTMENTS_ROUTES.bookingHealth(branchId),
    method: "GET",
    params: at ? { at } : undefined,
  });
}

export async function getDispatchDashboard(branchId, at) {
  return request({
    url: APPOINTMENTS_ROUTES.dispatchDashboard(branchId),
    method: "GET",
    params: at ? { at } : undefined,
  });
}

export async function runDispatchCycle(branchId, at) {
  return request({
    url: APPOINTMENTS_ROUTES.dispatchCycle(branchId),
    method: "POST",
    params: at ? { at } : undefined,
  });
}

export async function lockOvernightSlots(branchId, businessDate) {
  return request({
    url: APPOINTMENTS_ROUTES.overnightLock(branchId),
    method: "POST",
    data: businessDate ? { businessDate } : {},
  });
}

export async function getReceptionAppointmentPage(params) {
  return request({ url: "/api/appointments/reception-list", method: "GET", params });
}
