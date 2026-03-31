export const APPOINTMENTS_BASE = "/api/appointments";

export const APPOINTMENTS_ROUTES = {
  base: APPOINTMENTS_BASE,
  mine: `${APPOINTMENTS_BASE}/me`,
  availability: `${APPOINTMENTS_BASE}/availability`,
  guest: `${APPOINTMENTS_BASE}/guest`,
  groups: `${APPOINTMENTS_BASE}/groups`,
  branches: (branchId) => `${APPOINTMENTS_BASE}/branches/${branchId}`,
  detail: (id) => `${APPOINTMENTS_BASE}/${id}`,
  confirm: (id) => `${APPOINTMENTS_BASE}/${id}/confirm`,
  cancel: (id) => `${APPOINTMENTS_BASE}/${id}/cancel`,
  checkIn: (id) => `${APPOINTMENTS_BASE}/${id}/check-in`,
  arrive: (id) => `${APPOINTMENTS_BASE}/${id}/arrive`,
  autoNoShow: (branchId) => `${APPOINTMENTS_BASE}/branches/${branchId}/auto-no-show`,
  bookingHealth: (branchId) => `${APPOINTMENTS_BASE}/branches/${branchId}/booking-health`,
  dispatchDashboard: (branchId) => `${APPOINTMENTS_BASE}/branches/${branchId}/dispatch-dashboard`,
  dispatchCycle: (branchId) => `${APPOINTMENTS_BASE}/branches/${branchId}/dispatch-cycle`,
  overnightLock: (branchId) => `${APPOINTMENTS_BASE}/branches/${branchId}/overnight-lock`,
};
