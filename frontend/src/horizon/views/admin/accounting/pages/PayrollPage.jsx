import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  Badge,
  Box,
  Button,
  Divider,
  Flex,
  FormControl,
  FormLabel,
  Input,
  Modal,
  ModalBody,
  ModalCloseButton,
  ModalContent,
  ModalFooter,
  ModalHeader,
  ModalOverlay,
  Select,
  SimpleGrid,
  Spinner,
  Table,
  Tbody,
  Td,
  Text,
  Th,
  Thead,
  Tr,
  useColorModeValue,
  useToast,
} from "@chakra-ui/react";
import Card from "components/card/Card";
import MiniStatistics from "components/card/MiniStatistics";
import { getAllStaff } from "../../../../../features/admin/api/staffApi";
import { useBranch } from "../../../../../context/BranchContext";
import {
  approvePayrollRun,
  calculatePayrollForAllStaff,
  createPayrollItem,
  createPayrollPeriod,
  createPayrollRun,
  createPayslip,
  createInitialSalaryRequest,
  getBranches,
  getPayrollItems,
  getPayrollPeriods,
  getPayrollRuns,
  getPayslips,
  getRevenueSnapshots,
  getStaffSalaryCoefficient,
  markPayrollRunPaid,
  getStaffBaseSalary,
} from "../api/accountingApi";
import {
  formatCurrency,
  formatDate,
  formatDateTime,
  getCurrentUserId,
  normalizeText,
  sumBy,
  toNumber,
} from "../utils/accountingHelpers";

function pick(item, ...keys) {
  for (const key of keys) {
    if (item && item[key] !== undefined && item[key] !== null) return item[key];
  }
  return null;
}

function toId(value) {
  const n = Number(value);
  return Number.isFinite(n) && n > 0 ? Math.trunc(n) : null;
}

function calcItem(item) {
  const baseSalary = toNumber(pick(item, "baseSalary", "BaseSalary"));
  const overtimeAmount = toNumber(pick(item, "overtimeAmount", "OvertimeAmount"));
  const allowanceAmount = toNumber(pick(item, "allowanceAmount", "AllowanceAmount"));
  const commissionAmount = toNumber(pick(item, "commissionAmount", "CommissionAmount"));
  const bonusAmount = toNumber(pick(item, "bonusAmount", "BonusAmount"));

  const attendanceDeduction = toNumber(pick(item, "attendanceDeduction", "AttendanceDeduction"));
  const penaltyAmount = toNumber(pick(item, "penaltyAmount", "PenaltyAmount"));
  const advanceDeduction = toNumber(pick(item, "advanceDeduction", "AdvanceDeduction"));
  const loanDeduction = toNumber(pick(item, "loanDeduction", "LoanDeduction"));
  const insuranceEmployeeAmount = toNumber(pick(item, "insuranceEmployeeAmount", "InsuranceEmployeeAmount"));
  const pitAmount = toNumber(pick(item, "pitAmount", "PitAmount"));
  const otherDeduction = toNumber(pick(item, "otherDeduction", "OtherDeduction"));

  const grossPay = baseSalary + overtimeAmount + allowanceAmount + commissionAmount + bonusAmount;
  const totalDeduction =
    attendanceDeduction + penaltyAmount + advanceDeduction + loanDeduction + insuranceEmployeeAmount + pitAmount + otherDeduction;
  const netPay = toNumber(pick(item, "netPay", "NetPay")) || grossPay - totalDeduction;

  return { grossPay, totalDeduction, netPay };
}

function toDateInputValue(value) {
  const date = value ? new Date(value) : new Date();
  if (Number.isNaN(date.getTime())) return "";
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function parseDateValue(value) {
  if (!value) return null;
  const date = new Date(`${value}T00:00:00`);
  if (Number.isNaN(date.getTime())) return null;
  return date;
}

function toFriendlyError(error, fallback) {
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

function createDefaultPeriodForm() {
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

function createDefaultItemForm() {
  return {
    staffId: "",
    baseSalary: "",
    dependentCount: "0",
    insuranceZone: "I",
    attendanceDeduction: "0",
    overtimeAmount: "0",
    allowanceAmount: "0",
    commissionAmount: "0",
    bonusAmount: "0",
    penaltyAmount: "0",
    advanceDeduction: "0",
    loanDeduction: "0",
    insuranceEmployeeAmount: "0",
    insuranceCompanyAmount: "0",
    pitAmount: "0",
    otherDeduction: "0",
  };
}

function statusColor(status) {
  const code = String(status || "").toUpperCase();
  if (code === "PAID") return "green";
  if (code === "APPROVED") return "blue";
  if (code === "OPEN") return "green";
  if (code === "CLOSED") return "gray";
  if (code === "LOCKED") return "purple";
  return "orange";
}

function statusLabel(status) {
  const code = String(status || "").toUpperCase();
  if (code === "DRAFT") return "Nháp";
  if (code === "APPROVED") return "Đã duyệt";
  if (code === "PAID") return "Đã chi";
  if (code === "OPEN") return "Đang mở";
  if (code === "LOCKED") return "Tạm khóa";
  if (code === "CLOSED") return "Đã đóng";
  if (!code) return "--";
  return code;
}

function resolveEffectivePeriodStatus(rawStatus, runStatuses) {
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

function resolveEffectivePayrollItemStatus(rawItemStatus, runStatus) {
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

function safeTime(value) {
  const ts = Date.parse(String(value || ""));
  return Number.isNaN(ts) ? 0 : ts;
}

const VIETNAM_PAYROLL_POLICY_PRE_2026 = {
  employeeBhxhRate: 0.08,
  employeeBhytRate: 0.015,
  employeeBhtnRate: 0.01,
  employerBhxhRate: 0.175,
  employerBhytRate: 0.03,
  employerBhtnRate: 0.01,
  employerOaidRate: 0.005,
  personalDeduction: 11000000,
  dependentDeduction: 4400000,
  socialInsuranceBaseReference: 2340000,
  regionalMinimumWageByZone: {
    I: 4960000,
    II: 4410000,
    III: 3860000,
    IV: 3450000,
  },
};

const VIETNAM_PAYROLL_POLICY_2026 = {
  ...VIETNAM_PAYROLL_POLICY_PRE_2026,
  personalDeduction: 15500000,
  dependentDeduction: 6200000,
};

const VIETNAM_PIT_BRACKETS = [
  { limit: 5000000, rate: 0.05 },
  { limit: 10000000, rate: 0.1 },
  { limit: 18000000, rate: 0.15 },
  { limit: 32000000, rate: 0.2 },
  { limit: 52000000, rate: 0.25 },
  { limit: 80000000, rate: 0.3 },
  { limit: Number.POSITIVE_INFINITY, rate: 0.35 },
];

function resolveVietnamPayrollPolicy(asOfDate) {
  const asOf = asOfDate ? new Date(`${asOfDate}T00:00:00`) : new Date();
  if (!Number.isNaN(asOf.getTime()) && asOf >= new Date("2026-01-01T00:00:00")) {
    return VIETNAM_PAYROLL_POLICY_2026;
  }
  return VIETNAM_PAYROLL_POLICY_PRE_2026;
}

function normalizeInsuranceZone(zone) {
  const text = String(zone || "").trim().toUpperCase();
  if (text === "II" || text === "III" || text === "IV") return text;
  return "I";
}

function normalizePeriodCode(value) {
  return String(value || "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/\s+/g, "")
    .trim()
    .toUpperCase();
}

function roundVnd(value) {
  const amount = Number(value || 0);
  return Number.isFinite(amount) ? Math.max(0, Math.round(amount)) : 0;
}

function calculatePitByProgressiveBracket(taxableIncome) {
  let remaining = Math.max(0, toNumber(taxableIncome));
  if (remaining <= 0) return 0;

  let lowerBound = 0;
  let totalTax = 0;
  VIETNAM_PIT_BRACKETS.forEach((bracket) => {
    if (remaining <= 0) return;
    const upperBound = bracket.limit;
    const range = upperBound - lowerBound;
    const taxableInRange = Math.min(remaining, range);
    if (taxableInRange > 0) {
      totalTax += taxableInRange * bracket.rate;
      remaining -= taxableInRange;
    }
    lowerBound = upperBound;
  });
  return roundVnd(totalTax);
}

function calculateVietnamStatutoryAmounts(baseSalaryForInsurance, grossIncome, dependentCount, policy, insuranceZone) {
  const resolvedPolicy = policy || VIETNAM_PAYROLL_POLICY_2026;
  const zone = normalizeInsuranceZone(insuranceZone);
  const normalizedDependents = Math.max(0, Math.trunc(toNumber(dependentCount)));
  const socialInsuranceCap = 20 * resolvedPolicy.socialInsuranceBaseReference;
  const unemploymentCap = 20 * toNumber(resolvedPolicy.regionalMinimumWageByZone?.[zone]);
  const baseSalary = Math.max(0, toNumber(baseSalaryForInsurance));
  const socialInsurableBase = Math.min(baseSalary, socialInsuranceCap);
  const unemploymentInsurableBase = Math.min(baseSalary, unemploymentCap);

  const employeeBhxh = roundVnd(socialInsurableBase * resolvedPolicy.employeeBhxhRate);
  const employeeBhyt = roundVnd(socialInsurableBase * resolvedPolicy.employeeBhytRate);
  const employeeBhtn = roundVnd(unemploymentInsurableBase * resolvedPolicy.employeeBhtnRate);
  const employeeInsuranceTotal = roundVnd(employeeBhxh + employeeBhyt + employeeBhtn);

  const employerBhxh = roundVnd(socialInsurableBase * resolvedPolicy.employerBhxhRate);
  const employerBhyt = roundVnd(socialInsurableBase * resolvedPolicy.employerBhytRate);
  const employerBhtn = roundVnd(unemploymentInsurableBase * resolvedPolicy.employerBhtnRate);
  const employerOaid = roundVnd(socialInsurableBase * resolvedPolicy.employerOaidRate);
  const employerInsuranceTotal = roundVnd(employerBhxh + employerBhyt + employerBhtn + employerOaid);

  const taxableIncome =
    Math.max(
      0,
      toNumber(grossIncome) -
      employeeInsuranceTotal -
      resolvedPolicy.personalDeduction -
      normalizedDependents * resolvedPolicy.dependentDeduction,
    );
  const pitAmount = calculatePitByProgressiveBracket(taxableIncome);

  return {
    employeeBhxh,
    employeeBhyt,
    employeeBhtn,
    employeeInsuranceTotal,
    employerBhxh,
    employerBhyt,
    employerBhtn,
    employerOaid,
    employerInsuranceTotal,
    taxableIncome: roundVnd(taxableIncome),
    pitAmount,
    dependentCount: normalizedDependents,
    insuranceZone: zone,
    socialInsurableBase: roundVnd(socialInsurableBase),
    unemploymentInsurableBase: roundVnd(unemploymentInsurableBase),
    socialInsuranceCap: roundVnd(socialInsuranceCap),
    unemploymentInsuranceCap: roundVnd(unemploymentCap),
    personalDeduction: roundVnd(resolvedPolicy.personalDeduction),
    dependentDeduction: roundVnd(resolvedPolicy.dependentDeduction),
  };
}

const TENURE_BRACKETS = [
  { minYears: 0, label: "Dưới 5 năm" },
  { minYears: 5, label: "5 đến dưới 10 năm" },
  { minYears: 10, label: "10 đến dưới 15 năm" },
  { minYears: 15, label: "15 đến dưới 20 năm" },
  { minYears: 20, label: "20 đến dưới 25 năm" },
  { minYears: 25, label: "Từ 25 năm trở lên" },
];

function tenureRangeLabel(yearsOfService) {
  const years = toNumber(yearsOfService);
  if (years < 0) return "--";
  let matched = TENURE_BRACKETS[0];
  TENURE_BRACKETS.forEach((bracket) => {
    if (years >= bracket.minYears) matched = bracket;
  });
  return matched?.label || "--";
}

export default function PayrollPage() {
  const { selectedBranchId, isBranchLocked } = useBranch();
  const toast = useToast();
  const textColor = useColorModeValue("secondaryGray.900", "white");
  const textColorSecondary = useColorModeValue("secondaryGray.600", "secondaryGray.300");
  const enforcedBranchId = isBranchLocked ? toId(selectedBranchId) : null;
  const targetBranchId = enforcedBranchId || toId(selectedBranchId);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [periods, setPeriods] = useState({ available: false, items: [] });
  const [runs, setRuns] = useState({ available: false, items: [] });
  const [items, setItems] = useState({ available: false, items: [] });
  const [payslips, setPayslips] = useState({ available: false, items: [] });
  const [snapshots, setSnapshots] = useState([]);
  const [staffs, setStaffs] = useState([]);
  const [branches, setBranches] = useState([]);

  const [search, setSearch] = useState("");
  const [runFilter, setRunFilter] = useState("ALL");
  const [busyRunId, setBusyRunId] = useState(null);
  const [busyItemId, setBusyItemId] = useState(null);
  const [issuingAllPayslips, setIssuingAllPayslips] = useState(false);
  const [savingPeriod, setSavingPeriod] = useState(false);
  const [savingItem, setSavingItem] = useState(false);
  const [calculatingBatchPayroll, setCalculatingBatchPayroll] = useState(false);
  const [applyingSalaryCoefficient, setApplyingSalaryCoefficient] = useState(false);
  const [submittingInitialSalaryRequest, setSubmittingInitialSalaryRequest] = useState(false);
  const [salaryCoefficientInfo, setSalaryCoefficientInfo] = useState(null);
  const salaryCoefficientRequestSeqRef = useRef(0);
  const [selectedPayrollItem, setSelectedPayrollItem] = useState(null);
  const [selectedPayrollSalaryInfo, setSelectedPayrollSalaryInfo] = useState(null);
  const [loadingPayrollDetail, setLoadingPayrollDetail] = useState(false);
  const [payrollDetailError, setPayrollDetailError] = useState("");
  const payrollDetailRequestSeqRef = useRef(0);

  const [periodForm, setPeriodForm] = useState(() => createDefaultPeriodForm());
  const [itemForm, setItemForm] = useState(() => createDefaultItemForm());
  const [initialSalaryRequest, setInitialSalaryRequest] = useState({
    baseSalary: "",
    reason: "",
  });

  function updatePeriodEndDate(nextEndDate) {
    setPeriodForm((prev) => {
      const next = { ...prev, endDate: nextEndDate };
      const endDate = parseDateValue(nextEndDate);
      const payrollDate = parseDateValue(prev.payrollDate);
      if (endDate && payrollDate && payrollDate < endDate) {
        const adjusted = new Date(endDate);
        next.payrollDate = toDateInputValue(adjusted);
      }
      return next;
    });
  }

  async function loadData() {
    setLoading(true);
    setError("");
    const [periodRes, runRes, itemRes, payslipRes, snapshotRes, staffRes, branchRes] = await Promise.allSettled([
      getPayrollPeriods(),
      getPayrollRuns(),
      getPayrollItems(),
      getPayslips(),
      getRevenueSnapshots(),
      getAllStaff(),
      getBranches(),
    ]);

    setPeriods(periodRes.status === "fulfilled" ? periodRes.value : { available: false, items: [] });
    setRuns(runRes.status === "fulfilled" ? runRes.value : { available: false, items: [] });
    setItems(itemRes.status === "fulfilled" ? itemRes.value : { available: false, items: [] });
    setPayslips(payslipRes.status === "fulfilled" ? payslipRes.value : { available: false, items: [] });
    setSnapshots(snapshotRes.status === "fulfilled" && Array.isArray(snapshotRes.value) ? snapshotRes.value : []);

    const staffList = staffRes.status === "fulfilled" && Array.isArray(staffRes.value) ? staffRes.value : [];
    const branchList = branchRes.status === "fulfilled" && Array.isArray(branchRes.value) ? branchRes.value : [];

    const scopedStaffs = targetBranchId
      ? staffList.filter((staff) => {
        const staffBranchId = toId(pick(staff, "branchId", "BranchId"));
        return !staffBranchId || staffBranchId === targetBranchId;
      })
      : staffList;

    const scopedBranches = targetBranchId
      ? branchList.filter((branch) => toId(pick(branch, "id", "Id", "branchId", "BranchId")) === targetBranchId)
      : branchList;

    setStaffs(scopedStaffs);
    setBranches(scopedBranches);

    if (periodRes.status === "rejected" && runRes.status === "rejected" && itemRes.status === "rejected") {
      setError("Không tải được dữ liệu tiền lương từ backend.");
    }
    setLoading(false);
  }

  useEffect(() => {
    loadData();
  }, [targetBranchId]);

  const staffNameById = useMemo(() => {
    const map = new Map();
    (staffs || []).forEach((staff) => {
      const id = toId(pick(staff, "id", "Id"));
      if (!id) return;
      map.set(id, pick(staff, "fullName", "FullName", "name", "Name", "email", "Email") || `Staff #${id}`);
    });
    return map;
  }, [staffs]);

  const branchNameById = useMemo(() => {
    const map = new Map();
    (branches || []).forEach((branch) => {
      const id = toId(pick(branch, "id", "Id", "branchId", "BranchId"));
      if (!id) return;
      map.set(id, pick(branch, "name", "Name") || `Branch #${id}`);
    });
    return map;
  }, [branches]);

  const payslipItemIds = useMemo(() => {
    const set = new Set();
    (payslips.items || []).forEach((slip) => {
      const payrollItemId = toId(pick(slip, "payrollItemId", "PayrollItemId"));
      if (payrollItemId) set.add(payrollItemId);
    });
    return set;
  }, [payslips.items]);

  const periodCodeById = useMemo(() => {
    const map = new Map();
    (periods.items || []).forEach((period) => {
      const id = toId(pick(period, "id", "Id"));
      if (!id) return;
      const periodCode = String(pick(period, "periodCode", "PeriodCode") || "").trim();
      if (!periodCode) return;
      map.set(id, periodCode);
    });
    return map;
  }, [periods.items]);

  const runRows = useMemo(() => {
    const normalized = (runs.items || []).map((run, idx) => {
      const id = toId(pick(run, "id", "Id")) || idx + 1;
      const status = String(pick(run, "status", "Status") || "DRAFT").toUpperCase();
      const payrollPeriodId = toId(pick(run, "payrollPeriodId", "PayrollPeriodId"));
      const periodCodeFromPeriod = payrollPeriodId ? periodCodeById.get(payrollPeriodId) : "";
      const periodCode = periodCodeFromPeriod || pick(run, "periodCode", "PeriodCode") || `KY-${payrollPeriodId || "--"}`;
      const branchId = toId(pick(run, "branchId", "BranchId"));
      const runNoValue = toId(pick(run, "runNo", "RunNo")) || 0;
      return {
        id,
        runNo: pick(run, "runNo", "RunNo", "runCode", "RunCode") || id,
        runNoValue,
        status,
        payrollPeriodId,
        periodCode,
        branchId,
        branchName: branchId ? branchNameById.get(branchId) || `Branch #${branchId}` : "--",
        createdAt: pick(run, "calculatedAt", "CalculatedAt", "createdAt", "CreatedAt"),
      };
    });
    if (!targetBranchId) return normalized;
    return normalized.filter((run) => run.branchId === targetBranchId);
  }, [branchNameById, periodCodeById, runs.items, targetBranchId]);

  const periodRunStatusesById = useMemo(() => {
    const map = new Map();
    runRows.forEach((run) => {
      const periodId = run.payrollPeriodId;
      if (!periodId) return;
      const statuses = map.get(periodId) || [];
      statuses.push(String(run.status || "").toUpperCase());
      map.set(periodId, statuses);
    });
    return map;
  }, [runRows]);

  const periodRows = useMemo(() => {
    const normalized = (periods.items || []).map((period, idx) => {
      const id = toId(pick(period, "id", "Id")) || idx + 1;
      const runStatuses = periodRunStatusesById.get(id) || [];
      const rawStatus = String(pick(period, "status", "Status") || "").toUpperCase();
      return {
        id,
        periodCode: pick(period, "periodCode", "PeriodCode") || `KY-${id}`,
        startDate: pick(period, "startDate", "StartDate"),
        endDate: pick(period, "endDate", "EndDate"),
        payrollDate: pick(period, "payrollDate", "PayrollDate"),
        status: resolveEffectivePeriodStatus(rawStatus, runStatuses),
      };
    });
    return normalized.sort((a, b) => {
      const byDate =
        safeTime(b.startDate || b.payrollDate || b.endDate) -
        safeTime(a.startDate || a.payrollDate || a.endDate);
      if (byDate !== 0) return byDate;
      return Number(b.id) - Number(a.id);
    });
  }, [periodRunStatusesById, periods.items]);

  const runById = useMemo(() => {
    const map = new Map();
    runRows.forEach((run) => map.set(run.id, run));
    return map;
  }, [runRows]);

  const periodById = useMemo(() => {
    const map = new Map();
    periodRows.forEach((period) => {
      const id = toId(period.id);
      if (!id) return;
      map.set(id, {
        id,
        periodCode: period.periodCode,
        startDate: period.startDate,
        endDate: period.endDate,
        payrollDate: period.payrollDate,
        status: period.status,
      });
    });
    return map;
  }, [periodRows]);

  const itemRows = useMemo(() => {
    const normalized = (items.items || []).map((item, idx) => {
      const id = toId(pick(item, "id", "Id")) || idx + 1;
      const payrollRunId = toId(pick(item, "payrollRunId", "PayrollRunId"));
      const staffId = toId(pick(item, "staffId", "StaffId"));
      const runRow = payrollRunId ? runById.get(payrollRunId) || null : null;
      const payrollPeriodId = runRow?.payrollPeriodId || null;
      const periodRow = payrollPeriodId ? periodById.get(payrollPeriodId) || null : null;
      const { grossPay, totalDeduction, netPay } = calcItem(item);
      const runStatus = runRow?.status || "DRAFT";
      const policyAsOfDate = toDateInputValue(
        periodRow?.endDate ||
        periodRow?.payrollDate ||
        new Date().toISOString(),
      );
      const baseSalary = toNumber(pick(item, "baseSalary", "BaseSalary"));
      const overtimeAmount = toNumber(pick(item, "overtimeAmount", "OvertimeAmount"));
      const allowanceAmount = toNumber(pick(item, "allowanceAmount", "AllowanceAmount"));
      const commissionAmount = toNumber(pick(item, "commissionAmount", "CommissionAmount"));
      const bonusAmount = toNumber(pick(item, "bonusAmount", "BonusAmount"));
      const attendanceDeduction = toNumber(pick(item, "attendanceDeduction", "AttendanceDeduction"));
      const penaltyAmount = toNumber(pick(item, "penaltyAmount", "PenaltyAmount"));
      const advanceDeduction = toNumber(pick(item, "advanceDeduction", "AdvanceDeduction"));
      const loanDeduction = toNumber(pick(item, "loanDeduction", "LoanDeduction"));
      const insuranceEmployeeAmount = toNumber(pick(item, "insuranceEmployeeAmount", "InsuranceEmployeeAmount"));
      const insuranceCompanyAmount = toNumber(pick(item, "insuranceCompanyAmount", "InsuranceCompanyAmount"));
      const pitAmount = toNumber(pick(item, "pitAmount", "PitAmount"));
      const otherDeduction = toNumber(pick(item, "otherDeduction", "OtherDeduction"));
      const dependentCount = Math.max(0, Math.trunc(toNumber(pick(item, "dependentCount", "DependentCount"))));
      const insuranceZone = normalizeInsuranceZone(pick(item, "insuranceZone", "InsuranceZone") || "I");
      const statutoryBreakdown = calculateVietnamStatutoryAmounts(
        baseSalary,
        grossPay,
        dependentCount,
        resolveVietnamPayrollPolicy(policyAsOfDate),
        insuranceZone,
      );
      return {
        id,
        payrollRunId,
        payrollPeriodId,
        periodCode: periodRow?.periodCode || "--",
        policyAsOfDate,
        runNo: runRow?.runNo || `DOT-${payrollRunId || "--"}`,
        staffId,
        staffName: staffId ? staffNameById.get(staffId) || `Staff #${staffId}` : "--",
        status: resolveEffectivePayrollItemStatus(pick(item, "status", "Status"), runStatus),
        runStatus,
        canIssuePayslip: runStatus === "APPROVED" || runStatus === "PAID",
        baseSalary,
        overtimeAmount,
        allowanceAmount,
        commissionAmount,
        bonusAmount,
        attendanceDeduction,
        penaltyAmount,
        advanceDeduction,
        loanDeduction,
        insuranceEmployeeAmount,
        insuranceCompanyAmount,
        pitAmount,
        otherDeduction,
        dependentCount,
        insuranceZone,
        employeeBhxh: statutoryBreakdown.employeeBhxh,
        employeeBhyt: statutoryBreakdown.employeeBhyt,
        employeeBhtn: statutoryBreakdown.employeeBhtn,
        grossPay,
        totalDeduction,
        netPay,
        hasPayslip: payslipItemIds.has(id),
      };
    });
    if (!targetBranchId) return normalized;
    return normalized.filter((item) => item.payrollRunId && runById.has(item.payrollRunId));
  }, [items.items, payslipItemIds, periodById, runById, staffNameById, targetBranchId]);

  const filteredItems = useMemo(() => {
    const normalizedRunFilter = normalizePeriodCode(runFilter);
    return itemRows.filter((item) => {
      const passRun = runFilter === "ALL" || normalizePeriodCode(item.periodCode) === normalizedRunFilter;
      const searchTarget = normalizeText([item.staffName, item.periodCode, item.status].join(" "));
      const passSearch = !search || searchTarget.includes(normalizeText(search));
      return passRun && passSearch;
    });
  }, [itemRows, runFilter, search]);

  const issuablePayslipCount = useMemo(
    () => filteredItems.filter((item) => item.canIssuePayslip && !item.hasPayslip).length,
    [filteredItems],
  );

  const payrollTotal = useMemo(() => sumBy(itemRows, (item) => item.netPay), [itemRows]);
  const revenueTotal = useMemo(
    () => sumBy(snapshots || [], (snapshot) => pick(snapshot, "totalRevenue", "TotalRevenue")),
    [snapshots],
  );
  const payrollRevenueRatio = revenueTotal > 0 ? (payrollTotal / revenueTotal) * 100 : 0;

  const openPeriod = useMemo(() => periodRows.find((period) => String(period.status || "").toUpperCase() === "OPEN"), [periodRows]);
  const openPeriodId = useMemo(() => toId(pick(openPeriod, "id", "Id")), [openPeriod]);
  const selectedPeriodForBatch = useMemo(() => {
    if (runFilter === "ALL") return openPeriod || null;
    const normalizedFilter = normalizePeriodCode(runFilter);
    const matched = periodRows.find((period) => normalizePeriodCode(period.periodCode) === normalizedFilter);
    return matched || openPeriod || null;
  }, [openPeriod, periodRows, runFilter]);
  const selectedPeriodIdForBatch = useMemo(
    () => toId(pick(selectedPeriodForBatch, "id", "Id")),
    [selectedPeriodForBatch],
  );
  const selectedPeriodCodeForBatch = useMemo(() => {
    const code = String(pick(selectedPeriodForBatch, "periodCode", "PeriodCode") || "").trim();
    if (code) return code;
    return selectedPeriodIdForBatch ? `KY-${selectedPeriodIdForBatch}` : "";
  }, [selectedPeriodForBatch, selectedPeriodIdForBatch]);
  const selectedPeriodStatusForBatch = useMemo(
    () => String(pick(selectedPeriodForBatch, "status", "Status") || "").toUpperCase(),
    [selectedPeriodForBatch],
  );
  const coefficientAsOfDate = useMemo(() => {
    const asOfDateRaw =
      pick(openPeriod, "endDate", "EndDate") ||
      pick(openPeriod, "payrollDate", "PayrollDate") ||
      new Date().toISOString();
    return toDateInputValue(asOfDateRaw);
  }, [openPeriod]);

  const runSummary = useMemo(() => {
    return {
      draft: runRows.filter((run) => run.status === "DRAFT").length,
      approved: runRows.filter((run) => run.status === "APPROVED").length,
      paid: runRows.filter((run) => run.status === "PAID").length,
    };
  }, [runRows]);

  const activeDraftRun = useMemo(() => {
    const drafts = runRows.filter((run) => {
      if (run.status !== "DRAFT") return false;
      if (!selectedPeriodIdForBatch || run.payrollPeriodId !== selectedPeriodIdForBatch) return false;
      if (targetBranchId && run.branchId !== targetBranchId) return false;
      return true;
    });
    if (!drafts.length) return null;
    return [...drafts].sort((a, b) => {
      const byTime = safeTime(b.createdAt) - safeTime(a.createdAt);
      if (byTime !== 0) return byTime;
      return Number(b.id) - Number(a.id);
    })[0];
  }, [runRows, selectedPeriodIdForBatch, targetBranchId]);

  const autoRunLabel = useMemo(() => {
    if (!selectedPeriodIdForBatch) return "Chưa có kỳ lương đang mở";
    if (selectedPeriodStatusForBatch !== "OPEN") {
      return `Kỳ ${selectedPeriodCodeForBatch || "--"} chưa ở trạng thái ĐANG MỞ`;
    }
    if (activeDraftRun) return `${activeDraftRun.runNo} · ${activeDraftRun.periodCode}`;
    return `Sẽ tự tạo đợt Nháp cho kỳ ${selectedPeriodCodeForBatch}`;
  }, [activeDraftRun, selectedPeriodCodeForBatch, selectedPeriodIdForBatch, selectedPeriodStatusForBatch]);

  const periodFilterOptions = useMemo(() => {
    const unique = new Set();
    const options = [];
    periodRows.forEach((period) => {
      const code = String(period.periodCode || "").trim();
      const normalizedCode = normalizePeriodCode(code);
      if (!code || !normalizedCode || unique.has(normalizedCode)) return;
      unique.add(normalizedCode);
      options.push(code);
    });
    return options;
  }, [periodRows]);

  useEffect(() => {
    if (
      runFilter !== "ALL" &&
      !periodFilterOptions.some((periodCode) => normalizePeriodCode(periodCode) === normalizePeriodCode(runFilter))
    ) {
      setRunFilter("ALL");
    }
  }, [periodFilterOptions, runFilter]);

  const effectiveBaseSalary = useMemo(() => {
    const baseSalary = toNumber(itemForm.baseSalary);
    const appliedSalary = toNumber(pick(salaryCoefficientInfo, "calculatedSalary"));
    const hasAppliedSalary = Boolean(pick(salaryCoefficientInfo, "hasBaseSalary")) && appliedSalary > 0;
    return hasAppliedSalary ? appliedSalary : baseSalary;
  }, [itemForm.baseSalary, salaryCoefficientInfo]);

  const payrollPolicy = useMemo(
    () => resolveVietnamPayrollPolicy(coefficientAsOfDate),
    [coefficientAsOfDate],
  );

  const grossIncomePreview = useMemo(() => {
    return roundVnd(
      effectiveBaseSalary +
      toNumber(itemForm.overtimeAmount) +
      toNumber(itemForm.allowanceAmount) +
      toNumber(itemForm.commissionAmount) +
      toNumber(itemForm.bonusAmount),
    );
  }, [
    effectiveBaseSalary,
    itemForm.overtimeAmount,
    itemForm.allowanceAmount,
    itemForm.commissionAmount,
    itemForm.bonusAmount,
  ]);

  const statutoryPreview = useMemo(
    () =>
      calculateVietnamStatutoryAmounts(
        effectiveBaseSalary,
        grossIncomePreview,
        itemForm.dependentCount,
        payrollPolicy,
        itemForm.insuranceZone,
      ),
    [effectiveBaseSalary, grossIncomePreview, itemForm.dependentCount, itemForm.insuranceZone, payrollPolicy],
  );

  useEffect(() => {
    const nextInsuranceEmployeeAmount = String(statutoryPreview.employeeInsuranceTotal);
    const nextPitAmount = String(statutoryPreview.pitAmount);
    const nextInsuranceCompanyAmount = String(statutoryPreview.employerInsuranceTotal);

    setItemForm((prev) => {
      if (
        prev.insuranceEmployeeAmount === nextInsuranceEmployeeAmount &&
        prev.pitAmount === nextPitAmount &&
        prev.insuranceCompanyAmount === nextInsuranceCompanyAmount
      ) {
        return prev;
      }

      return {
        ...prev,
        insuranceEmployeeAmount: nextInsuranceEmployeeAmount,
        pitAmount: nextPitAmount,
        insuranceCompanyAmount: nextInsuranceCompanyAmount,
      };
    });
  }, [
    statutoryPreview.employeeInsuranceTotal,
    statutoryPreview.pitAmount,
    statutoryPreview.employerInsuranceTotal,
  ]);

  const itemPreview = useMemo(
    () => calcItem({ ...itemForm, baseSalary: String(effectiveBaseSalary) }),
    [effectiveBaseSalary, itemForm],
  );

  const selectedPayrollBreakdown = useMemo(() => {
    if (!selectedPayrollItem) return null;

    const policyAsOfDate = selectedPayrollItem.policyAsOfDate || coefficientAsOfDate;
    const statutory = calculateVietnamStatutoryAmounts(
      selectedPayrollItem.baseSalary,
      selectedPayrollItem.grossPay,
      selectedPayrollItem.dependentCount,
      resolveVietnamPayrollPolicy(policyAsOfDate),
      selectedPayrollItem.insuranceZone,
    );

    const originalBaseSalary = toNumber(pick(selectedPayrollSalaryInfo, "baseSalary", "BaseSalary"));
    const coefficient = toNumber(pick(selectedPayrollSalaryInfo, "coefficient", "Coefficient")) || 1;
    const yearsOfService = Math.max(
      0,
      Math.trunc(toNumber(pick(selectedPayrollSalaryInfo, "yearsOfService", "YearsOfService"))),
    );

    return {
      policyAsOfDate,
      originalBaseSalary,
      hasOriginalBaseSalary: Boolean(pick(selectedPayrollSalaryInfo, "hasBaseSalary", "HasBaseSalary")) || originalBaseSalary > 0,
      coefficient,
      yearsOfService,
      tenureRange: tenureRangeLabel(yearsOfService),
      employeeBhxh: statutory.employeeBhxh,
      employeeBhyt: statutory.employeeBhyt,
      employeeBhtn: statutory.employeeBhtn,
      employeeInsuranceTotal:
        selectedPayrollItem.insuranceEmployeeAmount > 0
          ? selectedPayrollItem.insuranceEmployeeAmount
          : statutory.employeeInsuranceTotal,
      pitAmount: selectedPayrollItem.pitAmount,
      totalIncome: selectedPayrollItem.grossPay,
      totalDeduction: selectedPayrollItem.totalDeduction,
      netPay: selectedPayrollItem.netPay,
    };
  }, [coefficientAsOfDate, selectedPayrollItem, selectedPayrollSalaryInfo]);

  async function reloadRuns() {
    const data = await getPayrollRuns();
    setRuns(data);
  }

  async function reloadPeriods() {
    const data = await getPayrollPeriods();
    setPeriods(data);
  }

  async function reloadItemsAndPayslips() {
    const [itemData, payslipData] = await Promise.all([getPayrollItems(), getPayslips()]);
    setItems(itemData);
    setPayslips(payslipData);
  }

  async function ensureDraftRunId() {
    if (!openPeriodId) {
      throw new Error("Chưa có kỳ lương đang mở. Vui lòng tạo kỳ lương trước.");
    }

    if (activeDraftRun?.id) {
      return activeDraftRun.id;
    }

    const scopedRuns = runRows.filter((run) => {
      if (run.payrollPeriodId !== openPeriodId) return false;
      if (targetBranchId) return run.branchId === targetBranchId;
      return run.branchId == null;
    });
    const maxRunNo = scopedRuns.reduce((max, run) => Math.max(max, run.runNoValue || 0), 0);
    const nextRunNo = maxRunNo + 1;

    const payload = {
      payrollPeriodId: openPeriodId,
      runNo: nextRunNo,
      status: "DRAFT",
      calculatedAt: new Date().toISOString(),
    };
    if (targetBranchId) payload.branchId = targetBranchId;

    const created = await createPayrollRun(payload);
    const createdId = toId(pick(created, "id", "Id"));
    if (!createdId) {
      throw new Error("Không lấy được đợt lương vừa tạo.");
    }

    await reloadRuns();
    toast({ status: "success", title: `Đã tự tạo đợt nháp #${nextRunNo}.` });
    return createdId;
  }

  async function handleCreatePeriod() {
    if (!periodForm.periodCode.trim()) {
      setError("Vui lòng nhập mã kỳ lương.");
      return;
    }
    if (!periodForm.startDate || !periodForm.endDate || !periodForm.payrollDate) {
      setError("Vui lòng nhập đầy đủ ngày bắt đầu, kết thúc và ngày trả lương.");
      return;
    }

    const startDate = parseDateValue(periodForm.startDate);
    const endDate = parseDateValue(periodForm.endDate);
    const payrollDate = parseDateValue(periodForm.payrollDate);

    if (!startDate || !endDate || !payrollDate) {
      setError("Định dạng ngày không hợp lệ.");
      return;
    }

    if (startDate > endDate) {
      setError("Từ ngày phải nhỏ hơn hoặc bằng Đến ngày.");
      return;
    }

    if (payrollDate < endDate) {
      setError("Ngày trả lương phải bằng hoặc sau ngày kết thúc kỳ.");
      return;
    }

    setSavingPeriod(true);
    setError("");
    try {
      await createPayrollPeriod({
        periodCode: periodForm.periodCode.trim(),
        startDate: periodForm.startDate,
        endDate: periodForm.endDate,
        payrollDate: periodForm.payrollDate,
        status: periodForm.status,
      });
      toast({ status: "success", title: "Đã tạo kỳ lương mới." });
      setPeriodForm(createDefaultPeriodForm());
      setRunFilter("ALL");
      await reloadPeriods();
    } catch (err) {
      setError(toFriendlyError(err, "Tạo kỳ lương thất bại."));
    } finally {
      setSavingPeriod(false);
    }
  }

  async function handleCreateItem() {
    const staffId = toId(itemForm.staffId);
    if (!staffId) {
      setError("Vui lòng chọn nhân sự.");
      return;
    }
    if (!openPeriodId) {
      setError("Chưa có kỳ lương đang mở. Vui lòng tạo kỳ lương trước.");
      return;
    }

    setSavingItem(true);
    setError("");
    try {
      const payrollRunId = await ensureDraftRunId();
      await createPayrollItem({
        payrollRunId,
        staffId,
        baseSalary: effectiveBaseSalary,
        attendanceDeduction: toNumber(itemForm.attendanceDeduction),
        overtimeAmount: toNumber(itemForm.overtimeAmount),
        allowanceAmount: toNumber(itemForm.allowanceAmount),
        commissionAmount: toNumber(itemForm.commissionAmount),
        bonusAmount: toNumber(itemForm.bonusAmount),
        penaltyAmount: toNumber(itemForm.penaltyAmount),
        advanceDeduction: toNumber(itemForm.advanceDeduction),
        loanDeduction: toNumber(itemForm.loanDeduction),
        insuranceEmployeeAmount: toNumber(itemForm.insuranceEmployeeAmount),
        insuranceCompanyAmount: toNumber(itemForm.insuranceCompanyAmount),
        pitAmount: toNumber(itemForm.pitAmount),
        otherDeduction: toNumber(itemForm.otherDeduction),
        netPay: itemPreview.netPay,
        status: "DRAFT",
      });
      toast({ status: "success", title: "Đã thêm dòng lương." });
      setItemForm(createDefaultItemForm());
      setSalaryCoefficientInfo(null);
      setInitialSalaryRequest({ baseSalary: "", reason: "" });
      await reloadItemsAndPayslips();
    } catch (err) {
      setError(toFriendlyError(err, "Thêm dòng lương thất bại."));
    } finally {
      setSavingItem(false);
    }
  }

  async function handleCalculateAllStaffPayroll() {
    if (!selectedPeriodIdForBatch) {
      setError("Chưa xác định được kỳ lương để tính. Vui lòng chọn một kỳ đang mở.");
      return;
    }
    if (selectedPeriodStatusForBatch !== "OPEN") {
      setError(`Kỳ ${selectedPeriodCodeForBatch || "--"} chưa ở trạng thái ĐANG MỞ nên không thể tính lương hàng loạt.`);
      return;
    }

    setCalculatingBatchPayroll(true);
    setError("");
    try {
      const result = await calculatePayrollForAllStaff({
        payrollPeriodId: selectedPeriodIdForBatch,
        overwriteExisting: true,
        includeInactiveStaff: false,
        issuePayslips: false,
      });

      const inserted = Math.max(0, Math.trunc(toNumber(pick(result, "inserted", "Inserted"))));
      const updated = Math.max(0, Math.trunc(toNumber(pick(result, "updated", "Updated"))));
      const skippedNoBase = Math.max(0, Math.trunc(toNumber(pick(result, "skippedNoBaseSalary", "SkippedNoBaseSalary"))));
      const processed = Math.max(0, Math.trunc(toNumber(pick(result, "processedStaffCount", "ProcessedStaffCount"))));
      const netTotal = toNumber(pick(result, "netTotal", "NetTotal"));
      const calculated = inserted + updated;

      await reloadRuns();
      await reloadItemsAndPayslips();

      if (processed > 0 && calculated <= 0) {
        toast({
          status: "warning",
          title: "Đợt lương đã tạo nhưng chưa có dòng lương.",
          description: `Kỳ ${selectedPeriodCodeForBatch}: ${processed} nhân sự được quét, thiếu lương gốc hiệu lực: ${skippedNoBase}.`,
        });
        return;
      }

      toast({
        status: "success",
        title: "Đã tính lương hàng loạt.",
        description: `Kỳ ${selectedPeriodCodeForBatch}: Nhân sự xử lý: ${processed} · Thêm mới: ${inserted} · Cập nhật: ${updated} · Thiếu lương gốc: ${skippedNoBase} · Tổng thực nhận: ${formatCurrency(netTotal)}`,
      });
    } catch (err) {
      setError(toFriendlyError(err, "Tính lương hàng loạt thất bại."));
    } finally {
      setCalculatingBatchPayroll(false);
    }
  }

  const applySalaryCoefficientFromServer = useCallback(async (staffIdInput, options = {}) => {
    const { showToast = false } = options;
    const staffId = toId(staffIdInput);
    if (!staffId) {
      setSalaryCoefficientInfo(null);
      setItemForm((prev) => ({
        ...prev,
        baseSalary: "",
        dependentCount: "0",
        insuranceZone: "I",
      }));
      return;
    }

    const requestSeq = salaryCoefficientRequestSeqRef.current + 1;
    salaryCoefficientRequestSeqRef.current = requestSeq;

    setApplyingSalaryCoefficient(true);
    if (showToast) setError("");

    try {
      const data = await getStaffSalaryCoefficient(staffId, coefficientAsOfDate);
      if (requestSeq !== salaryCoefficientRequestSeqRef.current) return;

      const baseSalary = toNumber(pick(data, "baseSalary", "BaseSalary"));
      const hasBaseSalary =
        Boolean(pick(data, "hasBaseSalary", "HasBaseSalary")) || baseSalary > 0;
      const coefficient = toNumber(pick(data, "coefficient", "Coefficient")) || 1;
      const calculatedSalary = toNumber(pick(data, "calculatedSalary", "CalculatedSalary"));
      const yearsOfService = Math.max(0, Math.trunc(toNumber(pick(data, "yearsOfService", "YearsOfService"))));
      const appliedSalary =
        calculatedSalary > 0 ? calculatedSalary : Math.round(baseSalary * coefficient);

      setItemForm((prev) => ({
        ...prev,
        baseSalary: hasBaseSalary ? String(Math.round(baseSalary)) : "",
      }));
      setSalaryCoefficientInfo({
        coefficient,
        yearsOfService,
        tenureRange: tenureRangeLabel(yearsOfService),
        baseSalary,
        hasBaseSalary,
        calculatedSalary: hasBaseSalary ? appliedSalary : 0,
        asOfDate: pick(data, "asOfDate", "AsOfDate") || coefficientAsOfDate,
        policyReference: pick(data, "policyReference", "PolicyReference") || "",
      });

      if (!showToast) return;
      if (hasBaseSalary && appliedSalary > 0) {
        toast({
          status: "success",
          title: "Đã áp hệ số lương theo thâm niên.",
          description: `Hệ số ${coefficient.toFixed(4)} · Lương áp dụng ${formatCurrency(appliedSalary)}`,
        });
      } else {
        toast({
          status: "warning",
          title: "Đã tính hệ số thâm niên.",
          description: `Hệ số ${coefficient.toFixed(4)}. Nhân sự này chưa có lương gốc hiệu lực trong kỳ.`,
        });
      }
    } catch (err) {
      if (requestSeq !== salaryCoefficientRequestSeqRef.current) return;
      const statusCode = Number(err?.response?.status || err?.status || 0);
      if (statusCode === 404) {
        try {
          const baseData = await getStaffBaseSalary(staffId, coefficientAsOfDate);
          if (requestSeq !== salaryCoefficientRequestSeqRef.current) return;
          const fallbackBaseSalary = toNumber(pick(baseData, "baseSalary", "BaseSalary"));
          const hasFallbackBaseSalary = fallbackBaseSalary > 0;

          setItemForm((prev) => ({
            ...prev,
            baseSalary: hasFallbackBaseSalary ? String(Math.round(fallbackBaseSalary)) : "",
          }));
          setSalaryCoefficientInfo({
            coefficient: 1,
            yearsOfService: 0,
            tenureRange: tenureRangeLabel(0),
            baseSalary: fallbackBaseSalary,
            hasBaseSalary: hasFallbackBaseSalary,
            calculatedSalary: hasFallbackBaseSalary ? Math.round(fallbackBaseSalary) : 0,
            asOfDate: coefficientAsOfDate,
            policyReference: "Fallback to base salary because salary-coefficient API returned 404.",
          });

          if (showToast) {
            toast({
              status: "warning",
              title: "Không tìm thấy API hệ số lương.",
              description: "Đã tạm dùng hệ số 1.0000 theo lương cơ bản.",
            });
          }
          return;
        } catch {
          // Ignore and continue to common error handling below.
        }
      }

      setSalaryCoefficientInfo(null);
      setItemForm((prev) => ({ ...prev, baseSalary: "" }));

      if (showToast) {
        const fallbackMessage = "Không áp được hệ số lương.";
        setError(toFriendlyError(err, fallbackMessage));
      }
    } finally {
      if (requestSeq === salaryCoefficientRequestSeqRef.current) {
        setApplyingSalaryCoefficient(false);
      }
    }
  }, [coefficientAsOfDate, toast]);

  async function handleApplySalaryCoefficient() {
    const staffId = toId(itemForm.staffId);
    if (!staffId) {
      setError("Vui lòng chọn nhân sự trước khi áp hệ số lương.");
      return;
    }
    await applySalaryCoefficientFromServer(staffId, { showToast: true });
  }

  async function handleSubmitInitialSalaryRequest() {
    const staffId = toId(itemForm.staffId);
    if (!staffId) {
      setError("Vui lòng chọn nhân sự trước khi gửi yêu cầu lương.");
      return;
    }

    const proposedBaseSalary = toNumber(initialSalaryRequest.baseSalary);
    if (proposedBaseSalary <= 0) {
      setError("Vui lòng nhập mức lương đề xuất lớn hơn 0.");
      return;
    }

    setSubmittingInitialSalaryRequest(true);
    setError("");
    try {
      const reasonText = initialSalaryRequest.reason.trim();
      const response = await createInitialSalaryRequest(staffId, {
        baseSalary: proposedBaseSalary,
        effectiveFrom: coefficientAsOfDate,
        reason: reasonText || `Đề xuất tạo lương khởi điểm cho nhân sự #${staffId}.`,
        note: reasonText
      });
      toast({
        status: "success",
        title: "Đã gửi yêu cầu duyệt lương lên admin.",
        description: `Mã yêu cầu #${pick(response, "requestId", "RequestId") || "--"}`
      });
      setInitialSalaryRequest((prev) => ({ ...prev, reason: "" }));
    } catch (err) {
      setError(toFriendlyError(err, "Không gửi được yêu cầu duyệt lương."));
    } finally {
      setSubmittingInitialSalaryRequest(false);
    }
  }

  useEffect(() => {
    const staffId = toId(itemForm.staffId);
    if (!staffId) {
      salaryCoefficientRequestSeqRef.current += 1;
      setApplyingSalaryCoefficient(false);
      return;
    }
    applySalaryCoefficientFromServer(staffId);
  }, [applySalaryCoefficientFromServer, itemForm.staffId, coefficientAsOfDate]);

  async function handleApprove(runId) {
    const userId = getCurrentUserId();
    if (!userId) {
      setError("Không xác định được người dùng hiện tại để duyệt đợt tính lương.");
      return;
    }
    const run = runById.get(runId);
    if (!run) {
      setError("Không tìm thấy đợt tính lương.");
      return;
    }
    if (run.status !== "DRAFT") {
      setError("Chỉ đợt tính lương ở trạng thái Nháp mới được duyệt.");
      return;
    }
    const hasItems = itemRows.some((item) => item.payrollRunId === runId);
    if (!hasItems) {
      setError("Không thể duyệt đợt chưa có dòng lương.");
      return;
    }
    setBusyRunId(runId);
    setError("");
    try {
      await approvePayrollRun(runId, userId);
      await reloadRuns();
      toast({ status: "success", title: `Đã duyệt đợt tính lương ${run.runNo}.` });
    } catch (err) {
      setError(toFriendlyError(err, "Duyệt đợt tính lương thất bại."));
    } finally {
      setBusyRunId(null);
    }
  }

  async function handleMarkPaid(runId) {
    const run = runById.get(runId);
    if (!run) {
      setError("Không tìm thấy đợt tính lương.");
      return;
    }
    if (run.status !== "APPROVED") {
      setError("Đợt tính lương phải ở trạng thái Đã duyệt trước khi xác nhận đã chi.");
      return;
    }
    const runItems = itemRows.filter((item) => item.payrollRunId === runId);
    if (!runItems.length) {
      setError("Đợt tính lương chưa có dòng lương.");
      return;
    }
    const allHavePayslip = runItems.every((item) => item.hasPayslip);
    if (!allHavePayslip) {
      setError("Vui lòng phát hành phiếu lương cho toàn bộ dòng lương trước khi xác nhận đã chi.");
      return;
    }

    setBusyRunId(runId);
    setError("");
    try {
      await markPayrollRunPaid(runId, getCurrentUserId() || 1);
      await reloadRuns();
      toast({ status: "success", title: `Đã xác nhận chi xong đợt ${run.runNo}.` });
    } catch (err) {
      setError(toFriendlyError(err, "Xác nhận đã chi thất bại."));
    } finally {
      setBusyRunId(null);
    }
  }

  async function handleIssuePayslip(itemId) {
    const userId = getCurrentUserId();
    if (!userId) {
      setError("Không xác định được người dùng hiện tại để phát hành phiếu lương.");
      return;
    }
    const row = itemRows.find((item) => item.id === itemId);
    if (!row) {
      setError("Không tìm thấy dòng lương.");
      return;
    }
    if (!row.canIssuePayslip) {
      setError("Chỉ được phát hành phiếu lương khi đợt tính lương đã duyệt hoặc đã chi.");
      return;
    }

    setBusyItemId(itemId);
    setError("");
    try {
      const codeSuffix = String(Date.now()).slice(-6);
      await createPayslip({
        payrollItemId: itemId,
        slipNo: `PS-${itemId}-${codeSuffix}`,
        issuedAt: new Date().toISOString(),
        issuedBy: userId,
        fileUrl: "",
      });
      await reloadItemsAndPayslips();
      toast({ status: "success", title: `Đã phát hành phiếu lương cho dòng #${itemId}.` });
    } catch (err) {
      setError(toFriendlyError(err, "Phát hành phiếu lương thất bại."));
    } finally {
      setBusyItemId(null);
    }
  }

  async function handleIssueAllPayslips() {
    const userId = getCurrentUserId();
    if (!userId) {
      setError("Không xác định được người dùng hiện tại để phát hành phiếu lương.");
      return;
    }

    const targets = filteredItems.filter((item) => item.canIssuePayslip && !item.hasPayslip);
    if (!targets.length) {
      setError("Không có dòng lương hợp lệ để phát hành phiếu lương hàng loạt.");
      return;
    }

    setIssuingAllPayslips(true);
    setError("");
    let successCount = 0;
    let failedCount = 0;
    let firstError = null;
    const batchKey = Date.now();

    for (let index = 0; index < targets.length; index += 1) {
      const item = targets[index];
      try {
        await createPayslip({
          payrollItemId: item.id,
          slipNo: `PS-${item.id}-${batchKey}-${index + 1}`,
          issuedAt: new Date().toISOString(),
          issuedBy: userId,
          fileUrl: "",
        });
        successCount += 1;
      } catch (err) {
        failedCount += 1;
        if (!firstError) firstError = err;
      }
    }

    await reloadItemsAndPayslips();

    if (successCount > 0 && failedCount === 0) {
      toast({
        status: "success",
        title: "Đã phát hành toàn bộ phiếu lương.",
        description: `Số phiếu đã phát hành: ${successCount}.`,
      });
    } else if (successCount > 0 && failedCount > 0) {
      toast({
        status: "warning",
        title: "Đã phát hành phiếu lương một phần.",
        description: `Thành công: ${successCount} · Thất bại: ${failedCount}.`,
      });
      setError(firstError ? toFriendlyError(firstError, "Một số phiếu lương phát hành thất bại.") : "Một số phiếu lương phát hành thất bại.");
    } else {
      setError(firstError ? toFriendlyError(firstError, "Phát hành toàn bộ phiếu lương thất bại.") : "Phát hành toàn bộ phiếu lương thất bại.");
    }

    setIssuingAllPayslips(false);
  }

  function handleClosePayrollDetail() {
    payrollDetailRequestSeqRef.current += 1;
    setSelectedPayrollItem(null);
    setSelectedPayrollSalaryInfo(null);
    setPayrollDetailError("");
    setLoadingPayrollDetail(false);
  }

  async function handleOpenPayrollDetail(item) {
    setSelectedPayrollItem(item);
    setSelectedPayrollSalaryInfo(null);
    setPayrollDetailError("");

    const staffId = toId(item?.staffId);
    if (!staffId) return;

    const requestSeq = payrollDetailRequestSeqRef.current + 1;
    payrollDetailRequestSeqRef.current = requestSeq;
    setLoadingPayrollDetail(true);

    try {
      const info = await getStaffSalaryCoefficient(staffId, item.policyAsOfDate);
      if (requestSeq !== payrollDetailRequestSeqRef.current) return;

      const baseSalary = toNumber(pick(info, "baseSalary", "BaseSalary"));
      const hasBaseSalary = Boolean(pick(info, "hasBaseSalary", "HasBaseSalary")) || baseSalary > 0;
      const coefficient = toNumber(pick(info, "coefficient", "Coefficient")) || 1;
      const yearsOfService = Math.max(0, Math.trunc(toNumber(pick(info, "yearsOfService", "YearsOfService"))));

      setSelectedPayrollSalaryInfo({
        baseSalary,
        hasBaseSalary,
        coefficient,
        yearsOfService,
      });
    } catch (err) {
      if (requestSeq !== payrollDetailRequestSeqRef.current) return;

      try {
        const baseInfo = await getStaffBaseSalary(staffId, item.policyAsOfDate);
        if (requestSeq !== payrollDetailRequestSeqRef.current) return;

        const baseSalary = toNumber(pick(baseInfo, "baseSalary", "BaseSalary"));
        setSelectedPayrollSalaryInfo({
          baseSalary,
          hasBaseSalary: baseSalary > 0,
          coefficient: 1,
          yearsOfService: 0,
        });
        setPayrollDetailError("Không lấy được hệ số lương từ server, đang hiển thị hệ số mặc định 1.0000.");
      } catch {
        setPayrollDetailError(toFriendlyError(err, "Không tải được thông tin hệ số lương cho nhân sự này."));
      }
    } finally {
      if (requestSeq === payrollDetailRequestSeqRef.current) {
        setLoadingPayrollDetail(false);
      }
    }
  }

  if (loading) {
    return (
      <Flex minH='260px' align='center' justify='center' direction='column' gap='10px'>
        <Spinner size='lg' color='brand.500' />
        <Text color={textColorSecondary}>Đang tải dữ liệu tiền lương...</Text>
      </Flex>
    );
  }

  return (
    <Box>
      {error ? (
        <Card p='16px' mb='20px'>
          <Text color='red.400' fontWeight='600'>{error}</Text>
        </Card>
      ) : null}

      <SimpleGrid columns={{ base: 1, md: 2, xl: 5 }} gap='20px' mb='20px'>
        <MiniStatistics
          name='Kỳ đang mở'
          value={String(pick(openPeriod, "periodCode", "PeriodCode") || "Chua có")}
        />
        <MiniStatistics name='Kỳ tính lương' value={String(runRows.length)} />
        <MiniStatistics name='Dòng lương' value={String(itemRows.length)} />
        <MiniStatistics name='Tổng lương thực nhận' value={formatCurrency(payrollTotal)} />
        <MiniStatistics name='Lương / Doanh thu' value={`${payrollRevenueRatio.toFixed(2)}%`} />
      </SimpleGrid>

      <Card p='16px' mb='20px'>
        <Flex wrap='wrap' gap='10px' align='center'>
          <Badge colorScheme={periods.available ? "green" : "orange"}>API Kỳ lương: {periods.available ? "OK" : "Chua có"}</Badge>
          <Badge colorScheme={runs.available ? "green" : "orange"}>API Đợt lương: {runs.available ? "OK" : "Chua có"}</Badge>
          <Badge colorScheme={items.available ? "green" : "orange"}>API Dòng lương: {items.available ? "OK" : "Chua có"}</Badge>
          <Badge colorScheme={payslips.available ? "green" : "orange"}>API Phiếu lương: {payslips.available ? "OK" : "Chua có"}</Badge>
          <Badge colorScheme='blue'>Nhân sự: {staffs.length}</Badge>
        </Flex>
      </Card>

      <SimpleGrid columns={{ base: 1, xl: 2 }} gap='20px' mb='20px'>
        <Card p='16px'>
          <Text color={textColor} fontWeight='700' mb='10px'>Mở kỳ lương</Text>
          <SimpleGrid columns={1} gap='10px'>
            <FormControl>
              <FormLabel fontSize='xs' mb='4px'>Mã kỳ lương</FormLabel>
              <Input
                value={periodForm.periodCode}
                onChange={(e) => setPeriodForm((prev) => ({ ...prev, periodCode: e.target.value }))}
                placeholder='VD: 2026-03'
              />
            </FormControl>
            <SimpleGrid columns={2} gap='10px'>
              <FormControl>
                <FormLabel fontSize='xs' mb='4px'>Từ ngày</FormLabel>
                <Input
                  type='date'
                  value={periodForm.startDate}
                  onChange={(e) => setPeriodForm((prev) => ({ ...prev, startDate: e.target.value }))}
                />
              </FormControl>
              <FormControl>
                <FormLabel fontSize='xs' mb='4px'>Đến ngày</FormLabel>
                <Input
                  type='date'
                  value={periodForm.endDate}
                  onChange={(e) => updatePeriodEndDate(e.target.value)}
                />
              </FormControl>
            </SimpleGrid>
            <SimpleGrid columns={2} gap='10px'>
              <FormControl>
                <FormLabel fontSize='xs' mb='4px'>Ngày trả lương</FormLabel>
                <Input
                  type='date'
                  value={periodForm.payrollDate}
                  onChange={(e) => setPeriodForm((prev) => ({ ...prev, payrollDate: e.target.value }))}
                />
              </FormControl>
              <FormControl>
                <FormLabel fontSize='xs' mb='4px'>Trạng thái</FormLabel>
                <Select
                  value={periodForm.status}
                  onChange={(e) => setPeriodForm((prev) => ({ ...prev, status: e.target.value }))}>
                  <option value='OPEN'>Đang mở</option>
                </Select>
              </FormControl>
            </SimpleGrid>
            <Text color={textColorSecondary} fontSize='xs'>
              Luu ý: Ngày trả lương phải bằng hoặc sau ngày kết thúc kỳ.
            </Text>
            <Button size='sm' colorScheme='blue' onClick={handleCreatePeriod} isLoading={savingPeriod}>
              Tạo kỳ lương
            </Button>
          </SimpleGrid>
        </Card>

        <Card p='16px'>
          <Text color={textColor} fontWeight='700' mb='10px'>Tính lương toàn bộ nhân sự</Text>
          <SimpleGrid columns={1} gap='10px'>
            <FormControl>
              <FormLabel fontSize='xs' mb='4px'>Kỳ lương (tự động)</FormLabel>
              <Input value={autoRunLabel} isReadOnly />
            </FormControl>
            <Text color={textColorSecondary} fontSize='sm'>
              Hệ thống sẽ tự lấy dữ liệu lương và tính cho toàn bộ nhân sự trong kỳ đang mở.
            </Text>
            {!selectedPeriodIdForBatch ? (
              <Text color='orange.400' fontSize='xs'>
                Cần có kỳ lương đang mở trước khi tính lương hàng loạt.
              </Text>
            ) : null}
            {selectedPeriodIdForBatch && selectedPeriodStatusForBatch !== "OPEN" ? (
              <Text color='orange.400' fontSize='xs'>
                Kỳ đã chọn chưa ở trạng thái ĐANG MỞ.
              </Text>
            ) : null}
            <Button
              size='sm'
              colorScheme='teal'
              variant='outline'
              onClick={handleCalculateAllStaffPayroll}
              isLoading={calculatingBatchPayroll}
              isDisabled={!selectedPeriodIdForBatch || selectedPeriodStatusForBatch !== "OPEN"}>
              Tính lương toàn bộ nhân sự
            </Button>
          </SimpleGrid>
        </Card>
      </SimpleGrid>

      <SimpleGrid columns={{ base: 1, xl: 2 }} gap='20px' mb='20px'>
        <Card p='20px'>
          <Text color={textColor} fontWeight='700' mb='10px'>Danh sách kỳ lương</Text>
          {periodRows.length ? (
            <Table size='sm'>
              <Thead>
                <Tr>
                  <Th>Mã kỳ</Th>
                  <Th>Từ ngày</Th>
                  <Th>Đến ngày</Th>
                  <Th>Ngày trả lương</Th>
                  <Th>Trạng thái</Th>
                </Tr>
              </Thead>
              <Tbody>
                {periodRows.slice(0, 8).map((period, idx) => {
                  const status = String(period.status || "").toUpperCase();
                  return (
                    <Tr key={period.id || idx}>
                      <Td color={textColor} fontWeight='600'>
                        {period.periodCode || `PERIOD-${idx + 1}`}
                      </Td>
                      <Td color={textColorSecondary}>{formatDate(period.startDate)}</Td>
                      <Td color={textColorSecondary}>{formatDate(period.endDate)}</Td>
                      <Td color={textColorSecondary}>{formatDate(period.payrollDate)}</Td>
                      <Td>
                        <Badge colorScheme={statusColor(status)}>{statusLabel(status)}</Badge>
                      </Td>
                    </Tr>
                  );
                })}
              </Tbody>
            </Table>
          ) : (
            <Text color={textColorSecondary}>Chưa có dữ liệu kỳ lương.</Text>
          )}
        </Card>

        <Card p='20px'>
          <Flex justify='space-between' align='center' mb='10px' wrap='wrap' gap='8px'>
            <Text color={textColor} fontWeight='700'>Danh sách kỳ lương</Text>
            <Flex gap='6px'>
              <Badge colorScheme='orange'>Nháp: {runSummary.draft}</Badge>
              <Badge colorScheme='blue'>Đã duyệt: {runSummary.approved}</Badge>
              <Badge colorScheme='green'>Đã chi: {runSummary.paid}</Badge>
            </Flex>
          </Flex>
          {runRows.length ? (
            <Table size='sm'>
              <Thead>
                <Tr>
                  <Th>Mã kỳ</Th>
                  <Th>Chi nhánh</Th>
                  <Th>Ngày tạo</Th>
                  <Th>Status</Th>
                  <Th textAlign='right'>Thao tác</Th>
                </Tr>
              </Thead>
              <Tbody>
                {runRows.slice(0, 10).map((run) => (
                  <Tr key={run.id}>
                    <Td color={textColor} fontWeight='600'>{run.periodCode}</Td>
                    <Td color={textColorSecondary}>{run.branchName}</Td>
                    <Td color={textColorSecondary}>{formatDateTime(run.createdAt)}</Td>
                    <Td>
                      <Badge colorScheme={statusColor(run.status)}>
                        {statusLabel(run.status)}
                      </Badge>
                    </Td>
                    <Td textAlign='right'>
                      <Flex justify='flex-end' gap='8px'>
                        <Button
                          size='xs'
                          colorScheme='blue'
                          variant='outline'
                          isDisabled={run.status === "APPROVED" || run.status === "PAID"}
                          isLoading={busyRunId === run.id}
                          onClick={() => handleApprove(run.id)}>
                          Duyệt
                        </Button>
                        <Button
                          size='xs'
                          colorScheme='green'
                          isDisabled={run.status !== "APPROVED"}
                          isLoading={busyRunId === run.id}
                          onClick={() => handleMarkPaid(run.id)}>
                          Xác nhận đã chi
                        </Button>
                      </Flex>
                    </Td>
                  </Tr>
                ))}
              </Tbody>
            </Table>
          ) : (
            <Text color={textColorSecondary}>Chưa có kỳ lương.</Text>
          )}
        </Card>
      </SimpleGrid>

      <Card p='20px' mb='20px'>
        <Flex wrap='wrap' gap='10px' mb='10px'>
          <Select value={runFilter} onChange={(e) => setRunFilter(e.target.value)} maxW='220px'>
            <option value='ALL'>Tất cả kỳ lương</option>
            {periodFilterOptions.map((periodCode) => (
              <option value={periodCode} key={periodCode}>{periodCode}</option>
            ))}
          </Select>
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder='Tìm theo nhân sự / kỳ / trạng thái'
            maxW='320px'
          />
          <Button
            size='sm'
            colorScheme='purple'
            variant='outline'
            onClick={handleIssueAllPayslips}
            isLoading={issuingAllPayslips}
            isDisabled={issuingAllPayslips || issuablePayslipCount <= 0}>
            Phát hành toàn bộ phiếu lương ({issuablePayslipCount})
          </Button>
        </Flex>

        {filteredItems.length ? (
          <Table size='sm'>
            <Thead>
              <Tr>
                <Th>Nhân sự</Th>
                <Th>Mã kỳ</Th>
                <Th isNumeric>Tổng thu nhập</Th>
                <Th isNumeric>Tổng khấu trừ</Th>
                <Th isNumeric>Thực nhận</Th>
                <Th>Status</Th>
                <Th>Phiếu lương</Th>
                <Th textAlign='right'>Thao tác</Th>
              </Tr>
            </Thead>
            <Tbody>
              {filteredItems.slice(0, 20).map((item) => (
                <Tr key={item.id}>
                  <Td color={textColor} fontWeight='600'>
                    <Button
                      variant='link'
                      colorScheme='blue'
                      fontSize='sm'
                      fontWeight='700'
                      onClick={() => handleOpenPayrollDetail(item)}>
                      {item.staffName}
                    </Button>
                  </Td>
                  <Td color={textColorSecondary}>{item.periodCode || "--"}</Td>
                  <Td color={textColor} isNumeric>{formatCurrency(item.grossPay)}</Td>
                  <Td color={textColor} isNumeric>{formatCurrency(item.totalDeduction)}</Td>
                  <Td color={textColor} isNumeric>{formatCurrency(item.netPay)}</Td>
                  <Td>
                    <Badge colorScheme={statusColor(item.status)}>
                      {statusLabel(item.status)}
                    </Badge>
                  </Td>
                  <Td>
                    <Badge colorScheme={item.hasPayslip ? "green" : "gray"}>
                      {item.hasPayslip ? "Đã phát hành" : "Chưa phát hành"}
                    </Badge>
                    {!item.canIssuePayslip ? (
                      <Text color={textColorSecondary} fontSize='xs'>
                        Chờ duyệt đợt
                      </Text>
                    ) : null}
                  </Td>
                  <Td textAlign='right'>
                    <Button
                      size='xs'
                      colorScheme='purple'
                      variant='outline'
                      isDisabled={item.hasPayslip || !item.canIssuePayslip}
                      isLoading={busyItemId === item.id}
                      onClick={() => handleIssuePayslip(item.id)}>
                      Phát hành phiếu lương
                    </Button>
                  </Td>
                </Tr>
              ))}
            </Tbody>
          </Table>
        ) : (
          <Text color={textColorSecondary}>Chưa có dòng lương phù hợp bộ lọc.</Text>
        )}
      </Card>

      <Modal
        isOpen={Boolean(selectedPayrollItem)}
        onClose={handleClosePayrollDetail}
        size='3xl'
        isCentered
        scrollBehavior='inside'>
        <ModalOverlay />
        <ModalContent>
          <ModalHeader>
            Chi tiết lương: {selectedPayrollItem?.staffName || "--"}
          </ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            <SimpleGrid columns={{ base: 1, md: 3 }} gap='10px' mb='12px'>
              <Box borderWidth='1px' borderRadius='10px' p='10px'>
                <Text color={textColorSecondary} fontSize='xs'>Kỳ lương</Text>
                <Text color={textColor} fontWeight='700'>
                  {selectedPayrollItem?.periodCode || "--"}
                </Text>
              </Box>
              <Box borderWidth='1px' borderRadius='10px' p='10px'>
                <Text color={textColorSecondary} fontSize='xs'>Đợt lương</Text>
                <Text color={textColor} fontWeight='700'>
                  {selectedPayrollItem?.runNo || "--"}
                </Text>
              </Box>
              <Box borderWidth='1px' borderRadius='10px' p='10px'>
                <Text color={textColorSecondary} fontSize='xs'>Mốc tính</Text>
                <Text color={textColor} fontWeight='700'>
                  {formatDate(selectedPayrollBreakdown?.policyAsOfDate)}
                </Text>
              </Box>
            </SimpleGrid>

            {loadingPayrollDetail ? (
              <Flex align='center' gap='8px' mb='10px'>
                <Spinner size='sm' />
                <Text color={textColorSecondary} fontSize='sm'>Đang tải hệ số lương...</Text>
              </Flex>
            ) : null}

            {payrollDetailError ? (
              <Text color='orange.400' fontSize='sm' mb='10px'>
                {payrollDetailError}
              </Text>
            ) : null}

            {selectedPayrollBreakdown ? (
              <>
                <SimpleGrid columns={{ base: 1, md: 2 }} gap='10px' mb='12px'>
                  <Box borderWidth='1px' borderRadius='10px' p='12px'>
                    <Text color={textColorSecondary} fontSize='xs'>Lương cơ bản (gốc)</Text>
                    <Text color={textColor} fontWeight='700'>
                      {selectedPayrollBreakdown.hasOriginalBaseSalary
                        ? formatCurrency(selectedPayrollBreakdown.originalBaseSalary)
                        : "Chưa thiết lập"}
                    </Text>
                  </Box>
                  <Box borderWidth='1px' borderRadius='10px' p='12px'>
                    <Text color={textColorSecondary} fontSize='xs'>Hệ số lương</Text>
                    <Text color={textColor} fontWeight='700'>
                      {Number(selectedPayrollBreakdown.coefficient || 1).toFixed(4)}
                    </Text>
                    <Text color={textColorSecondary} fontSize='xs'>
                      Thâm niên: {selectedPayrollBreakdown.yearsOfService} năm ({selectedPayrollBreakdown.tenureRange})
                    </Text>
                  </Box>
                  <Box borderWidth='1px' borderRadius='10px' p='12px'>
                    <Text color={textColorSecondary} fontSize='xs'>Lương áp dụng sau hệ số</Text>
                    <Text color={textColor} fontWeight='700'>
                      {formatCurrency(selectedPayrollItem.baseSalary)}
                    </Text>
                  </Box>
                  <Box borderWidth='1px' borderRadius='10px' p='12px'>
                    <Text color={textColorSecondary} fontSize='xs'>Phụ cấp</Text>
                    <Text color={textColor} fontWeight='700'>
                      {formatCurrency(selectedPayrollItem.allowanceAmount)}
                    </Text>
                  </Box>
                </SimpleGrid>

                <SimpleGrid columns={{ base: 1, md: 2 }} gap='10px' mb='12px'>
                  <Box borderWidth='1px' borderRadius='10px' p='12px'>
                    <Text color={textColorSecondary} fontSize='xs'>BHXH NLĐ (8%)</Text>
                    <Text color={textColor} fontWeight='700'>{formatCurrency(selectedPayrollBreakdown.employeeBhxh)}</Text>
                    <Text color={textColorSecondary} fontSize='xs' mt='6px'>BHYT NLĐ (1.5%)</Text>
                    <Text color={textColor} fontWeight='700'>{formatCurrency(selectedPayrollBreakdown.employeeBhyt)}</Text>
                    <Text color={textColorSecondary} fontSize='xs' mt='6px'>BHTN NLĐ (1%)</Text>
                    <Text color={textColor} fontWeight='700'>{formatCurrency(selectedPayrollBreakdown.employeeBhtn)}</Text>
                    <Divider my='8px' />
                    <Text color={textColorSecondary} fontSize='xs'>Tổng BH NLĐ</Text>
                    <Text color={textColor} fontWeight='700'>{formatCurrency(selectedPayrollBreakdown.employeeInsuranceTotal)}</Text>
                  </Box>

                  <Box borderWidth='1px' borderRadius='10px' p='12px'>
                    <Text color={textColorSecondary} fontSize='xs'>Thuế TNCN (PIT)</Text>
                    <Text color={textColor} fontWeight='700'>{formatCurrency(selectedPayrollBreakdown.pitAmount)}</Text>
                    <Divider my='8px' />
                    <Text color={textColorSecondary} fontSize='xs'>Khấu trừ công thiếu</Text>
                    <Text color={textColor} fontWeight='700'>{formatCurrency(selectedPayrollItem.attendanceDeduction)}</Text>
                    <Text color={textColorSecondary} fontSize='xs' mt='6px'>Tạm ứng + Khoản vay + Khác</Text>
                    <Text color={textColor} fontWeight='700'>
                      {formatCurrency(
                        selectedPayrollItem.advanceDeduction +
                        selectedPayrollItem.loanDeduction +
                        selectedPayrollItem.otherDeduction +
                        selectedPayrollItem.penaltyAmount,
                      )}
                    </Text>
                  </Box>
                </SimpleGrid>

                <Box borderWidth='1px' borderColor='green.300' bg='green.50' borderRadius='12px' p='12px'>
                  <SimpleGrid columns={{ base: 1, md: 3 }} gap='10px'>
                    <Box>
                      <Text color='green.700' fontSize='xs'>Tổng thu nhập</Text>
                      <Text color='green.800' fontWeight='800' fontSize='lg'>
                        {formatCurrency(selectedPayrollBreakdown.totalIncome)}
                      </Text>
                    </Box>
                    <Box>
                      <Text color='green.700' fontSize='xs'>Tổng khấu trừ</Text>
                      <Text color='green.800' fontWeight='800' fontSize='lg'>
                        {formatCurrency(selectedPayrollBreakdown.totalDeduction)}
                      </Text>
                    </Box>
                    <Box>
                      <Text color='green.700' fontSize='xs'>Tổng tiền nhận được</Text>
                      <Text color='green.800' fontWeight='800' fontSize='lg'>
                        {formatCurrency(selectedPayrollBreakdown.netPay)}
                      </Text>
                    </Box>
                  </SimpleGrid>
                </Box>
              </>
            ) : (
              <Text color={textColorSecondary}>Chưa có dữ liệu chi tiết để hiển thị.</Text>
            )}
          </ModalBody>
          <ModalFooter>
            <Button onClick={handleClosePayrollDetail}>Đóng</Button>
          </ModalFooter>
        </ModalContent>
      </Modal>

    </Box>
  );
}



