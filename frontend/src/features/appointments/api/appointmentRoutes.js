export const APPOINTMENTS_BASE = "/api/appointments";

export const APPOINTMENTS_ROUTES = {
  base: APPOINTMENTS_BASE,
  mine: `${APPOINTMENTS_BASE}/customers/current`,
  availability: `${APPOINTMENTS_BASE}/slots`,
  slotHolds: `${APPOINTMENTS_BASE}/slot-holds`,
  slotHoldDetail: (holdToken) => `${APPOINTMENTS_BASE}/slot-holds/${holdToken}`,
  guest: `${APPOINTMENTS_BASE}/public-bookings`,
  branches: (branchId) => `${APPOINTMENTS_BASE}/branches/${branchId}`,
  detail: (id) => `${APPOINTMENTS_BASE}/${id}`,
  status: (id) => `${APPOINTMENTS_BASE}/${id}`,
  autoNoShow: (branchId) => `${APPOINTMENTS_BASE}/branches/${branchId}/no-show-cycles`,
  bookingHealth: (branchId) => `${APPOINTMENTS_BASE}/branches/${branchId}/health-metrics`,
  dispatchDashboard: (branchId) => `${APPOINTMENTS_BASE}/branches/${branchId}/allocation-metrics`,
  dispatchCycle: (branchId) => `${APPOINTMENTS_BASE}/branches/${branchId}/allocation-cycles`,
  overnightLock: (branchId) => `${APPOINTMENTS_BASE}/branches/${branchId}/overnight-slot-constraints`,
};
