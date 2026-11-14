import { request } from "@app/apiClient";

export const executionApi = {
  getBayTimeline: (params = {}) =>
    request({ url: "/api/bays/timeline", method: "GET", params }),
  allocateBay: (bayId, data) =>
    request({ url: `/api/bays/${bayId}/allocate`, method: "POST", data }),
  releaseBay: (bayId, params = {}) =>
    request({ url: `/api/bays/${bayId}/release`, method: "DELETE", params }),

  createConsumableReturnBundle: (data) =>
    request({ url: "/api/consumable-returns", method: "POST", data }),
  getConsumablesForOrder: (serviceOrderId) =>
    request({ url: `/api/consumable-returns/service-orders/${serviceOrderId}`, method: "GET" }).catch(() => []),

  checkInTimesheet: (data) =>
    request({ url: "/api/timesheets/check-in", method: "POST", data }),
  checkOutTimesheet: (id, data) =>
    request({ url: `/api/timesheets/${id}/check-out`, method: "PUT", data }),

  getTechnicianProductivity: (technicianId, params = {}) =>
    request({ url: `/api/technicians/${technicianId}/productivity`, method: "GET", params }),
  getTechnicianProductivityReport: (params = {}) =>
    request({ url: "/api/technicians/productivity-report", method: "GET", params }),

  getQcChecklistTemplates: (params = {}) =>
    request({ url: "/api/qc-checklist-templates", method: "GET", params }),
  upsertQcInspectionItems: (inspectionId, data) =>
    request({ url: `/api/qc-inspections/${inspectionId}/items`, method: "POST", data }),

  getStockAlerts: (params = {}) =>
    request({ url: "/api/stock-alerts", method: "GET", params }),
  getLowStockProducts: (params = {}) =>
    request({ url: "/api/products/low-stock", method: "GET", params }),

  scheduleMaintenanceReminder: (data) =>
    request({ url: "/api/maintenance-reminders/schedule", method: "POST", data }),
  getMaintenanceRemindersDueToday: () =>
    request({ url: "/api/maintenance-reminders/due-today", method: "GET" }),
  processMaintenanceRemindersDue: (params = {}) =>
    request({ url: "/api/maintenance-reminders/process-due", method: "POST", params }),

  /* ── Assignment lifecycle (A16 My Tasks) ── */
  startAssignment: (assignmentId, data = {}) =>
    request({ url: `/api/service-staff-assignments/${assignmentId}/start`, method: "PUT", data }),
  pauseAssignment: (assignmentId) =>
    request({ url: `/api/service-staff-assignments/${assignmentId}/pause`, method: "PUT" }),
  completeAssignment: (assignmentId, data = {}) =>
    request({ url: `/api/service-staff-assignments/${assignmentId}/complete`, method: "PUT", data }),

  /* ── Active timesheet for a staff member ── */
  getActiveTimesheet: (staffId) =>
    request({ url: "/api/timesheets/active", method: "GET", params: { staffId } }),

  /* ── Bay management (A14 Bay Timeline) ── */
  getBays: (params = {}) =>
    request({ url: "/api/bays", method: "GET", params }),
};


