import { request, requestSafe } from "../../../../../app/apiClient";
import { parseInvoiceTaxSummary } from "./invoiceTaxSummary";

/**
 * Executes a request targeting the modern /api/v1 route first,
 * falling back to the legacy /api route if 404 is encountered.
 */
export async function requestWithDualRoute(v1Url, legacyUrl, options = {}) {
  try {
    return await request({ ...options, url: v1Url });
  } catch (err) {
    if ((err?.status === 404 || err?.response?.status === 404) && legacyUrl) {
      return await request({ ...options, url: legacyUrl });
    }
    throw err;
  }
}

/**
 * Safe version of requestWithDualRoute using requestSafe.
 */
export async function requestSafeWithDualRoute(v1Url, legacyUrl, options = {}) {
  const result = await requestSafe({ ...options, url: v1Url });
  if (!result.ok && (result.status === 404 || result.response?.status === 404) && legacyUrl) {
    return await requestSafe({ ...options, url: legacyUrl });
  }
  return result;
}

function normalizeListResponse(data) {
  if (Array.isArray(data)) return data;
  if (Array.isArray(data?.items)) return data.items;
  return [];
}

function normalizeFinanceRequestType(requestType) {
  const value = String(requestType || "").trim().toUpperCase();
  if (value === "REFUND") return "PAYMENT_REFUND";
  if (value === "REVERSAL") return "PAYMENT_REVERSAL";
  return value || "MANUAL_JOURNAL";
}

export async function getInvoices() {
  const data = await requestWithDualRoute("/api/v1/invoices", "/api/invoices", { method: "GET" });
  return normalizeListResponse(data);
}

export async function getInvoiceTaxSummary() {
  const data = await requestWithDualRoute(
    "/api/v1/invoices/tax-summary",
    "/api/invoices/tax-summary",
    { method: "GET" }
  );
  return parseInvoiceTaxSummary(data);
}

export async function getInvoiceDetails() {
  const data = await requestWithDualRoute("/api/v1/invoice-details", "/api/invoice-details", { method: "GET" });
  return normalizeListResponse(data);
}

export async function getPayments() {
  const data = await requestWithDualRoute("/api/v1/payments", "/api/payments", { method: "GET" });
  return normalizeListResponse(data);
}

export async function updatePayment(id, payload) {
  return requestWithDualRoute(`/api/v1/payments/${id}`, `/api/payments/${id}`, {
    method: "PUT",
    data: payload
  });
}

export async function transitionPaymentStatus(id, payload) {
  return requestWithDualRoute(
    `/api/v1/payments/${id}/transition-status`,
    `/api/payments/${id}/transition-status`,
    {
      method: "POST",
      data: payload
    }
  );
}

export async function getPaymentMethods() {
  const data = await requestWithDualRoute("/api/v1/payment-methods", "/api/payment-methods", { method: "GET" });
  return normalizeListResponse(data);
}

export async function getRevenueSnapshots() {
  const data = await requestWithDualRoute("/api/v1/revenue-snapshots", "/api/revenue-snapshots", { method: "GET" });
  return normalizeListResponse(data);
}

export async function getBranches() {
  const data = await requestWithDualRoute("/api/v1/branches", "/api/branches", { method: "GET" });
  return normalizeListResponse(data);
}

export async function getAuditLogs(params = {}) {
  const data = await requestWithDualRoute(
    "/api/v1/audit-logs/admin-changes",
    "/api/audit-logs/admin-changes",
    {
      method: "GET",
      params
    }
  );
  return normalizeListResponse(data);
}

export async function fetchOptionalCollection(url, params) {
  const v1Url = url.startsWith("/api/") ? `/api/v1/${url.slice(5)}` : url;
  const result = await requestSafeWithDualRoute(v1Url, url, { method: "GET", params });
  if (result.ok) {
    return { available: true, items: normalizeListResponse(result.data), raw: result.data };
  }
  return { available: false, items: [], raw: result.data };
}

export async function createAccountingTableRow(tableKey, payload) {
  return requestWithDualRoute(
    `/api/v1/accounting/table/${tableKey}`,
    `/api/accounting/table/${tableKey}`,
    {
      method: "POST",
      data: payload
    }
  );
}

export async function updateAccountingTableRow(tableKey, id, payload) {
  return requestWithDualRoute(
    `/api/v1/accounting/table/${tableKey}/${id}`,
    `/api/accounting/table/${tableKey}/${id}`,
    {
      method: "PUT",
      data: payload
    }
  );
}

export async function getAccountsPayable(params) {
  return fetchOptionalCollection("/api/accounting/table/accounts-payable", params);
}

export async function getSupplierInvoices(params) {
  return fetchOptionalCollection("/api/accounting/table/supplier-invoices", params);
}

export async function getSupplierPayments(params) {
  return fetchOptionalCollection("/api/accounting/table/supplier-payments", params);
}

export async function getAccountingPeriods(params) {
  return fetchOptionalCollection("/api/accounting/table/accounting-periods", params);
}

export async function getChartOfAccounts(params) {
  return fetchOptionalCollection("/api/accounting/table/chart-of-accounts", params);
}

export async function getJournalEntries(params) {
  return fetchOptionalCollection("/api/accounting/table/journal-entries", params);
}

export async function getJournalEntryLines(params) {
  return fetchOptionalCollection("/api/accounting/table/journal-entry-lines", params);
}

export async function getPayrollRuns(params) {
  return fetchOptionalCollection("/api/accounting/table/payroll-runs", params);
}

export async function getPayrollPeriods(params) {
  return fetchOptionalCollection("/api/accounting/table/payroll-periods", params);
}

export async function getPayrollItems(params) {
  return fetchOptionalCollection("/api/accounting/table/payroll-items", params);
}

export async function getPayslips(params) {
  return fetchOptionalCollection("/api/accounting/table/payslips", params);
}

export async function createPayrollPeriod(payload) {
  return requestWithDualRoute(
    "/api/v1/accounting/payroll-periods",
    "/api/accounting/payroll-periods",
    {
      method: "POST",
      data: payload
    }
  );
}

export async function createPayrollRun(payload) {
  return createAccountingTableRow("payroll-runs", payload);
}

export async function createPayrollItem(payload) {
  return createAccountingTableRow("payroll-items", payload);
}

export async function createPayslip(payload) {
  return requestWithDualRoute("/api/v1/accounting/payslips", "/api/accounting/payslips", {
    method: "POST",
    data: payload
  });
}

export async function getTaxFilings(params) {
  return fetchOptionalCollection("/api/accounting/table/tax-filings", params);
}

export async function getLegalDocuments(params) {
  return fetchOptionalCollection("/api/accounting/table/legal-documents", params);
}

export async function getDocumentAttachments(params) {
  return fetchOptionalCollection("/api/accounting/table/document-attachments", params);
}

export async function getTrialBalanceReport(params) {
  return fetchOptionalCollection("/api/accounting/reports/trial-balance", params);
}

export async function getSupplierPayableSummaryReport(params) {
  return fetchOptionalCollection("/api/accounting/reports/supplier-payable-summary", params);
}

export async function getPayrollRunSummaryReport(params) {
  return fetchOptionalCollection("/api/accounting/reports/payroll-run-summary", params);
}

export async function getFinanceApprovalRequests(params) {
  const primary = await fetchOptionalCollection("/api/accounting/table/finance-approval-requests", params);
  return { ...primary, source: "finance_approval_requests" };
}

export async function createFinanceApprovalRequest(payload) {
  const normalizedPayload = {
    ...payload,
    requestType: normalizeFinanceRequestType(payload?.requestType)
  };

  const primary = await requestSafeWithDualRoute(
    "/api/v1/accounting/table/finance-approval-requests",
    "/api/accounting/table/finance-approval-requests",
    {
      method: "POST",
      data: normalizedPayload
    }
  );
  return {
    ok: primary.ok,
    data: primary.data,
    source: primary.ok ? "finance_approval_requests" : null
  };
}

export async function updateFinanceApprovalRequest(id, payload) {
  const primary = await requestSafeWithDualRoute(
    `/api/v1/accounting/table/finance-approval-requests/${id}`,
    `/api/accounting/table/finance-approval-requests/${id}`,
    {
      method: "PUT",
      data: payload
    }
  );
  return {
    ok: primary.ok,
    data: primary.data,
    source: primary.ok ? "finance_approval_requests" : null
  };
}

export async function decideFinanceApproval(id, payload) {
  return requestWithDualRoute(
    `/api/v1/accounting/finance-approvals/${id}/decision`,
    `/api/accounting/finance-approvals/${id}/decision`,
    {
      method: "POST",
      data: payload
    }
  );
}

export async function postJournalEntry(id, postedBy) {
  return requestWithDualRoute(
    `/api/v1/accounting/journal-entries/${id}/post`,
    `/api/accounting/journal-entries/${id}/post`,
    {
      method: "POST",
      data: { postedBy }
    }
  );
}

export async function approvePayrollRun(id, approvedBy) {
  return requestWithDualRoute(
    `/api/v1/accounting/payroll-runs/${id}/approve`,
    `/api/accounting/payroll-runs/${id}/approve`,
    {
      method: "POST",
      data: { approvedBy }
    }
  );
}

export async function markPayrollRunPaid(id, paidBy) {
  return requestWithDualRoute(
    `/api/v1/accounting/payroll-runs/${id}/mark-paid`,
    `/api/accounting/payroll-runs/${id}/mark-paid`,
    {
      method: "POST",
      data: { paidBy }
    }
  );
}

export async function cancelPayrollRun(id, payload = {}) {
  return requestWithDualRoute(
    `/api/v1/accounting/payroll-runs/${id}/cancel`,
    `/api/accounting/payroll-runs/${id}/cancel`,
    {
      method: "POST",
      data: payload
    }
  );
}

export async function getStaffSalaryCoefficient(staffId, asOfDate) {
  const params = {};
  if (asOfDate) params.asOfDate = asOfDate;
  return requestWithDualRoute(
    `/api/v1/accounting/staff-salary/${staffId}/coefficient`,
    `/api/accounting/staff-salary/${staffId}/coefficient`,
    {
      method: "GET",
      params
    }
  );
}

export async function getStaffBaseSalary(staffId, asOfDate, options = {}) {
  const params = {};
  if (asOfDate) params.asOfDate = asOfDate;
  if (typeof options.includeTierFallback === "boolean") {
    params.includeTierFallback = options.includeTierFallback;
  }
  return requestWithDualRoute(
    `/api/v1/accounting/staff-salary/${staffId}/base`,
    `/api/accounting/staff-salary/${staffId}/base`,
    {
      method: "GET",
      params
    }
  );
}

export async function createInitialSalaryRequest(staffId, payload) {
  return requestWithDualRoute(
    `/api/v1/accounting/staff-salary/${staffId}/request-initial`,
    `/api/accounting/staff-salary/${staffId}/request-initial`,
    {
      method: "POST",
      data: payload
    }
  );
}

export async function calculatePayrollForAllStaff(payload = {}) {
  return requestWithDualRoute(
    "/api/v1/accounting/payroll-runs/calculate-all",
    "/api/accounting/payroll-runs/calculate-all",
    {
      method: "POST",
      data: payload
    }
  );
}

export async function getAccountingDashboardV2(params = {}) {
  return requestWithDualRoute(
    "/api/v1/accounting/dashboard-v2",
    "/api/accounting/dashboard-v2",
    {
      method: "GET",
      params
    }
  );
}

export async function exportAccountingDashboardV2Csv(params = {}) {
  return requestWithDualRoute(
    "/api/v1/accounting/dashboard-v2/export",
    "/api/accounting/dashboard-v2/export",
    {
      method: "GET",
      params,
      responseType: "blob"
    }
  );
}
