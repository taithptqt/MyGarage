import { request } from "@app/apiClient";

export const executionApi = {
  getBayTimeline: (params = {}) =>
    request({ url: "/api/v1/bays/timeline", method: "GET", params }),
  allocateBay: (bayId, data) =>
    request({ url: `/api/v1/bays/${bayId}/allocations`, method: "POST", data }),
  releaseBay: (bayId, params = {}) =>
    request({ url: `/api/v1/bays/${bayId}/allocations`, method: "DELETE", params }),

  createConsumableReturnBundle: (data) =>
    request({ url: "/api/v1/consumable-returns", method: "POST", data }),
  getConsumablesForOrder: (serviceOrderId) =>
    request({ url: `/api/v1/consumable-returns/service-orders/${serviceOrderId}`, method: "GET" }).catch(() => []),

  checkInTimesheet: (data) =>
    request({ url: "/api/v1/timesheets/check-ins", method: "POST", data }),
  checkOutTimesheet: (id, data) =>
    request({ url: `/api/v1/timesheets/${id}/check-outs`, method: "POST", data }),

  getTechnicianProductivity: (technicianId, params = {}) =>
    request({ url: `/api/v1/technicians/${technicianId}/productivity`, method: "GET", params }),
  getTechnicianProductivityReport: (params = {}) =>
    request({ url: "/api/v1/technicians/productivity-report", method: "GET", params }),

  getQcChecklistTemplates: (params = {}) =>
    request({ url: "/api/v1/qc-checklist-templates", method: "GET", params }),
  upsertQcInspectionItems: (inspectionId, data) =>
    request({ url: `/api/v1/qc-inspections/${inspectionId}/items`, method: "POST", data }),

  getStockAlerts: (params = {}) =>
    request({ url: "/api/v1/stock-alerts", method: "GET", params }),
  getLowStockProducts: (params = {}) =>
    request({ url: "/api/v1/products/low-stock", method: "GET", params }),

  scheduleMaintenanceReminder: (data) =>
    request({ url: "/api/v1/maintenance-reminders/schedules", method: "POST", data }),
  getMaintenanceRemindersDueToday: () =>
    request({ url: "/api/v1/maintenance-reminders/due-today", method: "GET" }),
  processMaintenanceRemindersDue: (params = {}) =>
    request({ url: "/api/v1/maintenance-reminders/due-dispatches", method: "POST", params }),

  /* ── Assignment lifecycle (A16 My Tasks) ── */
  startAssignment: (assignmentId, data = {}) =>
    request({ url: `/api/v1/service-staff-assignments/${assignmentId}/starts`, method: "POST", data }),
  pauseAssignment: (assignmentId) =>
    request({ url: `/api/v1/service-staff-assignments/${assignmentId}/pauses`, method: "POST" }),
  completeAssignment: (assignmentId, data = {}) =>
    request({ url: `/api/v1/service-staff-assignments/${assignmentId}/completions`, method: "POST", data }),

  /* ── Active timesheet for a staff member ── */
  getActiveTimesheet: (staffId) =>
    request({ url: "/api/v1/timesheets/active", method: "GET", params: { staffId } }),

  /* ── Bay management (A14 Bay Timeline) ── */
  getBays: (params = {}) =>
    request({ url: "/api/v1/bays", method: "GET", params }),
};


