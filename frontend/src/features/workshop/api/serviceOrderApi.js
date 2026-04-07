import { request } from "@app/apiClient";

/**
 * SERVICE ORDER API
 * Handles the lifecycle of a garage service order from reception to completion.
 */

export const serviceOrderApi = {
  // --- Service Orders ---
  getAll: () => request({ url: "/api/service-orders", method: "GET" }),
  getById: (id) => request({ url: `/api/service-orders/${id}`, method: "GET" }),
  getEditCapabilities: (id) => request({ url: `/api/service-orders/${id}/edit-capabilities`, method: "GET" }),
  getEditRequests: (id, status) =>
    request({ url: `/api/service-orders/${id}/edit-requests`, method: "GET", params: status ? { status } : undefined }),
  requestEdit: (id, data) => request({ url: `/api/service-orders/${id}/edit-requests`, method: "POST", data }),
  decideEditRequest: (id, requestId, data) =>
    request({ url: `/api/service-orders/${id}/edit-requests/${requestId}`, method: "PATCH", data }),
  create: (data) => request({ url: "/api/service-orders", method: "POST", data }),
  createWorkshop: (data) => request({ url: "/api/service-orders", method: "POST", data }),
  createWorkshopBatch: (data) => request({ url: "/api/service-orders/batches", method: "POST", data }),
  createGroupSession: (data) => request({ url: "/api/group-sessions/service-orders", method: "POST", data }),
  update: (id, data) => request({ url: `/api/service-orders/${id}`, method: "PUT", data }),
  updateWorkshop: (id, data) => request({ url: `/api/service-orders/${id}/workshop`, method: "PUT", data }),
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
  acceptCheckInRequest: () =>
    Promise.reject(new Error("CHECK-IN do le tan xu ly truc tiep, khong phan cong cho ky thuat vien.")),
  getCheckOutRequests: () => request({ url: "/api/vehicle-condition-logs/condition-jobs", method: "GET", params: { requestType: "CHECK_OUT" } }),
  createCheckOutRequest: (serviceOrderId) =>
    request({ url: "/api/vehicle-condition-logs/condition-jobs", method: "POST", data: { serviceOrderId, requestType: "CHECK_OUT" } }),
  acceptCheckOutRequest: () =>
    Promise.reject(new Error("CHECK-OUT do le tan xu ly truc tiep, khong phan cong cho ky thuat vien.")),

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
  requestDiagnosis: (serviceOrderId, payloadOrDispatchTag = null) => {
    if (payloadOrDispatchTag && typeof payloadOrDispatchTag === "object") {
      return request({
        url: `/api/diagnoses/service-orders/${serviceOrderId}/diagnosis-queues`,
        method: "POST",
        data: payloadOrDispatchTag,
      });
    }

    return request({
      url: `/api/diagnoses/service-orders/${serviceOrderId}/diagnosis-queues`,
      method: "POST",
      data: payloadOrDispatchTag ? { dispatchTag: payloadOrDispatchTag } : undefined,
    });
  },
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
  getDiagnosisDispatchSuggestion: (serviceOrderId, params) =>
    request({
      url: `/api/diagnoses/service-orders/${serviceOrderId}/dispatch-suggestions`,
      method: "GET",
      params,
    }),
  getDiagnosisIntakeSuggestion: (serviceOrderId, params) =>
    request({
      url: `/api/diagnoses/service-orders/${serviceOrderId}/intake-suggestions`,
      method: "GET",
      params,
    }),
  createDiagnosisHandoverSnapshot: (serviceOrderId, data) =>
    request({
      url: `/api/diagnoses/service-orders/${serviceOrderId}/handover-snapshots`,
      method: "POST",
      data,
    }),
  getLatestDiagnosisHandoverSnapshot: (serviceOrderId) =>
    request({
      url: `/api/diagnoses/service-orders/${serviceOrderId}/handover-snapshots/latest`,
      method: "GET",
    }),
  decideDiagnosisHandoverSnapshot: (serviceOrderId, snapshotId, data) =>
    request({
      url: `/api/diagnoses/service-orders/${serviceOrderId}/handover-snapshots/${snapshotId}/customer-decisions`,
      method: "POST",
      data,
    }),
  getDiagnosisIntakeConversionKpi: (params) =>
    request({
      url: "/api/diagnoses/kpis/intake-conversions",
      method: "GET",
      params,
    }),
  acceptDiagnosisRequest: (serviceOrderId, technicianId, options = {}) =>
    request({
      url: `/api/diagnoses/service-orders/${serviceOrderId}/diagnosis-queues`,
      method: "PATCH",
      data: { technicianId, ...options },
    }),
  startDiagnosis: (serviceOrderId, data) =>
    request({
      url: `/api/diagnoses/service-orders/${serviceOrderId}/diagnosis-queues/start`,
      method: "POST",
      data,
    }),
  approveDiagnosisTechnical: (serviceOrderId, data) =>
    request({
      url: `/api/diagnoses/service-orders/${serviceOrderId}/diagnosis-queues/technical-approval`,
      method: "POST",
      data,
    }),
  approveDiagnosisFinal: (serviceOrderId, data) =>
    request({
      url: `/api/diagnoses/service-orders/${serviceOrderId}/diagnosis-queues/final-approval`,
      method: "POST",
      data,
    }),
  requestDiagnosisSupport: (serviceOrderId, data) =>
    request({
      url: `/api/diagnoses/service-orders/${serviceOrderId}/diagnosis-queues/support`,
      method: "POST",
      data,
    }),
  selectDiagnosisSupportingTeam: (serviceOrderId, data) =>
    request({
      url: `/api/diagnoses/service-orders/${serviceOrderId}/diagnosis-queues/supporting-team-selections`,
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
  getBays: () => request({ url: "/api/bays", method: "GET" }),

  // --- Helpers ---
  generateCode: () => `SO-${new Date().getTime().toString().slice(-6)}`,
};


