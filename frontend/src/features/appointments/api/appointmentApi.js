import { request } from "../../../app/apiClient";

// ── Available Slots ─────────────────────────────────────────────────────────
export async function getAvailableSlots(branchId, date) {
  return request({
    url: "/api/Appointment/available-slots",
    method: "GET",
    params: { branchId, date },
  });
}

// ── Customer Endpoints ──────────────────────────────────────────────────────
export async function getMyAppointments() {
  return request({ url: "/api/Appointment/my", method: "GET" });
}

export async function bookAppointment(payload) {
  return request({
    url: "/api/Appointment/book",
    method: "POST",
    data: payload,
  });
}

export async function bookGuestAppointment(payload) {
  return request({
    url: "/api/Appointment/book-guest",
    method: "POST",
    data: payload,
  });
}

// ── Staff / Admin Endpoints ─────────────────────────────────────────────────
export async function getAllAppointments(status) {
  return request({
    url: "/api/Appointment/all",
    method: "GET",
    params: status ? { status } : undefined,
  });
}

export async function getBranchAppointments(branchId, status) {
  return request({
    url: `/api/Appointment/branch/${branchId}`,
    method: "GET",
    params: status ? { status } : undefined,
  });
}

export async function getAppointmentDetail(id) {
  return request({ url: `/api/Appointment/detail/${id}`, method: "GET" });
}

export async function confirmAppointment(id) {
  return request({ url: `/api/Appointment/${id}/confirm`, method: "PUT" });
}

export async function cancelAppointment(id, reason) {
  return request({
    url: `/api/Appointment/${id}/cancel`,
    method: "PUT",
    data: { reason: reason || null },
  });
}

export async function checkInAppointment(id) {
  return request({ url: `/api/Appointment/${id}/check-in`, method: "PUT" });
}

// ── Reference Data (reuse existing endpoints) ───────────────────────────────
export async function getAllBranches() {
  return request({ url: "/api/Branch", method: "GET" });
}

export async function getAllStandardServices() {
  return request({ url: "/api/StandardService", method: "GET" });
}

export async function getVehicleBrands() {
  return request({ url: "/api/VehicleBrand", method: "GET" });
}

export async function getVehicleModels() {
  return request({ url: "/api/VehicleModel", method: "GET" });
}

// ── Products & Categories ────────────────────────────────────────────────────
export async function getAllProducts() {
  return request({ url: "/api/Product", method: "GET" });
}

export async function getAllCategories() {
  return request({ url: "/api/Category", method: "GET" });
}

export async function getAllServiceCategories() {
  return request({ url: "/api/ServiceCategory", method: "GET" });
}
