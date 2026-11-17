import { toNumber } from "../utils/accountingHelpers";

export function pick(item, ...keys) {
  for (const key of keys) {
    if (item && item[key] !== undefined && item[key] !== null) return item[key];
  }
  return null;
}

export function toId(value) {
  const n = Number(value);
  return Number.isFinite(n) && n > 0 ? Math.trunc(n) : null;
}

export function derivePayrollAmounts(item) {
  const readAmount = (...keys) => {
    const raw = pick(item, ...keys);
    return {
      present: raw !== undefined && raw !== null,
      value: raw !== undefined && raw !== null ? toNumber(raw) : 0,
    };
  };

  const explicitGrossPay = pick(item, "grossPay", "GrossPay");
  const explicitTotalDeduction = pick(item, "totalDeduction", "TotalDeduction");
  const explicitNetPay = pick(item, "netPay", "NetPay");

  const baseSalary = readAmount("baseSalary", "BaseSalary");
  const overtimeAmount = readAmount("overtimeAmount", "OvertimeAmount");
  const allowanceAmount = readAmount("allowanceAmount", "AllowanceAmount");
  const commissionAmount = readAmount("commissionAmount", "CommissionAmount");
  const bonusAmount = readAmount("bonusAmount", "BonusAmount");

  const attendanceDeduction = readAmount("attendanceDeduction", "AttendanceDeduction");
  const penaltyAmount = readAmount("penaltyAmount", "PenaltyAmount");
  const advanceDeduction = readAmount("advanceDeduction", "AdvanceDeduction");
  const loanDeduction = readAmount("loanDeduction", "LoanDeduction");
  const insuranceEmployeeAmount = readAmount("insuranceEmployeeAmount", "InsuranceEmployeeAmount");
  const pitAmount = readAmount("pitAmount", "PitAmount");
  const otherDeduction = readAmount("otherDeduction", "OtherDeduction");

  const hasIncomeComponents = [baseSalary, overtimeAmount, allowanceAmount, commissionAmount, bonusAmount].some(
    (part) => part.present,
  );
  const hasDeductionComponents = [
    attendanceDeduction,
    penaltyAmount,
    advanceDeduction,
    loanDeduction,
    insuranceEmployeeAmount,
    pitAmount,
    otherDeduction,
  ].some((part) => part.present);

  const derivedGrossPay = hasIncomeComponents
    ? baseSalary.value + overtimeAmount.value + allowanceAmount.value + commissionAmount.value + bonusAmount.value
    : null;
  const derivedTotalDeduction = hasDeductionComponents
    ? attendanceDeduction.value +
      penaltyAmount.value +
      advanceDeduction.value +
      loanDeduction.value +
      insuranceEmployeeAmount.value +
      pitAmount.value +
      otherDeduction.value
    : null;

  const hasOfficialGrossPay = explicitGrossPay !== undefined && explicitGrossPay !== null;
  const hasOfficialTotalDeduction = explicitTotalDeduction !== undefined && explicitTotalDeduction !== null;
  const hasOfficialNetPay = explicitNetPay !== undefined && explicitNetPay !== null;

  const grossPay = hasOfficialGrossPay ? toNumber(explicitGrossPay) : derivedGrossPay;
  const totalDeduction = hasOfficialTotalDeduction ? toNumber(explicitTotalDeduction) : derivedTotalDeduction;
  const netPay = hasOfficialNetPay
    ? toNumber(explicitNetPay)
    : grossPay != null && totalDeduction != null
    ? grossPay - totalDeduction
    : null;

  const amountsSource =
    hasOfficialGrossPay && hasOfficialTotalDeduction && hasOfficialNetPay
      ? "SERVER"
      : grossPay != null && totalDeduction != null && netPay != null
      ? "DERIVED"
      : "INCOMPLETE";

  return {
    grossPay,
    totalDeduction,
    netPay,
    hasOfficialGrossPay,
    hasOfficialTotalDeduction,
    hasOfficialNetPay,
    amountsSource,
  };
}

export function toDateInputValue(value) {
  const date = value ? new Date(value) : new Date();
  if (Number.isNaN(date.getTime())) return "";
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

export function parseDateValue(value) {
  if (!value) return null;
  const date = new Date(`${value}T00:00:00`);
  if (Number.isNaN(date.getTime())) return null;
  return date;
}

export function toFriendlyError(error, fallback) {
  const raw = String(error?.message || "").trim();
  const lower = raw.toLowerCase();

  if (lower.includes("chk_payroll_periods_dates")) {
    return "Ngày kỳ lương không hợp lệ. Quy tắc: Từ ngày <= Đến ngày và Ngày trả lương phải bằng hoặc sau ngày kết thúc kỳ.";
  }

  if (lower.includes("microsoft.data.sqlclient.sqlexception") || lower.includes(" at microsoft.")) {
    return fallback;
  }

  if (raw.length > 280) {
    return fallback;
  }

  return raw || fallback;
}

export function createDefaultPeriodForm() {
  const now = new Date();
  const first = new Date(now.getFullYear(), now.getMonth(), 1);
  const last = new Date(now.getFullYear(), now.getMonth() + 1, 0);
  const payroll = new Date(last);
  payroll.setDate(payroll.getDate() + 1);
  return {
    periodCode: `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}`,
    startDate: toDateInputValue(first),
    endDate: toDateInputValue(last),
    payrollDate: toDateInputValue(payroll),
    status: "OPEN",
  };
}

export function statusColor(status) {
  const code = String(status || "").toUpperCase();
  if (code === "PAID") return "green";
  if (code === "APPROVED") return "blue";
  if (code === "CANCELED") return "red";
  if (code === "OPEN") return "green";
  if (code === "CLOSED") return "gray";
  if (code === "LOCKED") return "purple";
  return "orange";
}

export function statusLabel(status) {
  const code = String(status || "").toUpperCase();
  if (code === "DRAFT") return "Nháp";
  if (code === "APPROVED") return "Đã duyệt";
  if (code === "PAID") return "Đã chi";
  if (code === "CANCELED") return "Đã huỷ";
  if (code === "OPEN") return "Đang mở";
  if (code === "LOCKED") return "Tạm khóa";
  if (code === "CLOSED") return "Đã đóng";
  if (!code) return "--";
  return code;
}

export function resolveEffectivePeriodStatus(rawStatus, runStatuses) {
  const normalizedRawStatus = String(rawStatus || "").toUpperCase();
  if (normalizedRawStatus === "LOCKED" || normalizedRawStatus === "CLOSED") {
    return normalizedRawStatus;
  }

  const normalizedRunStatuses = (runStatuses || [])
    .map((status) => String(status || "").toUpperCase())
    .filter(Boolean);
  if (!normalizedRunStatuses.length) return normalizedRawStatus || "OPEN";

  if (normalizedRunStatuses.some((status) => status === "DRAFT" || status === "CALCULATED")) {
    return "OPEN";
  }

  const nonCanceledStatuses = normalizedRunStatuses.filter((status) => status !== "CANCELED");
  if (!nonCanceledStatuses.length) return normalizedRawStatus || "OPEN";

  if (nonCanceledStatuses.every((status) => status === "PAID" || status === "LOCKED")) {
    return "PAID";
  }

  if (nonCanceledStatuses.some((status) => status === "APPROVED")) {
    return "APPROVED";
  }

  if (nonCanceledStatuses.some((status) => status === "PAID" || status === "LOCKED")) {
    return "PAID";
  }

  return normalizedRawStatus || "OPEN";
}

export function resolveEffectivePayrollItemStatus(rawItemStatus, runStatus) {
  const normalizedItemStatus = String(rawItemStatus || "").toUpperCase();
  const normalizedRunStatus = String(runStatus || "").toUpperCase();

  if (normalizedRunStatus === "PAID" || normalizedRunStatus === "LOCKED") {
    return "PAID";
  }

  if (
    normalizedRunStatus === "APPROVED" &&
    (!normalizedItemStatus || normalizedItemStatus === "DRAFT" || normalizedItemStatus === "CALCULATED")
  ) {
    return "APPROVED";
  }

  if (normalizedItemStatus) return normalizedItemStatus;
  if (normalizedRunStatus === "APPROVED") return "APPROVED";
  if (normalizedRunStatus === "PAID" || normalizedRunStatus === "LOCKED") return "PAID";
  return "DRAFT";
}

export function safeTime(value) {
  const ts = Date.parse(String(value || ""));
  return Number.isNaN(ts) ? 0 : ts;
}

export function normalizePeriodCode(value) {
  return String(value || "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/\s+/g, "")
    .trim()
    .toUpperCase();
}

export const TENURE_BRACKETS = [
  { minYears: 0, label: "Dưới 5 năm" },
  { minYears: 5, label: "5 đến dưới 10 năm" },
  { minYears: 10, label: "10 đến dưới 15 năm" },
  { minYears: 15, label: "15 đến dưới 20 năm" },
  { minYears: 20, label: "20 đến dưới 25 năm" },
  { minYears: 25, label: "Từ 25 năm trở lên" },
];

export const PAYROLL_EXCLUDED_STAFF_STATUSES = new Set(["TERMINATED", "SUSPENDED", "INACTIVE"]);

export function tenureRangeLabel(yearsOfService) {
  const years = toNumber(yearsOfService);
  if (years < 0) return "--";
  let matched = TENURE_BRACKETS[0];
  TENURE_BRACKETS.forEach((bracket) => {
    if (years >= bracket.minYears) matched = bracket;
  });
  return matched?.label || "--";
}
