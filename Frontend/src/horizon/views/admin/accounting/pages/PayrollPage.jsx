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
  createPayrollPeriod,
  createPayslip,
  getBranches,
  getPayrollItems,
  getPayrollPeriods,
  getPayrollRuns,
  getPayslips,
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

function derivePayrollAmounts(item) {
  const explicitGrossPay = pick(item, "grossPay", "GrossPay");
  const explicitTotalDeduction = pick(item, "totalDeduction", "TotalDeduction");
  const explicitNetPay = pick(item, "netPay", "NetPay");

  const grossPay = explicitGrossPay !== undefined && explicitGrossPay !== null ? toNumber(explicitGrossPay) : null;
  const totalDeduction =
    explicitTotalDeduction !== undefined && explicitTotalDeduction !== null ? toNumber(explicitTotalDeduction) : null;
  const netPay = explicitNetPay !== undefined && explicitNetPay !== null ? toNumber(explicitNetPay) : null;

  return {
    grossPay,
    totalDeduction,
    netPay,
    hasOfficialGrossPay: explicitGrossPay !== undefined && explicitGrossPay !== null,
    hasOfficialTotalDeduction: explicitTotalDeduction !== undefined && explicitTotalDeduction !== null,
    hasOfficialNetPay: explicitNetPay !== undefined && explicitNetPay !== null
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
    return "NgÃ y ká»³ lÆ°Æ¡ng khÃ´ng há»£p lá»‡. Quy táº¯c: Tá»« ngÃ y <= Äáº¿n ngÃ y vÃ  NgÃ y tráº£ lÆ°Æ¡ng pháº£i báº±ng hoáº·c sau ngÃ y káº¿t thÃºc ká»³.";
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
  if (code === "OPEN") return "green";
  if (code === "CLOSED") return "gray";
  if (code === "LOCKED") return "purple";
  return "orange";
}

function statusLabel(status) {
  const code = String(status || "").toUpperCase();
  if (code === "DRAFT") return "NhÃ¡p";
  if (code === "APPROVED") return "ÄÃ£ duyá»‡t";
  if (code === "PAID") return "ÄÃ£ chi";
  if (code === "OPEN") return "Äang má»Ÿ";
  if (code === "LOCKED") return "Táº¡m khÃ³a";
  if (code === "CLOSED") return "ÄÃ£ Ä‘Ã³ng";
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
  { minYears: 0, label: "DÆ°á»›i 5 nÄƒm" },
  { minYears: 5, label: "5 Ä‘áº¿n dÆ°á»›i 10 nÄƒm" },
  { minYears: 10, label: "10 Ä‘áº¿n dÆ°á»›i 15 nÄƒm" },
  { minYears: 15, label: "15 Ä‘áº¿n dÆ°á»›i 20 nÄƒm" },
  { minYears: 20, label: "20 Ä‘áº¿n dÆ°á»›i 25 nÄƒm" },
  { minYears: 25, label: "Tá»« 25 nÄƒm trá»Ÿ lÃªn" },
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
  const payrollDetailRequestSeqRef = useRef(0);

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
      setError("KhÃ´ng táº£i Ä‘Æ°á»£c dá»¯ liá»‡u tiá»n lÆ°Æ¡ng tá»« backend.");
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
        hasOfficialGrossPay,
        hasOfficialTotalDeduction,
        hasOfficialNetPay,
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
        amountsSource:
          hasOfficialGrossPay && hasOfficialTotalDeduction && hasOfficialNetPay ? "SERVER" : "INCOMPLETE",
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
    () => itemRows.filter((item) => item.amountsSource !== "SERVER").length,
    [itemRows],
  );

  const openPeriod = useMemo(() => periodRows.find((period) => String(period.status || "").toUpperCase() === "OPEN"), [periodRows]);
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
    if (!selectedPeriodIdForBatch) return "ChÆ°a cÃ³ ká»³ lÆ°Æ¡ng Ä‘ang má»Ÿ";
    if (selectedPeriodStatusForBatch !== "OPEN") {
      return `Ká»³ ${selectedPeriodCodeForBatch || "--"} chÆ°a á»Ÿ tráº¡ng thÃ¡i ÄANG Má»ž`;
    }
    if (activeDraftRun) return `${activeDraftRun.runNo} Â· ${activeDraftRun.periodCode}`;
    return `Sáº½ tá»± táº¡o Ä‘á»£t NhÃ¡p cho ká»³ ${selectedPeriodCodeForBatch}`;
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
      setError("Vui lÃ²ng nháº­p mÃ£ ká»³ lÆ°Æ¡ng.");
      return;
    }
    if (!periodForm.startDate || !periodForm.endDate || !periodForm.payrollDate) {
      setError("Vui lÃ²ng nháº­p Ä‘áº§y Ä‘á»§ ngÃ y báº¯t Ä‘áº§u, káº¿t thÃºc vÃ  ngÃ y tráº£ lÆ°Æ¡ng.");
      return;
    }

    const startDate = parseDateValue(periodForm.startDate);
    const endDate = parseDateValue(periodForm.endDate);
    const payrollDate = parseDateValue(periodForm.payrollDate);

    if (!startDate || !endDate || !payrollDate) {
      setError("Äá»‹nh dáº¡ng ngÃ y khÃ´ng há»£p lá»‡.");
      return;
    }

    if (startDate > endDate) {
      setError("Tá»« ngÃ y pháº£i nhá» hÆ¡n hoáº·c báº±ng Äáº¿n ngÃ y.");
      return;
    }

    if (payrollDate < endDate) {
      setError("NgÃ y tráº£ lÆ°Æ¡ng pháº£i báº±ng hoáº·c sau ngÃ y káº¿t thÃºc ká»³.");
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
      toast({ status: "success", title: "ÄÃ£ táº¡o ká»³ lÆ°Æ¡ng má»›i." });
      setPeriodForm(createDefaultPeriodForm());
      setRunFilter("ALL");
      await reloadPeriods();
    } catch (err) {
      setError(toFriendlyError(err, "Táº¡o ká»³ lÆ°Æ¡ng tháº¥t báº¡i."));
    } finally {
      setSavingPeriod(false);
    }
  }

  async function handleCalculateAllStaffPayroll() {
    if (!selectedPeriodIdForBatch) {
      setError("ChÆ°a xÃ¡c Ä‘á»‹nh Ä‘Æ°á»£c ká»³ lÆ°Æ¡ng Ä‘á»ƒ tÃ­nh. Vui lÃ²ng chá»n má»™t ká»³ Ä‘ang má»Ÿ.");
      return;
    }
    if (selectedPeriodStatusForBatch !== "OPEN") {
      setError(`Ká»³ ${selectedPeriodCodeForBatch || "--"} chÆ°a á»Ÿ tráº¡ng thÃ¡i ÄANG Má»ž nÃªn khÃ´ng thá»ƒ tÃ­nh lÆ°Æ¡ng hÃ ng loáº¡t.`);
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
          title: "Äá»£t lÆ°Æ¡ng Ä‘Ã£ táº¡o nhÆ°ng chÆ°a cÃ³ dÃ²ng lÆ°Æ¡ng.",
          description: `Ká»³ ${selectedPeriodCodeForBatch}: ${processed} nhÃ¢n sá»± Ä‘Æ°á»£c quÃ©t, thiáº¿u lÆ°Æ¡ng gá»‘c hiá»‡u lá»±c: ${skippedNoBase}.`,
        });
        return;
      }

      toast({
        status: "success",
        title: "ÄÃ£ tÃ­nh lÆ°Æ¡ng hÃ ng loáº¡t.",
        description: `Ká»³ ${selectedPeriodCodeForBatch}: NhÃ¢n sá»± xá»­ lÃ½: ${processed} Â· ThÃªm má»›i: ${inserted} Â· Cáº­p nháº­t: ${updated} Â· Thiáº¿u lÆ°Æ¡ng gá»‘c: ${skippedNoBase} Â· Tá»•ng thá»±c nháº­n: ${formatCurrency(netTotal)}`,
      });
    } catch (err) {
      setError(toFriendlyError(err, "TÃ­nh lÆ°Æ¡ng hÃ ng loáº¡t tháº¥t báº¡i."));
    } finally {
      setCalculatingBatchPayroll(false);
    }
  }

  async function handleApprove(runId) {
    const userId = getCurrentUserId();
    if (!userId) {
      setError("KhÃ´ng xÃ¡c Ä‘á»‹nh Ä‘Æ°á»£c ngÆ°á»i dÃ¹ng hiá»‡n táº¡i Ä‘á»ƒ duyá»‡t Ä‘á»£t tÃ­nh lÆ°Æ¡ng.");
      return;
    }
    const run = runById.get(runId);
    if (!run) {
      setError("KhÃ´ng tÃ¬m tháº¥y Ä‘á»£t tÃ­nh lÆ°Æ¡ng.");
      return;
    }
    if (run.status !== "DRAFT") {
      setError("Chá»‰ Ä‘á»£t tÃ­nh lÆ°Æ¡ng á»Ÿ tráº¡ng thÃ¡i NhÃ¡p má»›i Ä‘Æ°á»£c duyá»‡t.");
      return;
    }
    const hasItems = itemRows.some((item) => item.payrollRunId === runId);
    if (!hasItems) {
      setError("KhÃ´ng thá»ƒ duyá»‡t Ä‘á»£t chÆ°a cÃ³ dÃ²ng lÆ°Æ¡ng.");
      return;
    }
    setBusyRunId(runId);
    setError("");
    try {
      await approvePayrollRun(runId, userId);
      await reloadRuns();
      toast({ status: "success", title: `ÄÃ£ duyá»‡t Ä‘á»£t tÃ­nh lÆ°Æ¡ng ${run.runNo}.` });
    } catch (err) {
      setError(toFriendlyError(err, "Duyá»‡t Ä‘á»£t tÃ­nh lÆ°Æ¡ng tháº¥t báº¡i."));
    } finally {
      setBusyRunId(null);
    }
  }

  async function handleMarkPaid(runId) {
    const run = runById.get(runId);
    if (!run) {
      setError("KhÃ´ng tÃ¬m tháº¥y Ä‘á»£t tÃ­nh lÆ°Æ¡ng.");
      return;
    }
    if (run.status !== "APPROVED") {
      setError("Äá»£t tÃ­nh lÆ°Æ¡ng pháº£i á»Ÿ tráº¡ng thÃ¡i ÄÃ£ duyá»‡t trÆ°á»›c khi xÃ¡c nháº­n Ä‘Ã£ chi.");
      return;
    }
    const runItems = itemRows.filter((item) => item.payrollRunId === runId);
    if (!runItems.length) {
      setError("Äá»£t tÃ­nh lÆ°Æ¡ng chÆ°a cÃ³ dÃ²ng lÆ°Æ¡ng.");
      return;
    }
    const allHavePayslip = runItems.every((item) => item.hasPayslip);
    if (!allHavePayslip) {
      setError("Vui lÃ²ng phÃ¡t hÃ nh phiáº¿u lÆ°Æ¡ng cho toÃ n bá»™ dÃ²ng lÆ°Æ¡ng trÆ°á»›c khi xÃ¡c nháº­n Ä‘Ã£ chi.");
      return;
    }

    setBusyRunId(runId);
    setError("");
    try {
      await markPayrollRunPaid(runId, getCurrentUserId() || 1);
      await reloadRuns();
      toast({ status: "success", title: `ÄÃ£ xÃ¡c nháº­n chi xong Ä‘á»£t ${run.runNo}.` });
    } catch (err) {
      setError(toFriendlyError(err, "XÃ¡c nháº­n Ä‘Ã£ chi tháº¥t báº¡i."));
    } finally {
      setBusyRunId(null);
    }
  }

  async function handleIssuePayslip(itemId) {
    const userId = getCurrentUserId();
    if (!userId) {
      setError("KhÃ´ng xÃ¡c Ä‘á»‹nh Ä‘Æ°á»£c ngÆ°á»i dÃ¹ng hiá»‡n táº¡i Ä‘á»ƒ phÃ¡t hÃ nh phiáº¿u lÆ°Æ¡ng.");
      return;
    }
    const payrollItemId = toId(itemId);
    if (!payrollItemId) {
      setError("DÃ²ng lÆ°Æ¡ng chÆ°a cÃ³ ID backend há»£p lá»‡, khÃ´ng thá»ƒ phÃ¡t hÃ nh phiáº¿u lÆ°Æ¡ng.");
      return;
    }
    const row = itemRows.find((item) => item.id === itemId);
    if (!row) {
      setError("KhÃ´ng tÃ¬m tháº¥y dÃ²ng lÆ°Æ¡ng.");
      return;
    }
    if (!row.canIssuePayslip) {
      setError("Chá»‰ Ä‘Æ°á»£c phÃ¡t hÃ nh phiáº¿u lÆ°Æ¡ng khi Ä‘á»£t tÃ­nh lÆ°Æ¡ng Ä‘Ã£ duyá»‡t hoáº·c Ä‘Ã£ chi.");
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
      toast({ status: "success", title: `ÄÃ£ phÃ¡t hÃ nh phiáº¿u lÆ°Æ¡ng cho dÃ²ng #${itemId}.` });
    } catch (err) {
      setError(toFriendlyError(err, "PhÃ¡t hÃ nh phiáº¿u lÆ°Æ¡ng tháº¥t báº¡i."));
    } finally {
      setBusyItemId(null);
    }
  }

  async function handleIssueAllPayslips() {
    const userId = getCurrentUserId();
    if (!userId) {
      setError("KhÃ´ng xÃ¡c Ä‘á»‹nh Ä‘Æ°á»£c ngÆ°á»i dÃ¹ng hiá»‡n táº¡i Ä‘á»ƒ phÃ¡t hÃ nh phiáº¿u lÆ°Æ¡ng.");
      return;
    }

    const targets = filteredItems.filter((item) => item.canIssuePayslip && !item.hasPayslip && toId(item.id));
    if (!targets.length) {
      setError("KhÃ´ng cÃ³ dÃ²ng lÆ°Æ¡ng há»£p lá»‡ Ä‘á»ƒ phÃ¡t hÃ nh phiáº¿u lÆ°Æ¡ng hÃ ng loáº¡t.");
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
        if (!firstError) firstError = new Error(`DÃ²ng lÆ°Æ¡ng cá»§a ${item.staffName || "nhÃ¢n sá»±"} thiáº¿u ID backend.`);
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

      // Auto "xÃ¡c nháº­n Ä‘Ã£ chi" cho cÃ¡c Ä‘á»£t liÃªn quan khi Ä‘Ã£ Ä‘á»§ payslip.
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
        title: "ÄÃ£ phÃ¡t hÃ nh toÃ n bá»™ phiáº¿u lÆ°Æ¡ng.",
        description:
          autoPaidSuccess > 0 || autoPaidFailed > 0
            ? `Sá»‘ phiáº¿u Ä‘Ã£ phÃ¡t hÃ nh: ${successCount}. Tá»± xÃ¡c nháº­n Ä‘Ã£ chi: ${autoPaidSuccess} Ä‘á»£t${autoPaidFailed > 0 ? ` Â· Lá»—i: ${autoPaidFailed}` : ""}.`
            : `Sá»‘ phiáº¿u Ä‘Ã£ phÃ¡t hÃ nh: ${successCount}.`,
      });
    } else if (successCount > 0 && failedCount > 0) {
      toast({
        status: "warning",
        title: "ÄÃ£ phÃ¡t hÃ nh phiáº¿u lÆ°Æ¡ng má»™t pháº§n.",
        description:
          autoPaidSuccess > 0 || autoPaidFailed > 0
            ? `ThÃ nh cÃ´ng: ${successCount} Â· Tháº¥t báº¡i: ${failedCount}. Tá»± xÃ¡c nháº­n Ä‘Ã£ chi: ${autoPaidSuccess} Ä‘á»£t${autoPaidFailed > 0 ? ` Â· Lá»—i: ${autoPaidFailed}` : ""}.`
            : `ThÃ nh cÃ´ng: ${successCount} Â· Tháº¥t báº¡i: ${failedCount}.`,
      });
      setError(firstError ? toFriendlyError(firstError, "Má»™t sá»‘ phiáº¿u lÆ°Æ¡ng phÃ¡t hÃ nh tháº¥t báº¡i.") : "Má»™t sá»‘ phiáº¿u lÆ°Æ¡ng phÃ¡t hÃ nh tháº¥t báº¡i.");
    } else {
      setError(firstError ? toFriendlyError(firstError, "PhÃ¡t hÃ nh toÃ n bá»™ phiáº¿u lÆ°Æ¡ng tháº¥t báº¡i.") : "PhÃ¡t hÃ nh toÃ n bá»™ phiáº¿u lÆ°Æ¡ng tháº¥t báº¡i.");
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
        setPayrollDetailError("KhÃ´ng láº¥y Ä‘Æ°á»£c há»‡ sá»‘ lÆ°Æ¡ng tá»« server, Ä‘ang hiá»ƒn thá»‹ há»‡ sá»‘ máº·c Ä‘á»‹nh 1.0000.");
      } catch {
        setPayrollDetailError(toFriendlyError(err, "KhÃ´ng táº£i Ä‘Æ°á»£c thÃ´ng tin há»‡ sá»‘ lÆ°Æ¡ng cho nhÃ¢n sá»± nÃ y."));
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
        <Text color={textColorSecondary}>Äang táº£i dá»¯ liá»‡u tiá»n lÆ°Æ¡ng...</Text>
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
          name='Ká»³ Ä‘ang má»Ÿ'
                  value={String(pick(openPeriod, "periodCode", "PeriodCode") || "Chưa có")}
        />
        <MiniStatistics name='Ká»³ tÃ­nh lÆ°Æ¡ng' value={String(runRows.length)} />
        <MiniStatistics name='DÃ²ng lÆ°Æ¡ng' value={String(itemRows.length)} />
        <MiniStatistics name='Tá»•ng thá»±c nháº­n (server)' value={formatCurrency(officialPayrollTotal)} />
        <MiniStatistics name='DÃ²ng thiáº¿u payroll facts' value={String(missingPayrollFactsCount)} />
      </SimpleGrid>

      <Card p='16px' mb='20px'>
        <Flex wrap='wrap' gap='10px' align='center'>
                <Badge colorScheme={periods.available ? "green" : "orange"}>API Kỳ lương: {periods.available ? "OK" : "Chưa có"}</Badge>
                <Badge colorScheme={runs.available ? "green" : "orange"}>API Đợt lương: {runs.available ? "OK" : "Chưa có"}</Badge>
                <Badge colorScheme={items.available ? "green" : "orange"}>API Dòng lương: {items.available ? "OK" : "Chưa có"}</Badge>
                <Badge colorScheme={payslips.available ? "green" : "orange"}>API Phiếu lương: {payslips.available ? "OK" : "Chưa có"}</Badge>
          <Badge colorScheme='blue'>NhÃ¢n sá»±: {staffs.length}</Badge>
        </Flex>
      </Card>

      <SimpleGrid columns={{ base: 1, xl: 2 }} gap='20px' mb='20px'>
        <Card p='16px'>
          <Text color={textColor} fontWeight='700' mb='10px'>Má»Ÿ ká»³ lÆ°Æ¡ng</Text>
          <SimpleGrid columns={1} gap='10px'>
            <FormControl>
              <FormLabel fontSize='xs' mb='4px'>MÃ£ ká»³ lÆ°Æ¡ng</FormLabel>
              <Input
                value={periodForm.periodCode}
                onChange={(e) => setPeriodForm((prev) => ({ ...prev, periodCode: e.target.value }))}
                placeholder='VD: 2026-03'
              />
            </FormControl>
            <SimpleGrid columns={2} gap='10px'>
              <FormControl>
                <FormLabel fontSize='xs' mb='4px'>Tá»« ngÃ y</FormLabel>
                <Input
                  type='date'
                  value={periodForm.startDate}
                  onChange={(e) => setPeriodForm((prev) => ({ ...prev, startDate: e.target.value }))}
                />
              </FormControl>
              <FormControl>
                <FormLabel fontSize='xs' mb='4px'>Äáº¿n ngÃ y</FormLabel>
                <Input
                  type='date'
                  value={periodForm.endDate}
                  onChange={(e) => updatePeriodEndDate(e.target.value)}
                />
              </FormControl>
            </SimpleGrid>
            <SimpleGrid columns={2} gap='10px'>
              <FormControl>
                <FormLabel fontSize='xs' mb='4px'>NgÃ y tráº£ lÆ°Æ¡ng</FormLabel>
                <Input
                  type='date'
                  value={periodForm.payrollDate}
                  onChange={(e) => setPeriodForm((prev) => ({ ...prev, payrollDate: e.target.value }))}
                />
              </FormControl>
              <FormControl>
                <FormLabel fontSize='xs' mb='4px'>Tráº¡ng thÃ¡i</FormLabel>
                <Select
                  value={periodForm.status}
                  onChange={(e) => setPeriodForm((prev) => ({ ...prev, status: e.target.value }))}>
                  <option value='OPEN'>Äang má»Ÿ</option>
                </Select>
              </FormControl>
            </SimpleGrid>
            <Text color={textColorSecondary} fontSize='xs'>
              Luu Ã½: NgÃ y tráº£ lÆ°Æ¡ng pháº£i báº±ng hoáº·c sau ngÃ y káº¿t thÃºc ká»³.
            </Text>
            <Button size='sm' colorScheme='blue' onClick={handleCreatePeriod} isLoading={savingPeriod}>
              Táº¡o ká»³ lÆ°Æ¡ng
            </Button>
          </SimpleGrid>
        </Card>

        <Card p='16px'>
          <Text color={textColor} fontWeight='700' mb='10px'>TÃ­nh lÆ°Æ¡ng toÃ n bá»™ nhÃ¢n sá»±</Text>
          <SimpleGrid columns={1} gap='10px'>
            <FormControl>
              <FormLabel fontSize='xs' mb='4px'>Ká»³ lÆ°Æ¡ng (tá»± Ä‘á»™ng)</FormLabel>
              <Input value={autoRunLabel} isReadOnly />
            </FormControl>
            <Text color={textColorSecondary} fontSize='sm'>
              Há»‡ thá»‘ng sáº½ tá»± láº¥y dá»¯ liá»‡u lÆ°Æ¡ng vÃ  tÃ­nh cho toÃ n bá»™ nhÃ¢n sá»± trong ká»³ Ä‘ang má»Ÿ.
            </Text>
            {!selectedPeriodIdForBatch ? (
              <Text color='orange.400' fontSize='xs'>
                Cáº§n cÃ³ ká»³ lÆ°Æ¡ng Ä‘ang má»Ÿ trÆ°á»›c khi tÃ­nh lÆ°Æ¡ng hÃ ng loáº¡t.
              </Text>
            ) : null}
            {selectedPeriodIdForBatch && selectedPeriodStatusForBatch !== "OPEN" ? (
              <Text color='orange.400' fontSize='xs'>
                Ká»³ Ä‘Ã£ chá»n chÆ°a á»Ÿ tráº¡ng thÃ¡i ÄANG Má»ž.
              </Text>
            ) : null}
            <Button
              size='sm'
              colorScheme='teal'
              variant='outline'
              onClick={handleCalculateAllStaffPayroll}
              isLoading={calculatingBatchPayroll}
              isDisabled={!selectedPeriodIdForBatch || selectedPeriodStatusForBatch !== "OPEN"}>
              TÃ­nh lÆ°Æ¡ng toÃ n bá»™ nhÃ¢n sá»±
            </Button>
          </SimpleGrid>
        </Card>
      </SimpleGrid>

      <SimpleGrid columns={{ base: 1, xl: 2 }} gap='20px' mb='20px'>
        <Card p='20px'>
          <Text color={textColor} fontWeight='700' mb='10px'>Danh sÃ¡ch ká»³ lÆ°Æ¡ng</Text>
          {periodRows.length ? (
            <Box maxH='420px' overflowY='auto' overflowX='auto'>
              <Table size='sm'>
                <Thead>
                  <Tr>
                    <Th>MÃ£ ká»³</Th>
                    <Th>Tá»« ngÃ y</Th>
                    <Th>Äáº¿n ngÃ y</Th>
                    <Th>NgÃ y tráº£ lÆ°Æ¡ng</Th>
                    <Th>Tráº¡ng thÃ¡i</Th>
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
            <Text color={textColorSecondary}>ChÆ°a cÃ³ dá»¯ liá»‡u ká»³ lÆ°Æ¡ng.</Text>
          )}
        </Card>

        <Card p='20px'>
          <Flex justify='space-between' align='center' mb='10px' wrap='wrap' gap='8px'>
            <Text color={textColor} fontWeight='700'>Danh sÃ¡ch ká»³ lÆ°Æ¡ng</Text>
            <Flex gap='6px'>
              <Badge colorScheme='orange'>NhÃ¡p: {runSummary.draft}</Badge>
              <Badge colorScheme='blue'>ÄÃ£ duyá»‡t: {runSummary.approved}</Badge>
              <Badge colorScheme='green'>ÄÃ£ chi: {runSummary.paid}</Badge>
            </Flex>
          </Flex>
          {runRows.length ? (
            <Box maxH='420px' overflowY='auto' overflowX='auto'>
              <Table size='sm'>
                <Thead>
                  <Tr>
                    <Th>MÃ£ ká»³</Th>
                    <Th>Chi nhÃ¡nh</Th>
                    <Th>NgÃ y táº¡o</Th>
                    <Th>Status</Th>
                    <Th textAlign='right'>Thao tÃ¡c</Th>
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
                            Duyá»‡t
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
                            {run.status === "PAID" ? "ÄÃ£ chi" : "XÃ¡c nháº­n Ä‘Ã£ chi"}
                          </Button>
                        </Flex>
                      </Td>
                    </Tr>
                  ))}
                </Tbody>
              </Table>
            </Box>
          ) : (
            <Text color={textColorSecondary}>ChÆ°a cÃ³ ká»³ lÆ°Æ¡ng.</Text>
          )}
        </Card>
      </SimpleGrid>

      <Card p='20px' mb='20px'>
        <Flex wrap='wrap' gap='10px' mb='10px'>
          <Select value={runFilter} onChange={(e) => setRunFilter(e.target.value)} maxW='220px'>
            <option value='ALL'>Táº¥t cáº£ ká»³ lÆ°Æ¡ng</option>
            {periodFilterOptions.map((periodCode) => (
              <option value={periodCode} key={periodCode}>{periodCode}</option>
            ))}
          </Select>
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder='TÃ¬m theo nhÃ¢n sá»± / ká»³ / tráº¡ng thÃ¡i'
            maxW='320px'
          />
          <Button
            size='sm'
            colorScheme='purple'
            variant='outline'
            onClick={handleIssueAllPayslips}
            isLoading={issuingAllPayslips}
            isDisabled={issuingAllPayslips || issuablePayslipCount <= 0}>
            PhÃ¡t hÃ nh toÃ n bá»™ phiáº¿u lÆ°Æ¡ng ({issuablePayslipCount})
          </Button>
        </Flex>

        {filteredItems.length ? (
          <Table size='sm'>
            <Thead>
              <Tr>
                <Th>NhÃ¢n sá»±</Th>
                <Th>MÃ£ ká»³</Th>
                <Th isNumeric>Tá»•ng thu nháº­p*</Th>
                <Th isNumeric>Tá»•ng kháº¥u trá»«*</Th>
                <Th isNumeric>Thá»±c nháº­n*</Th>
                <Th>Status</Th>
                <Th>Phiáº¿u lÆ°Æ¡ng</Th>
                <Th textAlign='right'>Thao tÃ¡c</Th>
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
                      {item.hasPayslip ? "ÄÃ£ phÃ¡t hÃ nh" : "ChÆ°a phÃ¡t hÃ nh"}
                    </Badge>
                    <Text color={textColorSecondary} fontSize='xs'>
                      {item.amountsSource === "SERVER" ? "Server" : "Thiáº¿u totals"}
                    </Text>
                    {!item.canIssuePayslip ? (
                      <Text color={textColorSecondary} fontSize='xs'>
                        Chá» duyá»‡t Ä‘á»£t
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
                      PhÃ¡t hÃ nh phiáº¿u lÆ°Æ¡ng
                    </Button>
                  </Td>
                </Tr>
              ))}
            </Tbody>
          </Table>
        ) : (
          <Text color={textColorSecondary}>ChÆ°a cÃ³ dÃ²ng lÆ°Æ¡ng phÃ¹ há»£p bá»™ lá»c.</Text>
        )}
        <Text color={textColorSecondary} fontSize='xs' mt='10px'>
          * Náº¿u backend chÆ°a tráº£ explicit totals thÃ¬ cá»™t sáº½ Ä‘á»ƒ trá»‘ng thay vÃ¬ Ä‘á»ƒ frontend tá»± suy diá»…n.
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
            Chi tiáº¿t lÆ°Æ¡ng: {selectedPayrollItem?.staffName || "--"}
          </ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            <SimpleGrid columns={{ base: 1, md: 3 }} gap='10px' mb='12px'>
              <Box borderWidth='1px' borderRadius='10px' p='10px'>
                <Text color={textColorSecondary} fontSize='xs'>Ká»³ lÆ°Æ¡ng</Text>
                <Text color={textColor} fontWeight='700'>
                  {selectedPayrollItem?.periodCode || "--"}
                </Text>
              </Box>
              <Box borderWidth='1px' borderRadius='10px' p='10px'>
                <Text color={textColorSecondary} fontSize='xs'>Äá»£t lÆ°Æ¡ng</Text>
                <Text color={textColor} fontWeight='700'>
                  {selectedPayrollItem?.runNo || "--"}
                </Text>
              </Box>
              <Box borderWidth='1px' borderRadius='10px' p='10px'>
                <Text color={textColorSecondary} fontSize='xs'>Má»‘c tÃ­nh</Text>
                <Text color={textColor} fontWeight='700'>
                  {formatDate(selectedPayrollBreakdown?.policyAsOfDate)}
                </Text>
              </Box>
            </SimpleGrid>

            {loadingPayrollDetail ? (
              <Flex align='center' gap='8px' mb='10px'>
                <Spinner size='sm' />
                <Text color={textColorSecondary} fontSize='sm'>Äang táº£i há»‡ sá»‘ lÆ°Æ¡ng...</Text>
              </Flex>
            ) : null}

            {payrollDetailError ? (
              <Text color='orange.400' fontSize='sm' mb='10px'>
                {payrollDetailError}
              </Text>
            ) : null}

            {selectedPayrollBreakdown?.amountsSource !== "SERVER" ? (
              <Text color='orange.400' fontSize='sm' mb='10px'>
                Backend chÆ°a tráº£ Ä‘á»§ breakdown canonical cho dÃ²ng lÆ°Æ¡ng nÃ y. Popup chá»‰ hiá»ƒn thá»‹ cÃ¡c sá»‘ server Ä‘Ã£ cung cáº¥p.
              </Text>
            ) : null}

            {selectedPayrollBreakdown ? (
              <>
                <SimpleGrid columns={{ base: 1, md: 2 }} gap='10px' mb='12px'>
                  <Box borderWidth='1px' borderRadius='10px' p='12px'>
                    <Text color={textColorSecondary} fontSize='xs'>LÆ°Æ¡ng cÆ¡ báº£n (gá»‘c)</Text>
                    <Text color={textColor} fontWeight='700'>
                      {selectedPayrollBreakdown.hasOriginalBaseSalary
                        ? formatCurrency(selectedPayrollBreakdown.originalBaseSalary)
                        : "ChÆ°a thiáº¿t láº­p"}
                    </Text>
                  </Box>
                  <Box borderWidth='1px' borderRadius='10px' p='12px'>
                    <Text color={textColorSecondary} fontSize='xs'>Há»‡ sá»‘ lÆ°Æ¡ng</Text>
                    <Text color={textColor} fontWeight='700'>
                      {Number(selectedPayrollBreakdown.coefficient || 1).toFixed(4)}
                    </Text>
                    <Text color={textColorSecondary} fontSize='xs'>
                      ThÃ¢m niÃªn: {selectedPayrollBreakdown.yearsOfService} nÄƒm ({selectedPayrollBreakdown.tenureRange})
                    </Text>
                  </Box>
                  <Box borderWidth='1px' borderRadius='10px' p='12px'>
                    <Text color={textColorSecondary} fontSize='xs'>LÆ°Æ¡ng Ã¡p dá»¥ng sau há»‡ sá»‘</Text>
                    <Text color={textColor} fontWeight='700'>
                      {formatCurrency(selectedPayrollItem.baseSalary)}
                    </Text>
                  </Box>
                  <Box borderWidth='1px' borderRadius='10px' p='12px'>
                    <Text color={textColorSecondary} fontSize='xs'>Phá»¥ cáº¥p</Text>
                    <Text color={textColor} fontWeight='700'>
                      {formatCurrency(selectedPayrollItem.allowanceAmount)}
                    </Text>
                  </Box>
                </SimpleGrid>

                <SimpleGrid columns={{ base: 1, md: 2 }} gap='10px' mb='12px'>
                  <Box borderWidth='1px' borderRadius='10px' p='12px'>
                    <Text color={textColorSecondary} fontSize='xs'>Tá»•ng BH NLÄ</Text>
                    <Text color={textColor} fontWeight='700'>{formatCurrency(selectedPayrollBreakdown.employeeInsuranceTotal)}</Text>
                    <Text color={textColorSecondary} fontSize='xs' mt='6px'>
                      Trang nÃ y khÃ´ng cÃ²n tÃ¡ch BHXH/BHYT/BHTN á»Ÿ frontend náº¿u backend chÆ°a tráº£ breakdown chi tiáº¿t.
                    </Text>
                  </Box>

                  <Box borderWidth='1px' borderRadius='10px' p='12px'>
                    <Text color={textColorSecondary} fontSize='xs'>Thuáº¿ TNCN (PIT)</Text>
                    <Text color={textColor} fontWeight='700'>{formatCurrency(selectedPayrollBreakdown.pitAmount)}</Text>
                    <Divider my='8px' />
                    <Text color={textColorSecondary} fontSize='xs'>Kháº¥u trá»« cÃ´ng thiáº¿u</Text>
                    <Text color={textColor} fontWeight='700'>{formatCurrency(selectedPayrollItem.attendanceDeduction)}</Text>
                    <Text color={textColorSecondary} fontSize='xs' mt='6px'>Táº¡m á»©ng + Khoáº£n vay + KhÃ¡c</Text>
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
                      <Text color='green.700' fontSize='xs'>Tá»•ng thu nháº­p</Text>
                      <Text color='green.800' fontWeight='800' fontSize='lg'>
                        {selectedPayrollBreakdown.totalIncome == null ? "--" : formatCurrency(selectedPayrollBreakdown.totalIncome)}
                      </Text>
                    </Box>
                    <Box>
                      <Text color='green.700' fontSize='xs'>Tá»•ng kháº¥u trá»«</Text>
                      <Text color='green.800' fontWeight='800' fontSize='lg'>
                        {selectedPayrollBreakdown.totalDeduction == null ? "--" : formatCurrency(selectedPayrollBreakdown.totalDeduction)}
                      </Text>
                    </Box>
                    <Box>
                      <Text color='green.700' fontSize='xs'>Tá»•ng tiá»n nháº­n Ä‘Æ°á»£c</Text>
                      <Text color='green.800' fontWeight='800' fontSize='lg'>
                        {selectedPayrollBreakdown.netPay == null ? "--" : formatCurrency(selectedPayrollBreakdown.netPay)}
                      </Text>
                    </Box>
                  </SimpleGrid>
                </Box>
              </>
            ) : (
              <Text color={textColorSecondary}>ChÆ°a cÃ³ dá»¯ liá»‡u chi tiáº¿t Ä‘á»ƒ hiá»ƒn thá»‹.</Text>
            )}
          </ModalBody>
          <ModalFooter>
            <Button onClick={handleClosePayrollDetail}>ÄÃ³ng</Button>
          </ModalFooter>
        </ModalContent>
      </Modal>

    </Box>
  );
}
