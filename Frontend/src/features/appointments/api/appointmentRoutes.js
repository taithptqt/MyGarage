export const APPOINTMENTS_BASE = "/api/v1/appointments";

export const APPOINTMENTS_ROUTES = {
  base: APPOINTMENTS_BASE,
  mine: `/api/v1/customers/current/appointments`,
  availability: `${APPOINTMENTS_BASE}/slots`,
  slotHolds: `${APPOINTMENTS_BASE}/slot-holds`,
  slotHoldDetail: (holdToken) => `${APPOINTMENTS_BASE}/slot-holds/${holdToken}`,
  guest: `${APPOINTMENTS_BASE}/public-bookings`,
  receptionBookings: `${APPOINTMENTS_BASE}/reception-bookings`,
  insuranceAssets: `${APPOINTMENTS_BASE}/insurance-assets`,
  branches: (branchId) => `/api/v1/branches/${branchId}/appointments`,
  detail: (id) => `${APPOINTMENTS_BASE}/${id}`,
  editCapabilities: (id) => `${APPOINTMENTS_BASE}/${id}/edit-capabilities`,
  editRequests: (id) => `${APPOINTMENTS_BASE}/${id}/edit-requests`,
  editRequestDecision: (id, requestId) => `${APPOINTMENTS_BASE}/${id}/edit-requests/${requestId}`,
  status: (id) => `${APPOINTMENTS_BASE}/${id}`,
  nearestSlot: (branchId) => `${APPOINTMENTS_BASE}/branches/${branchId}/nearest-slot`,
  autoNoShow: (branchId) => `${APPOINTMENTS_BASE}/branches/${branchId}/no-show-cycles`,
  bookingHealth: (branchId) => `${APPOINTMENTS_BASE}/branches/${branchId}/health-metrics`,
  dispatchDashboard: (branchId) => `${APPOINTMENTS_BASE}/branches/${branchId}/allocation-metrics`,
  dispatchCycle: (branchId) => `${APPOINTMENTS_BASE}/branches/${branchId}/allocation-cycles`,
  overnightLock: (branchId) => `${APPOINTMENTS_BASE}/branches/${branchId}/overnight-slot-constraints`,
};
