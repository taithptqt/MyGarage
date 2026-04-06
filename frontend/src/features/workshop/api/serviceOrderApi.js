import { request } from "@app/apiClient";

/**
 * SERVICE ORDER API
 * Handles the lifecycle of a garage service order from reception to completion.
 */

export const serviceOrderApi = {
  // --- Service Orders ---
  getAll: () => request({ url: "/api/service-orders", method: "GET" }),
  getById: (id) => request({ url: `/api/service-orders/${id}`, method: "GET" }),
  create: (data) => request({ url: "/api/service-orders", method: "POST", data }),
  createWorkshop: (data) => request({ url: "/api/service-orders", method: "POST", data }),
  createGroupSession: (data) => request({ url: "/api/group-sessions/service-orders", method: "POST", data }),
  update: (id, data) => request({ url: `/api/service-orders/${id}`, method: "PUT", data }),
  delete: (id) => request({ url: `/api/service-orders/${id}`, method: "DELETE" }),

  // --- Service Order Details (Services & Parts) ---
  getDetails: (orderId) => request({ url: `/api/service-order-details`, method: "GET", params: { serviceOrderId: orderId } }),
  createDetail: (data) => request({ url: "/api/service-order-details", method: "POST", data }),
  updateDetail: (id, data) => request({ url: `/api/service-order-details/${id}`, method: "PUT", data }),
  deleteDetail: (id) => request({ url: `/api/service-order-details/${id}`, method: "DELETE" }),

  // --- Check-in Requests ---
  getCheckInRequests: () => request({ url: "/api/vehicle-condition-logs/condition-jobs", method: "GET", params: { requestType: "CHECK_IN" } }),
  createCheckInRequest: (serviceOrderId) =>
    request({ url: "/api/vehicle-condition-logs/condition-jobs", method: "POST", data: { serviceOrderId, requestType: "CHECK_IN" } }),
  acceptCheckInRequest: (serviceOrderId, technicianId) =>
    request({
      url: `/api/vehicle-condition-logs/condition-jobs/${serviceOrderId}`,
      method: "PATCH",
      data: { requestType: "CHECK_IN", technicianId },
    }),
  getCheckOutRequests: () => request({ url: "/api/vehicle-condition-logs/condition-jobs", method: "GET", params: { requestType: "CHECK_OUT" } }),
  createCheckOutRequest: (serviceOrderId) =>
    request({ url: "/api/vehicle-condition-logs/condition-jobs", method: "POST", data: { serviceOrderId, requestType: "CHECK_OUT" } }),
  acceptCheckOutRequest: (serviceOrderId, technicianId) =>
    request({
      url: `/api/vehicle-condition-logs/condition-jobs/${serviceOrderId}`,
      method: "PATCH",
      data: { requestType: "CHECK_OUT", technicianId },
    }),

  // --- Diagnosis ---
  getDiagnoses: (orderId) => request({ url: "/api/diagnoses", method: "GET", params: orderId ? { serviceOrderId: orderId } : undefined }),
  getDiagnosisById: (id) => request({ url: `/api/diagnoses/${id}`, method: "GET" }),
  createDiagnosis: (data) => request({ url: "/api/diagnoses", method: "POST", data }),
  updateDiagnosis: (id, data) => request({ url: `/api/diagnoses/${id}`, method: "PUT", data }),
  completeDiagnosis: (id, data) => {
    const payload = {
      status: "COMPLETED",
      symptom: data?.symptom ?? null,
      rootCause: data?.rootCause ?? null,
      severity: data?.severity ?? null,
      serviceOrderId: data?.serviceOrderId ?? null,
      technicianId: data?.technicianId ?? null,
      updatedBy: data?.updatedBy ?? null,
      approvedByForemanId: data?.approvedByForemanId ?? null,
      diagnosedAt: data?.diagnosedAt ?? null,
    };

    if (!Number(id) || Number(id) <= 0) {
      return request({ url: "/api/diagnoses", method: "POST", data: payload });
    }

    return request({
      url: `/api/diagnoses/${id}`,
      method: "PATCH",
      data: payload,
    });
  },
  requestDiagnosis: (serviceOrderId, dispatchTag = null) =>
    request({
      url: `/api/diagnoses/service-orders/${serviceOrderId}/diagnosis-queues`,
      method: "POST",
      data: dispatchTag ? { dispatchTag } : undefined,
    }),
  recallDiagnosisRequest: (serviceOrderId) => request({ url: `/api/diagnoses/service-orders/${serviceOrderId}/diagnosis-queues`, method: "DELETE" }),
  getDiagnosisRequests: (teamId = null) =>
    request({
      url: "/api/diagnoses",
      method: "GET",
      params: { requestOnly: true, ...(teamId ? { teamId } : {}) },
    }),
  getDiagnosisDispatchBoard: (params) =>
    request({
      url: "/api/diagnoses/dispatch-board",
      method: "GET",
      params,
    }),
  acceptDiagnosisRequest: (serviceOrderId, technicianId, options = {}) =>
    request({
      url: `/api/diagnoses/service-orders/${serviceOrderId}/diagnosis-queues`,
      method: "PATCH",
      data: { technicianId, ...options },
    }),
  requestDiagnosisSupport: (serviceOrderId, data) =>
    request({
      url: `/api/diagnoses/service-orders/${serviceOrderId}/diagnosis-queues/support`,
      method: "POST",
      data,
    }),
  requestDiagnosisCancel: (serviceOrderId, data) =>
    request({
      url: `/api/diagnoses/service-orders/${serviceOrderId}/diagnosis-queues/cancel-requests`,
      method: "POST",
      data,
    }),
  decideDiagnosisCancel: (serviceOrderId, data) =>
    request({
      url: `/api/diagnoses/service-orders/${serviceOrderId}/diagnosis-queues/cancel-requests/decision`,
      method: "POST",
      data,
    }),
  overrideDiagnosisAssignment: (serviceOrderId, data) =>
    request({
      url: `/api/diagnoses/service-orders/${serviceOrderId}/diagnosis-queues/manager-override`,
      method: "POST",
      data,
    }),

  // --- Diagnose Recommendations ---
  getDiagnoseRecommendations: (diagnoseId) => request({ url: "/api/diagnose-recommendations", method: "GET", params: diagnoseId ? { diagnoseId } : undefined }),
  createDiagnoseRecommendation: (data) => request({ url: "/api/diagnose-recommendations", method: "POST", data }),
  updateDiagnoseRecommendation: (id, data) => request({ url: `/api/diagnose-recommendations/${id}`, method: "PUT", data }),
  deleteDiagnoseRecommendation: (id) => request({ url: `/api/diagnose-recommendations/${id}`, method: "DELETE" }),

  // --- QC Inspections ---
  getQcInspections: (orderId) => request({ url: `/api/qc-inspections`, method: "GET", params: { serviceOrderId: orderId } }),
  createQcInspection: (data) => request({ url: "/api/qc-inspections", method: "POST", data }),

  // --- Staff Assignments ---
  getAssignments: (orderId) => request({ url: `/api/service-staff-assignments`, method: "GET", params: { serviceOrderId: orderId } }),
  createAssignment: (data) => request({ url: "/api/service-staff-assignments", method: "POST", data }),
  updateAssignment: (id, data) => request({ url: `/api/service-staff-assignments/${id}`, method: "PUT", data }),
  deleteAssignment: (id) => request({ url: `/api/service-staff-assignments/${id}`, method: "DELETE" }),

  // --- Teams ---
  getTeams: (params) => request({ url: "/api/teams", method: "GET", params }),

  // --- Helpers ---
  generateCode: () => `SO-${new Date().getTime().toString().slice(-6)}`,
};


