import { request } from "../../../app/apiClient";

// ── Available Slots ─────────────────────────────────────────────────────────
export async function getAvailableSlots(branchId, date, options = {}) {
  const params = { branchId, date };
  if (options.requestedServiceIds) {
    params.requestedServiceIds = options.requestedServiceIds;
  }
  if (options.bookingFlow) {
    params.bookingFlow = options.bookingFlow;
  }

  return request({
    url: "/api/Appointment/available-slots",
    method: "GET",
    params,
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

export async function arriveAppointment(id, payload) {
  return request({
    url: `/api/Appointment/${id}/arrive`,
    method: "PUT",
    data: payload,
  });
}

export async function autoMarkNoShows(branchId, now) {
  return request({
    url: `/api/Appointment/branch/${branchId}/auto-no-show`,
    method: "POST",
    params: now ? { now } : undefined,
  });
}

export async function getBranchBookingHealth(branchId, at) {
  return request({
    url: `/api/Appointment/branch/${branchId}/booking-health`,
    method: "GET",
    params: at ? { at } : undefined,
  });
}

export async function getDispatchDashboard(branchId, at) {
  return request({
    url: `/api/Appointment/branch/${branchId}/dispatch-dashboard`,
    method: "GET",
    params: at ? { at } : undefined,
  });
}

export async function runDispatchCycle(branchId, at) {
  return request({
    url: `/api/Appointment/branch/${branchId}/dispatch-cycle`,
    method: "POST",
    params: at ? { at } : undefined,
  });
}

export async function lockOvernightSlots(branchId, businessDate) {
  return request({
    url: `/api/Appointment/branch/${branchId}/overnight-lock`,
    method: "POST",
    data: businessDate ? { businessDate } : {},
  });
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

// ── Customer/Vehicle for booking flow ───────────────────────────────────────
export async function getAllCustomers() {
  return request({ url: "/api/Customer", method: "GET" });
}

export async function updateCustomer(id, payload) {
  return request({
    url: `/api/Customer/${id}`,
    method: "PUT",
    data: payload,
  });
}

export async function getAllCustomerVehicles() {
  return request({ url: "/api/CustomerVehicle", method: "GET" });
}

export async function getAllVehicles() {
  return request({ url: "/api/Vehicle", method: "GET" });
}

export async function createVehicle(payload) {
  return request({
    url: "/api/Vehicle",
    method: "POST",
    data: payload,
  });
}

export async function createCustomerVehicle(payload) {
  return request({
    url: "/api/CustomerVehicle",
    method: "POST",
    data: payload,
  });
}
