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
  Textarea,
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
import { getInitialSalaryRequests } from "../../../../../features/admin/api/salaryAdminApi";
import { useBranch } from "../../../../../context/BranchContext";
import {
  approvePayrollRun,
  calculatePayrollForAllStaff,
  createInitialSalaryRequest,
  createPayrollPeriod,
  createPayslip,
  getBranches,
  getPayrollItems,
  getPayrollPeriods,
  getPayrollRuns,
  getPayslips,
  getStaffSalaryCoefficient,
  markPayrollRunPaid,
  cancelPayrollRun,
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

function derivePayrollAmounts(item) {
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

function statusColor(status) {
  const code = String(status || "").toUpperCase();
  if (code === "PAID") return "green";
  if (code === "APPROVED") return "blue";
  if (code === "CANCELED") return "red";
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
  if (code === "CANCELED") return "Đã huỷ";
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

function normalizePeriodCode(value) {
  return String(value || "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/\s+/g, "")
    .trim()
    .toUpperCase();
}

const TENURE_BRACKETS = [
  { minYears: 0, label: "Dưới 5 năm" },
  { minYears: 5, label: "5 đến dưới 10 năm" },
  { minYears: 10, label: "10 đến dưới 15 năm" },
  { minYears: 15, label: "15 đến dưới 20 năm" },
  { minYears: 20, label: "20 đến dưới 25 năm" },
  { minYears: 25, label: "Từ 25 năm trở lên" },
];

const PAYROLL_EXCLUDED_STAFF_STATUSES = new Set(["TERMINATED", "SUSPENDED", "INACTIVE"]);

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
  const [staffs, setStaffs] = useState([]);
  const [branches, setBranches] = useState([]);

  const [search, setSearch] = useState("");
  const [runFilter, setRunFilter] = useState("ALL");
  const [busyRunId, setBusyRunId] = useState(null);
  const [busyItemId, setBusyItemId] = useState(null);
  const [issuingAllPayslips, setIssuingAllPayslips] = useState(false);
  const [savingPeriod, setSavingPeriod] = useState(false);
  const [calculatingBatchPayroll, setCalculatingBatchPayroll] = useState(false);
  const [selectedPayrollItem, setSelectedPayrollItem] = useState(null);
  const [selectedPayrollSalaryInfo, setSelectedPayrollSalaryInfo] = useState(null);
  const [loadingPayrollDetail, setLoadingPayrollDetail] = useState(false);
  const [payrollDetailError, setPayrollDetailError] = useState("");
  const [loadingMissingBaseSalary, setLoadingMissingBaseSalary] = useState(false);
  const [missingBaseSalaryRows, setMissingBaseSalaryRows] = useState([]);
  const [initialSalaryTarget, setInitialSalaryTarget] = useState(null);
  const [initialSalaryForm, setInitialSalaryForm] = useState({
    baseSalary: "",
    reason: "",
    note: "",
  });
  const [sendingInitialSalaryRequest, setSendingInitialSalaryRequest] = useState(false);
  const [initialSalaryDecisionRows, setInitialSalaryDecisionRows] = useState([]);
  const payrollDetailRequestSeqRef = useRef(0);
  const missingBaseSalaryRequestSeqRef = useRef(0);
  const initialSalaryRequestStatusRef = useRef(new Map());
  const hasSeededInitialSalaryRequestStatusRef = useRef(false);

  const [periodForm, setPeriodForm] = useState(() => createDefaultPeriodForm());

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
    const [periodRes, runRes, itemRes, payslipRes, staffRes, branchRes] = await Promise.allSettled([
      getPayrollPeriods(),
      getPayrollRuns(),
      getPayrollItems(),
      getPayslips(),
      getAllStaff(),
      getBranches(),
    ]);

    setPeriods(periodRes.status === "fulfilled" ? periodRes.value : { available: false, items: [] });
    setRuns(runRes.status === "fulfilled" ? runRes.value : { available: false, items: [] });
    setItems(itemRes.status === "fulfilled" ? itemRes.value : { available: false, items: [] });
    setPayslips(payslipRes.status === "fulfilled" ? payslipRes.value : { available: false, items: [] });

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
      const id = toId(pick(run, "id", "Id", "payrollRunId", "PayrollRunId"));
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

  const periodIdsByBranchRuns = useMemo(() => {
    const set = new Set();
    runRows.forEach((run) => {
      const periodId = toId(run.payrollPeriodId);
      if (periodId) set.add(periodId);
    });
    return set;
  }, [runRows]);

  const allPeriodRows = useMemo(() => {
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

  const latestOpenPeriodId = useMemo(() => {
    const latestOpen = allPeriodRows.find((period) => String(period.status || "").toUpperCase() === "OPEN");
    return latestOpen ? toId(latestOpen.id) : null;
  }, [allPeriodRows]);

  const periodRows = useMemo(() => {
    if (!targetBranchId) return allPeriodRows;
    return allPeriodRows.filter((period) => {
      const periodId = toId(period.id);
      if (!periodId) return false;
      return periodIdsByBranchRuns.has(periodId) || periodId === latestOpenPeriodId;
    });
  }, [allPeriodRows, latestOpenPeriodId, periodIdsByBranchRuns, targetBranchId]);

  const runById = useMemo(() => {
    const map = new Map();
    runRows.forEach((run) => {
      if (!run.id) return;
      map.set(run.id, run);
    });
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
      const id = toId(pick(item, "id", "Id", "payrollItemId", "PayrollItemId", "payroll_item_id"));
      const payrollRunId = toId(pick(item, "payrollRunId", "PayrollRunId"));
      const staffId = toId(pick(item, "staffId", "StaffId"));
      const runRow = payrollRunId ? runById.get(payrollRunId) || null : null;
      const payrollPeriodId = runRow?.payrollPeriodId || null;
      const periodRow = payrollPeriodId ? periodById.get(payrollPeriodId) || null : null;
      const {
        grossPay,
        totalDeduction,
        netPay,
        hasOfficialNetPay,
        amountsSource,
      } = derivePayrollAmounts(item);
      const runStatus = runRow?.status || "DRAFT";
      const policyAsOfDate = toDateInputValue(
        periodRow?.endDate ||
        periodRow?.payrollDate ||
        new Date().toISOString(),
      );
      const attendanceDeduction = toNumber(pick(item, "attendanceDeduction", "AttendanceDeduction"));
      const penaltyAmount = toNumber(pick(item, "penaltyAmount", "PenaltyAmount"));
      const advanceDeduction = toNumber(pick(item, "advanceDeduction", "AdvanceDeduction"));
      const loanDeduction = toNumber(pick(item, "loanDeduction", "LoanDeduction"));
      const insuranceEmployeeAmount = toNumber(pick(item, "insuranceEmployeeAmount", "InsuranceEmployeeAmount"));
      const insuranceCompanyAmount = toNumber(pick(item, "insuranceCompanyAmount", "InsuranceCompanyAmount"));
      const pitAmount = toNumber(pick(item, "pitAmount", "PitAmount"));
      const otherDeduction = toNumber(pick(item, "otherDeduction", "OtherDeduction"));
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
        canIssuePayslip: Boolean(id) && (runStatus === "APPROVED" || runStatus === "PAID"),
        baseSalary: toNumber(pick(item, "baseSalary", "BaseSalary")),
        overtimeAmount: toNumber(pick(item, "overtimeAmount", "OvertimeAmount")),
        allowanceAmount: toNumber(pick(item, "allowanceAmount", "AllowanceAmount")),
        attendanceDeduction,
        penaltyAmount,
        advanceDeduction,
        loanDeduction,
        insuranceEmployeeAmount,
        insuranceCompanyAmount,
        pitAmount,
        otherDeduction,
        grossPay,
        totalDeduction,
        netPay,
        amountsSource,
        hasOfficialNetPay,
        hasPayslip: id ? payslipItemIds.has(id) : false,
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

  const officialPayrollTotal = useMemo(
    () => sumBy(itemRows.filter((item) => item.hasOfficialNetPay), (item) => item.netPay),
    [itemRows],
  );
  const missingPayrollFactsCount = useMemo(
    () => itemRows.filter((item) => item.amountsSource === "INCOMPLETE").length,
    [itemRows],
  );

  const openPeriod = useMemo(
    () => allPeriodRows.find((period) => String(period.status || "").toUpperCase() === "OPEN"),
    [allPeriodRows],
  );
  const selectedPeriodForBatch = useMemo(() => {
    if (runFilter === "ALL") return openPeriod || null;
    const normalizedFilter = normalizePeriodCode(runFilter);
    const matched = allPeriodRows.find((period) => normalizePeriodCode(period.periodCode) === normalizedFilter);
    return matched || openPeriod || null;
  }, [allPeriodRows, openPeriod, runFilter]);
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
  const salaryCheckAsOfDate = useMemo(() => {
    const periodDate =
      pick(selectedPeriodForBatch, "endDate", "EndDate") ||
      pick(selectedPeriodForBatch, "payrollDate", "PayrollDate") ||
      pick(selectedPeriodForBatch, "startDate", "StartDate");
    return toDateInputValue(periodDate || new Date());
  }, [selectedPeriodForBatch]);

  const activeStaffForSalaryCheck = useMemo(
    () =>
      (staffs || [])
        .map((staff) => {
          const staffId = toId(pick(staff, "id", "Id"));
          const branchId = toId(pick(staff, "branchId", "BranchId"));
          const hireDate = pick(staff, "hireDate", "HireDate", "createdAt", "CreatedAt");
          const status = String(pick(staff, "status", "Status") || "").trim().toUpperCase();
          return {
            staffId,
            branchId,
            staffName: pick(staff, "fullName", "FullName", "name", "Name", "email", "Email") || `Staff #${staffId || "--"}`,
            hireDate,
            status: status || "UNKNOWN",
          };
        })
        .filter((staff) => {
          if (!staff.staffId) return false;
          if (!staff.status || staff.status === "UNKNOWN") return true;
          return !PAYROLL_EXCLUDED_STAFF_STATUSES.has(staff.status);
        }),
    [staffs],
  );

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
    runRows.forEach((run) => {
      const code = String(run.periodCode || "").trim();
      const normalizedCode = normalizePeriodCode(code);
      if (!code || !normalizedCode || unique.has(normalizedCode)) return;
      unique.add(normalizedCode);
      options.push(code);
    });
    return options;
  }, [runRows]);

  useEffect(() => {
    if (
      runFilter !== "ALL" &&
      !periodFilterOptions.some((periodCode) => normalizePeriodCode(periodCode) === normalizePeriodCode(runFilter))
    ) {
      setRunFilter("ALL");
    }
  }, [periodFilterOptions, runFilter]);

  const selectedPayrollBreakdown = useMemo(() => {
    if (!selectedPayrollItem) return null;

    const originalBaseSalary = toNumber(pick(selectedPayrollSalaryInfo, "baseSalary", "BaseSalary"));
    const coefficient = toNumber(pick(selectedPayrollSalaryInfo, "coefficient", "Coefficient")) || 1;
    const yearsOfService = Math.max(
      0,
      Math.trunc(toNumber(pick(selectedPayrollSalaryInfo, "yearsOfService", "YearsOfService"))),
    );

    return {
      policyAsOfDate: selectedPayrollItem.policyAsOfDate,
      originalBaseSalary,
      hasOriginalBaseSalary: Boolean(pick(selectedPayrollSalaryInfo, "hasBaseSalary", "HasBaseSalary")) || originalBaseSalary > 0,
      coefficient,
      yearsOfService,
      tenureRange: tenureRangeLabel(yearsOfService),
      employeeInsuranceTotal: selectedPayrollItem.insuranceEmployeeAmount,
      pitAmount: selectedPayrollItem.pitAmount,
      totalIncome: selectedPayrollItem.grossPay,
      totalDeduction: selectedPayrollItem.totalDeduction,
      netPay: selectedPayrollItem.netPay,
      amountsSource: selectedPayrollItem.amountsSource,
    };
  }, [selectedPayrollItem, selectedPayrollSalaryInfo]);

  const refreshMissingBaseSalary = useCallback(async () => {
    const requestSeq = missingBaseSalaryRequestSeqRef.current + 1;
    missingBaseSalaryRequestSeqRef.current = requestSeq;

    if (!activeStaffForSalaryCheck.length) {
      setMissingBaseSalaryRows([]);
      setLoadingMissingBaseSalary(false);
      return;
    }

    setLoadingMissingBaseSalary(true);
    try {
      const salaryChecks = await Promise.allSettled(
        activeStaffForSalaryCheck.map(async (staff) => {
          const baseInfo = await getStaffBaseSalary(staff.staffId, salaryCheckAsOfDate, {
            includeTierFallback: false,
          });
          const baseSalary = toNumber(pick(baseInfo, "baseSalary", "BaseSalary"));
          const hasBaseSalary = Boolean(pick(baseInfo, "hasBaseSalary", "HasBaseSalary")) || baseSalary > 0;
          return {
            ...staff,
            baseSalary,
            hasBaseSalary,
            checkError: "",
          };
        }),
      );

      if (requestSeq !== missingBaseSalaryRequestSeqRef.current) return;

      const rows = [];
      salaryChecks.forEach((result, index) => {
        const staff = activeStaffForSalaryCheck[index];
        if (!staff) return;

        if (result.status === "fulfilled") {
          if (!result.value.hasBaseSalary) {
            rows.push(result.value);
          }
          return;
        }

        rows.push({
          ...staff,
          baseSalary: 0,
          hasBaseSalary: false,
          checkError: toFriendlyError(result.reason, "Không đọc được lương cơ bản."),
        });
      });

      rows.sort((a, b) => String(a.staffName || "").localeCompare(String(b.staffName || "")));
      setMissingBaseSalaryRows(rows);
    } finally {
      if (requestSeq === missingBaseSalaryRequestSeqRef.current) {
        setLoadingMissingBaseSalary(false);
      }
    }
  }, [activeStaffForSalaryCheck, salaryCheckAsOfDate]);

  useEffect(() => {
    if (loading) return;
    refreshMissingBaseSalary();
  }, [loading, refreshMissingBaseSalary]);

  const pollInitialSalaryRequestUpdates = useCallback(async () => {
    const currentUserId = getCurrentUserId();
    if (!currentUserId) return;

    try {
      const params = {};
      if (targetBranchId) params.branchId = targetBranchId;
      const rows = await getInitialSalaryRequests(params);
      const list = Array.isArray(rows) ? rows : [];
      const decisionRows = list
        .map((row) => {
          const requestId = toId(pick(row, "id", "Id"));
          const status = String(pick(row, "status", "Status") || "").toUpperCase();
          if (!requestId || (status !== "APPROVED" && status !== "REJECTED")) return null;
          return {
            requestId,
            staffName: pick(row, "staffName", "StaffName", "staff_name") || "--",
            status,
            approverName: pick(row, "approverName", "ApproverName", "approver_name") || "--",
            updatedAt: pick(row, "updatedAt", "UpdatedAt", "updated_at", "createdAt", "CreatedAt", "created_at"),
            rejectReason: pick(row, "rejectReason", "RejectReason", "reject_reason") || "",
          };
        })
        .filter(Boolean)
        .sort((a, b) => {
          const byTime = safeTime(b.updatedAt) - safeTime(a.updatedAt);
          if (byTime !== 0) return byTime;
          return Number(b.requestId) - Number(a.requestId);
        });
      setInitialSalaryDecisionRows(decisionRows);

      const nextStatusMap = new Map();
      const changedRows = [];

      list.forEach((row) => {
        const requestId = toId(pick(row, "id", "Id"));
        const requestedBy = toId(pick(row, "requestedBy", "RequestedBy", "requested_by"));
        const status = String(pick(row, "status", "Status") || "").toUpperCase();

        if (!requestId || !requestedBy || requestedBy !== currentUserId) return;
        nextStatusMap.set(requestId, status);

        if (!hasSeededInitialSalaryRequestStatusRef.current) return;
        const previousStatus = initialSalaryRequestStatusRef.current.get(requestId);
        if (!previousStatus || previousStatus === status) return;
        if (status === "APPROVED" || status === "REJECTED") {
          changedRows.push({ row, status });
        }
      });

      initialSalaryRequestStatusRef.current = nextStatusMap;
      if (!hasSeededInitialSalaryRequestStatusRef.current) {
        hasSeededInitialSalaryRequestStatusRef.current = true;
        return;
      }

      if (!changedRows.length) return;

      changedRows.forEach(({ row, status }) => {
        const staffName = pick(row, "staffName", "StaffName", "staff_name") || "nhan su";
        const rejectReason = pick(row, "rejectReason", "RejectReason", "reject_reason");
        if (status === "APPROVED") {
          toast({
            status: "success",
            title: "Yeu cau luong da duoc Admin duyet",
            description: String(staffName),
          });
        } else {
          toast({
            status: "warning",
            title: "Yeu cau luong bi Admin tu choi",
            description: rejectReason ? `${staffName}: ${rejectReason}` : String(staffName),
          });
        }
      });

      await refreshMissingBaseSalary();
    } catch {
      // Ignore polling errors; user can still manually refresh payroll data.
    }
  }, [refreshMissingBaseSalary, targetBranchId, toast]);

  const initialSalaryDecisionSummary = useMemo(() => {
    const approved = initialSalaryDecisionRows.filter((row) => row.status === "APPROVED").length;
    const rejected = initialSalaryDecisionRows.filter((row) => row.status === "REJECTED").length;
    return { approved, rejected };
  }, [initialSalaryDecisionRows]);

  useEffect(() => {
    if (loading) return undefined;
    pollInitialSalaryRequestUpdates();
    const timer = setInterval(() => {
      pollInitialSalaryRequestUpdates();
    }, 20000);
    return () => clearInterval(timer);
  }, [loading, pollInitialSalaryRequestUpdates]);

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

  async function handleCalculateAllStaffPayroll() {
    if (!targetBranchId) {
      setError("Vui lòng chọn chi nhánh trước khi tính lương.");
      return;
    }
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
        branchId: targetBranchId,
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

  async function handleCancelRun(runId) {
    const run = runById.get(runId);
    if (!run) {
      setError("Không tìm thấy đợt tính lương.");
      return;
    }
    if (!(run.status === "DRAFT" || run.status === "CALCULATED")) {
      setError("Chỉ được huỷ mã kỳ khi đợt đang ở trạng thái Nháp/Calculated.");
      return;
    }

    const confirmed = window.confirm(`Huỷ mã kỳ ${run.periodCode}?`);
    if (!confirmed) return;

    setBusyRunId(runId);
    setError("");
    try {
      await cancelPayrollRun(runId, { canceledBy: getCurrentUserId() || null });
      await reloadRuns();
      toast({ status: "success", title: `Đã huỷ mã kỳ ${run.periodCode}.` });
    } catch (err) {
      setError(toFriendlyError(err, "Huỷ mã kỳ thất bại."));
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
    const payrollItemId = toId(itemId);
    if (!payrollItemId) {
      setError("Dòng lương chưa có ID backend hợp lệ, không thể phát hành phiếu lương.");
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
        payrollItemId,
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

    const targets = filteredItems.filter((item) => item.canIssuePayslip && !item.hasPayslip && toId(item.id));
    if (!targets.length) {
      setError("Không có dòng lương hợp lệ để phát hành phiếu lương hàng loạt.");
      return;
    }

    setIssuingAllPayslips(true);
    setError("");
    let successCount = 0;
    let failedCount = 0;
    let firstError = null;
    let autoPaidSuccess = 0;
    let autoPaidFailed = 0;
    const batchKey = Date.now();
    const affectedRunIds = new Set(
      targets
        .map((item) => toId(item.payrollRunId))
        .filter(Boolean),
    );

    for (let index = 0; index < targets.length; index += 1) {
      const item = targets[index];
      const payrollItemId = toId(item.id);
      if (!payrollItemId) {
        failedCount += 1;
        if (!firstError) firstError = new Error(`Dòng lương của ${item.staffName || "nhân sự"} thiếu ID backend.`);
        continue;
      }
      try {
        await createPayslip({
          payrollItemId,
          slipNo: `PS-${payrollItemId}-${batchKey}-${index + 1}`,
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

    try {
      const [latestRunsData, latestItemsData, latestPayslipsData] = await Promise.all([
        getPayrollRuns(),
        getPayrollItems(),
        getPayslips(),
      ]);
      setRuns(latestRunsData);
      setItems(latestItemsData);
      setPayslips(latestPayslipsData);

      // Auto "xác nhận đã chi" cho các đợt liên quan khi đã đủ payslip.
      if (affectedRunIds.size > 0) {
        const latestPayslipItemIds = new Set();
        (latestPayslipsData.items || []).forEach((slip) => {
          const payrollItemId = toId(pick(slip, "payrollItemId", "PayrollItemId"));
          if (payrollItemId) latestPayslipItemIds.add(payrollItemId);
        });

        const payrollItemIdsByRunId = new Map();
        (latestItemsData.items || []).forEach((item) => {
          const payrollRunId = toId(pick(item, "payrollRunId", "PayrollRunId"));
          const payrollItemId = toId(pick(item, "id", "Id", "payrollItemId", "PayrollItemId", "payroll_item_id"));
          if (!payrollRunId || !payrollItemId) return;
          const arr = payrollItemIdsByRunId.get(payrollRunId) || [];
          arr.push(payrollItemId);
          payrollItemIdsByRunId.set(payrollRunId, arr);
        });

        for (const run of latestRunsData.items || []) {
          const runId = toId(pick(run, "id", "Id", "payrollRunId", "PayrollRunId"));
          if (!runId || !affectedRunIds.has(runId)) continue;

          const runStatus = String(pick(run, "status", "Status") || "").toUpperCase();
          if (runStatus !== "APPROVED") continue;

          const runItemIds = payrollItemIdsByRunId.get(runId) || [];
          if (!runItemIds.length) continue;

          const allHavePayslip = runItemIds.every((itemId) => latestPayslipItemIds.has(itemId));
          if (!allHavePayslip) continue;

          try {
            await markPayrollRunPaid(runId, userId);
            autoPaidSuccess += 1;
          } catch (err) {
            autoPaidFailed += 1;
            if (!firstError) firstError = err;
          }
        }
      }

      if (autoPaidSuccess > 0 || autoPaidFailed > 0) {
        await reloadRuns();
      }
    } catch (err) {
      if (!firstError) firstError = err;
    }

    if (successCount > 0 && failedCount === 0) {
      toast({
        status: "success",
        title: "Đã phát hành toàn bộ phiếu lương.",
        description:
          autoPaidSuccess > 0 || autoPaidFailed > 0
            ? `Số phiếu đã phát hành: ${successCount}. Tự xác nhận đã chi: ${autoPaidSuccess} đợt${autoPaidFailed > 0 ? ` · Lỗi: ${autoPaidFailed}` : ""}.`
            : `Số phiếu đã phát hành: ${successCount}.`,
      });
    } else if (successCount > 0 && failedCount > 0) {
      toast({
        status: "warning",
        title: "Đã phát hành phiếu lương một phần.",
        description:
          autoPaidSuccess > 0 || autoPaidFailed > 0
            ? `Thành công: ${successCount} · Thất bại: ${failedCount}. Tự xác nhận đã chi: ${autoPaidSuccess} đợt${autoPaidFailed > 0 ? ` · Lỗi: ${autoPaidFailed}` : ""}.`
            : `Thành công: ${successCount} · Thất bại: ${failedCount}.`,
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

  function handleOpenInitialSalaryRequest(staff) {
    if (!staff?.staffId) return;
    setInitialSalaryTarget(staff);
    setInitialSalaryForm({
      baseSalary: "",
      reason: "Đề nghị thiết lập lương cơ bản ban đầu",
      note: "",
    });
  }

  function handleCloseInitialSalaryRequest() {
    setInitialSalaryTarget(null);
    setInitialSalaryForm({
      baseSalary: "",
      reason: "",
      note: "",
    });
    setSendingInitialSalaryRequest(false);
  }

  async function handleSubmitInitialSalaryRequest() {
    const staffId = toId(initialSalaryTarget?.staffId);
    if (!staffId) {
      setError("Không xác định được nhân sự để gửi duyệt.");
      return;
    }

    const baseSalary = toNumber(initialSalaryForm.baseSalary);
    if (baseSalary <= 0) {
      setError("Lương cơ bản đề xuất phải lớn hơn 0.");
      return;
    }

    setSendingInitialSalaryRequest(true);
    setError("");
    try {
      const result = await createInitialSalaryRequest(staffId, {
        baseSalary,
        effectiveFrom: salaryCheckAsOfDate,
        reason: initialSalaryForm.reason?.trim() || null,
        note: initialSalaryForm.note?.trim() || null,
      });
      toast({
        status: "success",
        title: "Da gui duyet thanh cong",
        description:
          result?.message ||
          `Yeu cau luong cua ${initialSalaryTarget?.staffName || `Staff #${staffId}`} da duoc chuyen den Admin.`,
      });
      handleCloseInitialSalaryRequest();
      await refreshMissingBaseSalary();
    } catch (err) {
      setError(toFriendlyError(err, "Gửi yêu cầu duyệt lương thất bại."));
    } finally {
      setSendingInitialSalaryRequest(false);
    }
  }

  async function handleReloadMissingBaseSalary() {
    await loadData();
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
        <MiniStatistics name='Tổng thực nhận (server)' value={formatCurrency(officialPayrollTotal)} />
        <MiniStatistics name='Dòng thiếu payroll facts' value={String(missingPayrollFactsCount)} />
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
              Hệ thống sẽ tự lấy dữ liệu lương và chỉ tính cho nhân sự thuộc chi nhánh đang chọn.
            </Text>
            {!targetBranchId ? (
              <Text color='orange.400' fontSize='xs'>
                Cần chọn chi nhánh trước khi tính lương.
              </Text>
            ) : null}
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
              isDisabled={!targetBranchId || !selectedPeriodIdForBatch || selectedPeriodStatusForBatch !== "OPEN"}>
              Tính lương toàn bộ nhân sự
            </Button>
          </SimpleGrid>
        </Card>
      </SimpleGrid>

      <Card p='20px' mb='20px'>
        <Flex justify='space-between' align='center' mb='10px' wrap='wrap' gap='8px'>
          <Text color={textColor} fontWeight='700'>
            Nhân sự chưa có lương cơ bản
          </Text>
          <Flex align='center' gap='8px'>
            <Badge colorScheme={missingBaseSalaryRows.length ? "orange" : "green"}>
              Cần duyệt: {missingBaseSalaryRows.length}
            </Badge>
            <Button
              size='xs'
              variant='outline'
              onClick={handleReloadMissingBaseSalary}
              isLoading={loading || loadingMissingBaseSalary}>
              Tải lại
            </Button>
          </Flex>
        </Flex>

        {loadingMissingBaseSalary ? (
          <Flex align='center' gap='8px'>
            <Spinner size='sm' />
            <Text color={textColorSecondary} fontSize='sm'>
              Đang quét lương cơ bản...
            </Text>
          </Flex>
        ) : missingBaseSalaryRows.length ? (
          <Box maxH='320px' overflowY='auto' overflowX='auto'>
            <Table size='sm'>
              <Thead>
                <Tr>
                  <Th>Nhân sự</Th>
                  <Th>Trạng thái</Th>
                  <Th>Ngày vào</Th>
                  <Th isNumeric>Lương cơ bản</Th>
                  <Th textAlign='right'>Xử lý</Th>
                </Tr>
              </Thead>
              <Tbody>
                {missingBaseSalaryRows.map((row) => (
                  <Tr key={row.staffId}>
                    <Td color={textColor} fontWeight='600'>
                      {row.staffName}
                      {row.checkError ? (
                        <Text color='orange.400' fontSize='xs'>
                          {row.checkError}
                        </Text>
                      ) : null}
                    </Td>
                    <Td>
                      <Badge colorScheme='gray'>{row.status || "--"}</Badge>
                    </Td>
                    <Td color={textColorSecondary}>{formatDate(row.hireDate)}</Td>
                    <Td isNumeric color={textColor}>
                      {row.baseSalary > 0 ? formatCurrency(row.baseSalary) : "--"}
                    </Td>
                    <Td textAlign='right'>
                      <Button
                        size='xs'
                        colorScheme='orange'
                        variant='outline'
                        onClick={() => handleOpenInitialSalaryRequest(row)}>
                        Gửi duyệt lương
                      </Button>
                    </Td>
                  </Tr>
                ))}
              </Tbody>
            </Table>
          </Box>
        ) : (
          <Text color={textColorSecondary}>Không có nhân sự nào thiếu lương cơ bản.</Text>
        )}
      </Card>

      <Card p='20px' mb='20px'>
        <Flex justify='space-between' align='center' mb='10px' wrap='wrap' gap='8px'>
          <Text color={textColor} fontWeight='700'>
            Ket qua duyet luong khoi tao
          </Text>
          <Flex align='center' gap='8px'>
            <Badge colorScheme='green'>Duoc duyet: {initialSalaryDecisionSummary.approved}</Badge>
            <Badge colorScheme='red'>Bi tu choi: {initialSalaryDecisionSummary.rejected}</Badge>
            <Button size='xs' variant='outline' onClick={pollInitialSalaryRequestUpdates}>
              Tai lai
            </Button>
          </Flex>
        </Flex>

        {initialSalaryDecisionRows.length ? (
          <Box maxH='320px' overflowY='auto' overflowX='auto'>
            <Table size='sm'>
              <Thead>
                <Tr>
                  <Th>Nhan su</Th>
                  <Th>Ket qua</Th>
                  <Th>Admin xu ly</Th>
                  <Th>Thoi gian</Th>
                  <Th>Ghi chu</Th>
                </Tr>
              </Thead>
              <Tbody>
                {initialSalaryDecisionRows.slice(0, 100).map((row) => (
                  <Tr key={row.requestId}>
                    <Td color={textColor} fontWeight='600'>
                      {row.staffName}
                    </Td>
                    <Td>
                      <Badge colorScheme={row.status === "APPROVED" ? "green" : "red"}>
                        {row.status === "APPROVED" ? "Da duyet" : "Tu choi"}
                      </Badge>
                    </Td>
                    <Td color={textColorSecondary}>{row.approverName || "--"}</Td>
                    <Td color={textColorSecondary}>{formatDateTime(row.updatedAt)}</Td>
                    <Td color={textColorSecondary}>
                      {row.status === "REJECTED" ? row.rejectReason || "--" : "--"}
                    </Td>
                  </Tr>
                ))}
              </Tbody>
            </Table>
          </Box>
        ) : (
          <Text color={textColorSecondary}>Chua co ket qua duyet/tu choi.</Text>
        )}
      </Card>

      <SimpleGrid columns={{ base: 1, xl: 2 }} gap='20px' mb='20px'>
        <Card p='20px'>
          <Text color={textColor} fontWeight='700' mb='10px'>Danh sách kỳ lương</Text>
          {periodRows.length ? (
            <Box maxH='420px' overflowY='auto' overflowX='auto'>
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
                  {periodRows.map((period, idx) => {
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
            </Box>
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
            <Box maxH='420px' overflowY='auto' overflowX='auto'>
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
                  {runRows.map((run) => (
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
                            variant='solid'
                            isDisabled={run.status !== "APPROVED"}
                            isLoading={busyRunId === run.id}
                            onClick={() => handleMarkPaid(run.id)}
                            _disabled={
                              run.status === "PAID"
                                ? {
                                  opacity: 1,
                                  bg: "green.400",
                                  color: "white",
                                  cursor: "not-allowed",
                                  pointerEvents: "none",
                                }
                                : undefined
                            }>
                            {run.status === "PAID" ? "Đã chi" : "Xác nhận đã chi"}
                          </Button>
                          <Button
                            size='xs'
                            colorScheme='red'
                            variant='outline'
                            isDisabled={!(run.status === "DRAFT" || run.status === "CALCULATED")}
                            isLoading={busyRunId === run.id}
                            onClick={() => handleCancelRun(run.id)}>
                            Huỷ mã kỳ
                          </Button>
                        </Flex>
                      </Td>
                    </Tr>
                  ))}
                </Tbody>
              </Table>
            </Box>
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
                <Th isNumeric>Tổng thu nhập*</Th>
                <Th isNumeric>Tổng khấu trừ*</Th>
                <Th isNumeric>Thực nhận*</Th>
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
                  <Td color={textColor} isNumeric>{item.grossPay == null ? "--" : formatCurrency(item.grossPay)}</Td>
                  <Td color={textColor} isNumeric>{item.totalDeduction == null ? "--" : formatCurrency(item.totalDeduction)}</Td>
                  <Td color={textColor} isNumeric>{item.netPay == null ? "--" : formatCurrency(item.netPay)}</Td>
                  <Td>
                    <Badge colorScheme={statusColor(item.status)}>
                      {statusLabel(item.status)}
                    </Badge>
                  </Td>
                  <Td>
                    <Badge colorScheme={item.hasPayslip ? "green" : "gray"}>
                      {item.hasPayslip ? "Đã phát hành" : "Chưa phát hành"}
                    </Badge>
                    <Text color={textColorSecondary} fontSize='xs'>
                      {item.amountsSource === "SERVER"
                        ? "Server"
                        : item.amountsSource === "DERIVED"
                        ? "Suy diễn"
                        : "Thiếu totals"}
                    </Text>
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
        <Text color={textColorSecondary} fontSize='xs' mt='10px'>
          * Ưu tiên hiển thị totals backend. Nếu backend chưa trả explicit totals, frontend sẽ suy diễn từ các thành phần lương/khấu trừ.
        </Text>
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

            {selectedPayrollBreakdown?.amountsSource === "INCOMPLETE" ? (
              <Text color='orange.400' fontSize='sm' mb='10px'>
                Backend chưa trả đủ breakdown canonical cho dòng lương này. Popup chỉ hiển thị các số server đã cung cấp.
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
                    <Text color={textColorSecondary} fontSize='xs'>Tổng BH NLĐ</Text>
                    <Text color={textColor} fontWeight='700'>{formatCurrency(selectedPayrollBreakdown.employeeInsuranceTotal)}</Text>
                    <Text color={textColorSecondary} fontSize='xs' mt='6px'>
                      Trang này không còn tách BHXH/BHYT/BHTN ở frontend nếu backend chưa trả breakdown chi tiết.
                    </Text>
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
                        {selectedPayrollBreakdown.totalIncome == null ? "--" : formatCurrency(selectedPayrollBreakdown.totalIncome)}
                      </Text>
                    </Box>
                    <Box>
                      <Text color='green.700' fontSize='xs'>Tổng khấu trừ</Text>
                      <Text color='green.800' fontWeight='800' fontSize='lg'>
                        {selectedPayrollBreakdown.totalDeduction == null ? "--" : formatCurrency(selectedPayrollBreakdown.totalDeduction)}
                      </Text>
                    </Box>
                    <Box>
                      <Text color='green.700' fontSize='xs'>Tổng tiền nhận được</Text>
                      <Text color='green.800' fontWeight='800' fontSize='lg'>
                        {selectedPayrollBreakdown.netPay == null ? "--" : formatCurrency(selectedPayrollBreakdown.netPay)}
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

      <Modal isOpen={Boolean(initialSalaryTarget)} onClose={handleCloseInitialSalaryRequest} isCentered size='lg'>
        <ModalOverlay />
        <ModalContent>
          <ModalHeader>Gửi duyệt lương khởi tạo</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            <SimpleGrid columns={1} gap='10px'>
              <FormControl>
                <FormLabel fontSize='xs' mb='4px'>
                  Nhân sự
                </FormLabel>
                <Input value={initialSalaryTarget?.staffName || "--"} isReadOnly />
              </FormControl>
              <FormControl>
                <FormLabel fontSize='xs' mb='4px'>
                  Hiệu lực từ ngày
                </FormLabel>
                <Input type='date' value={salaryCheckAsOfDate} isReadOnly />
              </FormControl>
              <FormControl isRequired>
                <FormLabel fontSize='xs' mb='4px'>
                  Lương cơ bản đề xuất
                </FormLabel>
                <Input
                  type='number'
                  min='0'
                  step='1000'
                  value={initialSalaryForm.baseSalary}
                  onChange={(e) => setInitialSalaryForm((prev) => ({ ...prev, baseSalary: e.target.value }))}
                  placeholder='VD: 12.000.000'
                />
              </FormControl>
              <FormControl>
                <FormLabel fontSize='xs' mb='4px'>
                  Lý do
                </FormLabel>
                <Input
                  value={initialSalaryForm.reason}
                  onChange={(e) => setInitialSalaryForm((prev) => ({ ...prev, reason: e.target.value }))}
                  placeholder='Lý do đề xuất'
                />
              </FormControl>
              <FormControl>
                <FormLabel fontSize='xs' mb='4px'>
                  Ghi chú
                </FormLabel>
                <Textarea
                  value={initialSalaryForm.note}
                  onChange={(e) => setInitialSalaryForm((prev) => ({ ...prev, note: e.target.value }))}
                  placeholder='Thông tin bổ sung'
                  rows={3}
                />
              </FormControl>
            </SimpleGrid>
          </ModalBody>
          <ModalFooter gap='8px'>
            <Button variant='ghost' onClick={handleCloseInitialSalaryRequest}>
              Hủy
            </Button>
            <Button
              colorScheme='orange'
              onClick={handleSubmitInitialSalaryRequest}
              isLoading={sendingInitialSalaryRequest}>
              Gửi duyệt
            </Button>
          </ModalFooter>
        </ModalContent>
      </Modal>

    </Box>
  );
}



