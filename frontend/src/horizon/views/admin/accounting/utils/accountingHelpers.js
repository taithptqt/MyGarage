export function toNumber(value) {
  const num = Number(value ?? 0);
  return Number.isFinite(num) ? num : 0;
}

const COMPLETED_PAYMENT_STATUSES = new Set(["COMPLETED", "PAID", "SUCCESS"]);
const OUTFLOW_PAYMENT_TYPES = new Set(["REFUND", "REVERSAL"]);

function paymentFieldOf(item, ...keys) {
  for (const key of keys) {
    if (item && item[key] !== undefined && item[key] !== null) return item[key];
  }
  return null;
}

function upper(value) {
  return String(value || "").trim().toUpperCase();
}

export function normalizeText(value) {
  return String(value ?? "").trim().toLowerCase();
}

export function normalizeDate(value) {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
}

export function formatCurrency(value, currency = "VND") {
  return new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency,
    maximumFractionDigits: 0
  }).format(toNumber(value));
}

export function formatNumber(value) {
  return new Intl.NumberFormat("vi-VN").format(toNumber(value));
}

export function formatDate(value) {
  const date = normalizeDate(value);
  if (!date) return "--";
  return new Intl.DateTimeFormat("vi-VN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit"
  }).format(date);
}

export function formatDateTime(value) {
  const date = normalizeDate(value);
  if (!date) return "--";
  return new Intl.DateTimeFormat("vi-VN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit"
  }).format(date);
}

export function sumBy(items, selector) {
  return (items || []).reduce((total, item) => total + toNumber(selector(item)), 0);
}

export function isCompletedPayment(item) {
  const status = upper(paymentFieldOf(item, "status", "Status"));
  return COMPLETED_PAYMENT_STATUSES.has(status);
}

export function isOutflowPayment(item) {
  const direction = upper(paymentFieldOf(item, "direction", "Direction"));
  if (direction === "OUTFLOW" || direction === "OUT") return true;
  if (direction === "INFLOW" || direction === "IN") return false;
  const type = upper(paymentFieldOf(item, "type", "Type"));
  return OUTFLOW_PAYMENT_TYPES.has(type);
}

export function isInflowPayment(item) {
  return !isOutflowPayment(item);
}

export function paymentAmount(item) {
  return toNumber(paymentFieldOf(item, "amount", "Amount"));
}

export function sumCompletedCollectedPayments(items) {
  return (items || []).reduce((total, item) => {
    if (!isCompletedPayment(item) || !isInflowPayment(item)) return total;
    return total + paymentAmount(item);
  }, 0);
}

export function sumCompletedPaymentByTypes(items, types = []) {
  const typeSet = new Set((types || []).map((type) => upper(type)));
  if (!typeSet.size) return 0;
  return (items || []).reduce((total, item) => {
    if (!isCompletedPayment(item)) return total;
    const paymentType = upper(paymentFieldOf(item, "type", "Type"));
    if (!typeSet.has(paymentType)) return total;
    return total + Math.abs(paymentAmount(item));
  }, 0);
}

export function countCompletedInflowWithoutInvoice(items) {
  return (items || []).filter((item) => {
    if (!isCompletedPayment(item) || !isInflowPayment(item)) return false;
    const invoiceId = paymentFieldOf(item, "invoiceId", "InvoiceId");
    return !invoiceId;
  }).length;
}

export function getCurrentUser() {
  try {
    const raw = localStorage.getItem("mygarage_user");
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

export function getCurrentUserId() {
  const user = getCurrentUser();
  const id = Number(user?.id ?? 0);
  return Number.isFinite(id) && id > 0 ? id : null;
}

export function getRiskColorScheme(risk) {
  const value = String(risk || "").toUpperCase();
  if (value.includes("HIGH")) return "red";
  if (value.includes("MEDIUM")) return "orange";
  return "green";
}

export function toMonthKey(value) {
  const date = normalizeDate(value);
  if (!date) return "";
  const month = `${date.getMonth() + 1}`.padStart(2, "0");
  return `${date.getFullYear()}-${month}`;
}
