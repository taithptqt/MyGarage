import { getApiBaseUrl, request, requestSafe } from "@app/apiClient";

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
  uploadInsuranceAsset: (file, assetType) => {
    const formData = new FormData();
    formData.append("file", file, file?.name || "insurance-image.jpg");
    formData.append("assetType", assetType);
    return request({
      url: "/api/service-orders/insurance-assets",
      method: "POST",
      data: formData,
      headers: { "Content-Type": "multipart/form-data" },
    });
  },
  createGroupSession: (data) => request({ url: "/api/group-sessions/service-orders", method: "POST", data }),
  update: (id, data) => request({ url: `/api/service-orders/${id}`, method: "PUT", data }),
  updateWorkshop: (id, data) => request({ url: `/api/service-orders/${id}/workshop`, method: "PUT", data }),
  delete: (id) => request({ url: `/api/service-orders/${id}`, method: "DELETE" }),

  // --- Service Order Details (Services & Parts) ---
  getDetails: (orderId) => request({ url: `/api/service-order-details`, method: "GET", params: { serviceOrderId: orderId } }),
  createDetail: (data) => request({ url: "/api/service-order-details", method: "POST", data }),
  updateDetail: (id, data) => request({ url: `/api/service-order-details/${id}`, method: "PUT", data }),
  deleteDetail: (id) => request({ url: `/api/service-order-details/${id}`, method: "DELETE" }),
  // --- Quotation ---
  getQuotationsByServiceOrder: (serviceOrderId) =>
    request({ url: `/api/service-orders/${serviceOrderId}/quotations`, method: "GET" }),
  getMandatoryOverridesByServiceOrder: (serviceOrderId) =>
    request({ url: `/api/service-orders/${serviceOrderId}/mandatory-overrides`, method: "GET" }),
  getQuotationById: (quotationId) =>
    request({ url: `/api/quotations/${quotationId}`, method: "GET" }),
  createQuotation: (serviceOrderId, data) =>
    request({ url: `/api/service-orders/${serviceOrderId}/quotations`, method: "POST", data }),
  submitQuotation: (quotationId, data) =>
    request({ url: `/api/quotations/${quotationId}/submit`, method: "POST", data }),
  confirmQuotationInsuranceApproval: (quotationId, data) =>
    request({ url: `/api/quotations/${quotationId}/insurance-approval`, method: "POST", data }),
  sendQuotationToCustomer: (quotationId, data) =>
    request({ url: `/api/quotations/${quotationId}/send-to-customer`, method: "POST", data }),
  exportQuotationExcel: async (quotationId) => {
    const exportUrl = `${getApiBaseUrl()}/api/quotations/${quotationId}/export/excel`;
    const res = await fetch(exportUrl, {
      credentials: "include",
    });

    if (res.status === 401) {
      throw new Error("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
    }

    if (!res.ok) {
      let message = "Không thể xuất file Excel từ server.";
      try {
        const contentType = String(res.headers.get("content-type") || "").toLowerCase();
        if (contentType.includes("application/json")) {
          const payload = await res.json();
          message = payload?.message || payload?.title || message;
        } else {
          const text = await res.text();
          if (text && text.trim()) {
            message = text.slice(0, 200);
          }
        }
      } catch {
        // ignore parse errors
      }
      throw new Error(`Export lỗi (${res.status}): ${message}`);
    }

    const contentType = String(res.headers.get("content-type") || "").toLowerCase();
    if (contentType.includes("application/json")) {
      const payload = await res.json().catch(() => null);
      throw new Error(payload?.message || "Server không trả về file xuất hợp lệ.");
    }

    const blob = await res.blob();
    const contentDisposition = String(res.headers.get("content-disposition") || "");
    const quotedNameMatch = contentDisposition.match(/filename\*?=(?:UTF-8''|")?([^\";]+)/i);
    const fileNameFromHeader = quotedNameMatch?.[1]
      ? decodeURIComponent(quotedNameMatch[1].replace(/"/g, "").trim())
      : "";

    return {
      blob,
      fileName: fileNameFromHeader || `BaoGia-${quotationId}.xlsx`,
    };
  },
  importQuotationExcel: async (quotationId, file, actorStaffId, note = "") => {
    if (!file) {
      throw new Error("Vui lòng chọn file Excel để nhập.");
    }

    const importUrl = `${getApiBaseUrl()}/api/quotations/${quotationId}/import/excel`;
    const formData = new FormData();
    formData.append("file", file, file?.name || "quotation-import.xlsx");
    if (Number(actorStaffId) > 0) {
      formData.append("actorStaffId", String(actorStaffId));
    }
    if (note && String(note).trim()) {
      formData.append("note", String(note).trim());
    }

    const res = await fetch(importUrl, {
      method: "POST",
      credentials: "include",
      body: formData,
    });

    if (res.status === 401) {
      throw new Error("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
    }

    if (!res.ok) {
      let message = "Không thể nhập file Excel từ server.";
      try {
        const contentType = String(res.headers.get("content-type") || "").toLowerCase();
        if (contentType.includes("application/json")) {
          const payload = await res.json();
          message = payload?.message || payload?.title || message;
        } else {
          const text = await res.text();
          if (text && text.trim()) {
            message = text.slice(0, 300);
          }
        }
      } catch {
        // ignore parse errors
      }

      throw new Error(`Nhập Excel lỗi (${res.status}): ${message}`);
    }

    const payload = await res.json().catch(() => null);
    return payload;
  },
  publishQuotationCustomerLink: (quotationId, data) =>
    request({ url: `/api/quotations/${quotationId}/publish-customer-link`, method: "POST", data }),
  uploadQuotationInsuranceEvidence: (quotationId, file, note = "") => {
    const formData = new FormData();
    formData.append("file", file, file?.name || "insurance-evidence.jpg");
    if (note) formData.append("note", note);
    return request({
      url: `/api/quotations/${quotationId}/insurance-evidence`,
      method: "POST",
      data: formData,
      headers: { "Content-Type": "multipart/form-data" },
    });
  },
  deleteQuotationInsuranceEvidence: (quotationId) =>
    request({ url: `/api/quotations/${quotationId}/insurance-evidence`, method: "DELETE" }),
  approveQuotation: (quotationId, data) =>
    request({ url: `/api/quotations/${quotationId}/approve`, method: "POST", data }),
  submitQuotationInternalApproval: (quotationId, data) =>
    request({ url: `/api/quotations/${quotationId}/internal-submit`, method: "POST", data }),
  financialApproveQuotation: (quotationId, data) =>
    request({ url: `/api/quotations/${quotationId}/financial-approval`, method: "POST", data }),
  getBranchQuotationApprovalPolicy: (branchId) =>
    request({ url: `/api/branches/${branchId}/quotation-approval-policy`, method: "GET" }),
  upsertBranchQuotationApprovalPolicy: (branchId, data) =>
    request({ url: `/api/branches/${branchId}/quotation-approval-policy`, method: "PUT", data }),
  cloneQuotation: (quotationId, data) =>
    request({ url: `/api/quotations/${quotationId}/clone`, method: "POST", data }),
  updateQuotationSplitBilling: (quotationId, data) =>
    request({ url: `/api/quotations/${quotationId}/split-billing`, method: "POST", data }),
  getQuotationDiscounts: (quotationId) =>
    request({ url: `/api/quotations/${quotationId}/discounts`, method: "GET" }),
  upsertQuotationDiscounts: (quotationId, data) =>
    request({ url: `/api/quotations/${quotationId}/discounts`, method: "PUT", data }),
  applyQuotationCustomerDecision: (quotationId, data) =>
    request({ url: `/api/quotations/${quotationId}/customer-decision`, method: "POST", data }),
  getQuotationByPublicToken: (token) =>
    request({ url: `/api/quotations/public/${token}`, method: "GET" }),
  sendQuotationPublicDecisionOtp: (token, data) =>
    request({ url: `/api/quotations/public/${token}/otp/send`, method: "POST", data }),
  applyQuotationPublicDecision: (token, data) =>
    request({ url: `/api/quotations/public/${token}/decision`, method: "POST", data }),
  decideMandatoryOverride: (requestId, data) =>
    request({ url: `/api/mandatory-overrides/${requestId}/decision`, method: "POST", data }),
  supersedeQuotation: (quotationId, data) =>
    request({ url: `/api/quotations/${quotationId}/supersede`, method: "POST", data }),
  getServiceBoms: (serviceId, vehicleModelId = null) =>
    request({
      url: "/api/service-boms",
      method: "GET",
      params: {
        serviceId,
        ...(vehicleModelId ? { vehicleModelId } : {}),
      },
    }),

  // --- Check-in Requests ---
  getCheckInRequests: () => request({ url: "/api/vehicle-condition-logs/condition-jobs", method: "GET", params: { requestType: "CHECK_IN" } }),
  createCheckInRequest: (serviceOrderId) =>
    request({ url: "/api/vehicle-condition-logs/condition-jobs", method: "POST", data: { serviceOrderId, requestType: "CHECK_IN" } }),
  acceptCheckInRequest: () =>
    Promise.reject(new Error("CHECK-IN do lễ tân xử lý trực tiếp, không phân công cho kỹ thuật viên.")),
  getCheckOutRequests: () => request({ url: "/api/vehicle-condition-logs/condition-jobs", method: "GET", params: { requestType: "CHECK_OUT" } }),
  createCheckOutRequest: (serviceOrderId) =>
    request({ url: "/api/vehicle-condition-logs/condition-jobs", method: "POST", data: { serviceOrderId, requestType: "CHECK_OUT" } }),
  acceptCheckOutRequest: () =>
    Promise.reject(new Error("CHECK-OUT do lễ tân xử lý trực tiếp, không phân công cho kỹ thuật viên.")),

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
  requestDiagnosisSafe: (serviceOrderId, payloadOrDispatchTag = null) => {
    if (payloadOrDispatchTag && typeof payloadOrDispatchTag === "object") {
      return requestSafe({
        url: `/api/diagnoses/service-orders/${serviceOrderId}/diagnosis-queues`,
        method: "POST",
        data: payloadOrDispatchTag,
      });
    }

    return requestSafe({
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
  getDiagnosisIntakePackages: () =>
    request({
      url: "/api/diagnoses/intake-packages",
      method: "GET",
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
  rejectDiagnosisTechnical: (serviceOrderId, data) =>
    request({
      url: `/api/diagnoses/service-orders/${serviceOrderId}/diagnosis-queues/technical-rejection`,
      method: "POST",
      data,
    }),
  cloneRejectedDiagnosisVersion: (serviceOrderId, data) =>
    request({
      url: `/api/diagnoses/service-orders/${serviceOrderId}/diagnosis-queues/clone-version`,
      method: "POST",
      data,
    }),
  approveDiagnosisFinal: (serviceOrderId, data) =>
    request({
      url: `/api/diagnoses/service-orders/${serviceOrderId}/diagnosis-queues/final-approval`,
      method: "POST",
      data,
    }),
  decideDiagnosisForeman: (serviceOrderId, data) =>
    request({
      url: `/api/diagnoses/service-orders/${serviceOrderId}/diagnosis-queues/foreman-decision`,
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

  // --- Diagnosis Recommendations ---
  getDiagnosisRecommendations: (diagnosisIdOrQuery) => {
    const isQueryObject =
      diagnosisIdOrQuery &&
      typeof diagnosisIdOrQuery === "object" &&
      !Array.isArray(diagnosisIdOrQuery);
    const params = isQueryObject
      ? diagnosisIdOrQuery
      : (diagnosisIdOrQuery ? { diagnosisId: diagnosisIdOrQuery } : undefined);
    return request({ url: "/api/diagnosis-recommendations", method: "GET", params });
  },
  createDiagnosisRecommendation: (data) =>
    request({ url: "/api/diagnosis-recommendations", method: "POST", data }),
  updateDiagnosisRecommendation: (id, data) =>
    request({ url: `/api/diagnosis-recommendations/${id}`, method: "PUT", data }),
  deleteDiagnosisRecommendation: (id) =>
    request({ url: `/api/diagnosis-recommendations/${id}`, method: "DELETE" }),
  getPendingSupplementaryProposals: (params) =>
    request({ url: "/api/diagnosis-recommendations/pending-supplementary", method: "GET", params }),
  // Backward-compatible aliases
  getDiagnoseRecommendations: (diagnoseId) =>
    request({ url: "/api/diagnosis-recommendations", method: "GET", params: diagnoseId ? { diagnosisId: diagnoseId } : undefined }),
  createDiagnoseRecommendation: (data) =>
    request({ url: "/api/diagnosis-recommendations", method: "POST", data }),
  updateDiagnoseRecommendation: (id, data) =>
    request({ url: `/api/diagnosis-recommendations/${id}`, method: "PUT", data }),
  deleteDiagnoseRecommendation: (id) =>
    request({ url: `/api/diagnosis-recommendations/${id}`, method: "DELETE" }),

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


