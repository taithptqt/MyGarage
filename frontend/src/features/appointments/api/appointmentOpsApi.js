import { request } from "../../../app/apiClient";
import { APPOINTMENTS_ROUTES } from "./appointmentRoutes";

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

export async function confirmAppointment(id) {
  return request({ url: APPOINTMENTS_ROUTES.confirm(id), method: "PATCH" });
}

export async function checkInAppointment(id) {
  return request({ url: APPOINTMENTS_ROUTES.checkIn(id), method: "PATCH" });
}

export async function arriveAppointment(id, payload) {
  return request({
    url: APPOINTMENTS_ROUTES.arrive(id),
    method: "PATCH",
    data: payload,
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
