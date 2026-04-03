import { request, requestSafe } from "../../../../../app/apiClient";

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
  const data = await request({ url: "/api/Invoice", method: "GET" });
  return normalizeListResponse(data);
}

export async function updateInvoice(id, payload) {
  return request({
    url: `/api/Invoice/${id}`,
    method: "PUT",
    data: payload
  });
}

export async function getInvoiceDetails() {
  const data = await request({ url: "/api/InvoiceDetail", method: "GET" });
  return normalizeListResponse(data);
}

export async function getPayments() {
  const data = await request({ url: "/api/Payment", method: "GET" });
  return normalizeListResponse(data);
}

export async function updatePayment(id, payload) {
  return request({
    url: `/api/Payment/${id}`,
    method: "PUT",
    data: payload
  });
}

export async function transitionPaymentStatus(id, payload) {
  return request({
    url: `/api/Payment/${id}/transition-status`,
    method: "POST",
    data: payload
  });
}

export async function getPaymentMethods() {
  const data = await request({ url: "/api/PaymentMethod", method: "GET" });
  return normalizeListResponse(data);
}

export async function getRevenueSnapshots() {
  const data = await request({ url: "/api/RevenueSnapshot", method: "GET" });
  return normalizeListResponse(data);
}

export async function getBranches() {
  const data = await request({ url: "/api/Branch", method: "GET" });
  return normalizeListResponse(data);
}

export async function getAuditLogs(params = {}) {
  const data = await request({
    url: "/api/AuditLog/admin-changes",
    method: "GET",
    params
  });
  return normalizeListResponse(data);
}

export async function fetchOptionalCollection(url) {
  const result = await requestSafe({ url, method: "GET" });
  if (result.ok) {
    return { available: true, items: normalizeListResponse(result.data), raw: result.data };
  }
  return { available: false, items: [], raw: result.data };
}

export async function createAccountingTableRow(tableKey, payload) {
  return request({
    url: `/api/Accounting/table/${tableKey}`,
    method: "POST",
    data: payload
  });
}

export async function updateAccountingTableRow(tableKey, id, payload) {
  return request({
    url: `/api/Accounting/table/${tableKey}/${id}`,
    method: "PUT",
    data: payload
  });
}

export async function getAccountsPayable() {
  return fetchOptionalCollection("/api/Accounting/table/accounts-payable");
}

export async function getSupplierInvoices() {
  return fetchOptionalCollection("/api/Accounting/table/supplier-invoices");
}

export async function getSupplierPayments() {
  return fetchOptionalCollection("/api/Accounting/table/supplier-payments");
}

export async function getAccountingPeriods() {
  return fetchOptionalCollection("/api/Accounting/table/accounting-periods");
}

export async function getChartOfAccounts() {
  return fetchOptionalCollection("/api/Accounting/table/chart-of-accounts");
}

export async function getJournalEntries() {
  return fetchOptionalCollection("/api/Accounting/table/journal-entries");
}

export async function getJournalEntryLines() {
  return fetchOptionalCollection("/api/Accounting/table/journal-entry-lines");
}

export async function getPayrollRuns() {
  return fetchOptionalCollection("/api/Accounting/table/payroll-runs");
}

export async function getPayrollPeriods() {
  return fetchOptionalCollection("/api/Accounting/table/payroll-periods");
}

export async function getPayrollItems() {
  return fetchOptionalCollection("/api/Accounting/table/payroll-items");
}

export async function getPayslips() {
  return fetchOptionalCollection("/api/Accounting/table/payslips");
}

export async function createPayrollPeriod(payload) {
  return createAccountingTableRow("payroll-periods", payload);
}

export async function createPayrollRun(payload) {
  return createAccountingTableRow("payroll-runs", payload);
}

export async function createPayrollItem(payload) {
  return createAccountingTableRow("payroll-items", payload);
}

export async function createPayslip(payload) {
  return createAccountingTableRow("payslips", payload);
}

export async function getTaxFilings() {
  return fetchOptionalCollection("/api/Accounting/table/tax-filings");
}

export async function getLegalDocuments() {
  return fetchOptionalCollection("/api/Accounting/table/legal-documents");
}

export async function getDocumentAttachments() {
  return fetchOptionalCollection("/api/Accounting/table/document-attachments");
}

export async function getTrialBalanceReport() {
  return fetchOptionalCollection("/api/Accounting/reports/trial-balance");
}

export async function getSupplierPayableSummaryReport() {
  return fetchOptionalCollection("/api/Accounting/reports/supplier-payable-summary");
}

export async function getPayrollRunSummaryReport() {
  return fetchOptionalCollection("/api/Accounting/reports/payroll-run-summary");
}

export async function getFinanceApprovalRequests() {
  const primary = await fetchOptionalCollection("/api/Accounting/table/finance-approval-requests");
  return { ...primary, source: "finance_approval_requests" };
}

export async function createFinanceApprovalRequest(payload) {
  const normalizedPayload = {
    ...payload,
    requestType: normalizeFinanceRequestType(payload?.requestType)
  };

  const primary = await requestSafe({
    url: "/api/Accounting/table/finance-approval-requests",
    method: "POST",
    data: normalizedPayload
  });
  return {
    ok: primary.ok,
    data: primary.data,
    source: primary.ok ? "finance_approval_requests" : null
  };
}

export async function updateFinanceApprovalRequest(id, payload) {
  const primary = await requestSafe({
    url: `/api/Accounting/table/finance-approval-requests/${id}`,
    method: "PUT",
    data: payload
  });
  return {
    ok: primary.ok,
    data: primary.data,
    source: primary.ok ? "finance_approval_requests" : null
  };
}

export async function decideFinanceApproval(id, payload) {
  return request({
    url: `/api/Accounting/finance-approvals/${id}/decision`,
    method: "POST",
    data: payload
  });
}

export async function postJournalEntry(id, postedBy) {
  return request({
    url: `/api/Accounting/journal-entries/${id}/post`,
    method: "POST",
    data: { postedBy }
  });
}

export async function approvePayrollRun(id, approvedBy) {
  return request({
    url: `/api/Accounting/payroll-runs/${id}/approve`,
    method: "POST",
    data: { approvedBy }
  });
}

export async function markPayrollRunPaid(id, paidBy) {
  return request({
    url: `/api/Accounting/payroll-runs/${id}/mark-paid`,
    method: "POST",
    data: { paidBy }
  });
}

export async function getStaffSalaryCoefficient(staffId, asOfDate) {
  const params = {};
  if (asOfDate) params.asOfDate = asOfDate;
  return request({
    url: `/api/Accounting/staff-salary/${staffId}/coefficient`,
    method: "GET",
    params
  });
}

export async function getStaffBaseSalary(staffId, asOfDate) {
  const params = {};
  if (asOfDate) params.asOfDate = asOfDate;
  return request({
    url: `/api/Accounting/staff-salary/${staffId}/base`,
    method: "GET",
    params
  });
}

export async function createInitialSalaryRequest(staffId, payload) {
  return request({
    url: `/api/Accounting/staff-salary/${staffId}/request-initial`,
    method: "POST",
    data: payload
  });
}

export async function calculatePayrollForAllStaff(payload = {}) {
  return request({
    url: "/api/Accounting/payroll-runs/calculate-all",
    method: "POST",
    data: payload
  });
}
