import { request } from "../../../app/apiClient";

/**
 * SERVICE ORDER API
 * Handles the lifecycle of a garage service order from reception to completion.
 */

export const serviceOrderApi = {
  // --- Service Orders ---
  getAll: () => request({ url: "/api/ServiceOrder", method: "GET" }),
  getById: (id) => request({ url: `/api/ServiceOrder/${id}`, method: "GET" }),
  create: (data) => request({ url: "/api/ServiceOrder", method: "POST", data }),
  createWorkshop: (data) => request({ url: "/api/ServiceOrder/workshop-create", method: "POST", data }),
  update: (id, data) => request({ url: `/api/ServiceOrder/${id}`, method: "PUT", data }),
  delete: (id) => request({ url: `/api/ServiceOrder/${id}`, method: "DELETE" }),

  // --- Service Order Details (Services & Parts) ---
  getDetails: (orderId) => request({ url: `/api/ServiceOrderDetail`, method: "GET", params: { serviceOrderId: orderId } }),
  createDetail: (data) => request({ url: "/api/ServiceOrderDetail", method: "POST", data }),
  updateDetail: (id, data) => request({ url: `/api/ServiceOrderDetail/${id}`, method: "PUT", data }),
  deleteDetail: (id) => request({ url: `/api/ServiceOrderDetail/${id}`, method: "DELETE" }),

  // --- Check-in Requests ---
  getCheckInRequests: () => request({ url: "/api/VehicleConditionLog/checkin-requests", method: "GET" }),
  createCheckInRequest: (serviceOrderId) => request({ url: `/api/VehicleConditionLog/checkin-requests/${serviceOrderId}`, method: "POST" }),
  acceptCheckInRequest: (serviceOrderId, technicianId) => request({ url: `/api/VehicleConditionLog/checkin-requests/${serviceOrderId}/accept`, method: "POST", data: { technicianId } }),
  getCheckOutRequests: () => request({ url: "/api/VehicleConditionLog/checkout-requests", method: "GET" }),
  createCheckOutRequest: (serviceOrderId) => request({ url: `/api/VehicleConditionLog/checkout-requests/${serviceOrderId}`, method: "POST" }),
  acceptCheckOutRequest: (serviceOrderId, technicianId) => request({ url: `/api/VehicleConditionLog/checkout-requests/${serviceOrderId}/accept`, method: "POST", data: { technicianId } }),

  // --- Diagnosis ---
  getDiagnoses: (orderId) => request({ url: "/api/Diagnosis", method: "GET", params: orderId ? { serviceOrderId: orderId } : undefined }),
  getDiagnosisById: (id) => request({ url: `/api/Diagnosis/${id}`, method: "GET" }),
  createDiagnosis: (data) => request({ url: "/api/Diagnosis", method: "POST", data }),
  updateDiagnosis: (id, data) => request({ url: `/api/Diagnosis/${id}`, method: "PUT", data }),
  completeDiagnosis: (id, data) => request({ url: `/api/Diagnosis/${id}/complete`, method: "POST", data }),
  requestDiagnosis: (serviceOrderId) => request({ url: `/api/Diagnosis/requests/${serviceOrderId}`, method: "POST" }),
  recallDiagnosisRequest: (serviceOrderId) => request({ url: `/api/Diagnosis/requests/${serviceOrderId}/recall`, method: "POST" }),
  getDiagnosisRequests: () => request({ url: "/api/Diagnosis", method: "GET", params: { requestOnly: true } }),
  acceptDiagnosisRequest: (serviceOrderId, technicianId) => request({ url: `/api/Diagnosis/requests/${serviceOrderId}/accept`, method: "POST", data: { technicianId } }),

  // --- Diagnose Recommendations ---
  getDiagnoseRecommendations: (diagnoseId) => request({ url: "/api/DiagnoseRecommendation", method: "GET", params: diagnoseId ? { diagnoseId } : undefined }),
  createDiagnoseRecommendation: (data) => request({ url: "/api/DiagnoseRecommendation", method: "POST", data }),
  updateDiagnoseRecommendation: (id, data) => request({ url: `/api/DiagnoseRecommendation/${id}`, method: "PUT", data }),
  deleteDiagnoseRecommendation: (id) => request({ url: `/api/DiagnoseRecommendation/${id}`, method: "DELETE" }),

  // --- QC Inspections ---
  getQcInspections: (orderId) => request({ url: `/api/QcInspection`, method: "GET", params: { serviceOrderId: orderId } }),
  createQcInspection: (data) => request({ url: "/api/QcInspection", method: "POST", data }),

  // --- Staff Assignments ---
  getAssignments: (orderId) => request({ url: `/api/ServiceStaffAssignment`, method: "GET", params: { serviceOrderId: orderId } }),
  createAssignment: (data) => request({ url: "/api/ServiceStaffAssignment", method: "POST", data }),
  updateAssignment: (id, data) => request({ url: `/api/ServiceStaffAssignment/${id}`, method: "PUT", data }),
  deleteAssignment: (id) => request({ url: `/api/ServiceStaffAssignment/${id}`, method: "DELETE" }),

  // --- Helpers ---
  generateCode: () => `SO-${new Date().getTime().toString().slice(-6)}`,
};


