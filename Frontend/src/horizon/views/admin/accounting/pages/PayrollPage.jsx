import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  Box,
  Flex,
  Spinner,
  Text,
  useColorModeValue,
  useToast,
} from "@chakra-ui/react";
import Card from "components/card/Card";
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
import {
  PAYROLL_EXCLUDED_STAFF_STATUSES,
  createDefaultPeriodForm,
  derivePayrollAmounts,
  normalizePeriodCode,
  parseDateValue,
  pick,
  resolveEffectivePayrollItemStatus,
  resolveEffectivePeriodStatus,
  safeTime,
  statusLabel,
  tenureRangeLabel,
  toDateInputValue,
  toFriendlyError,
  toId,
} from "./payrollPageHelpers";
import {
  InitialSalaryDecisionsCard,
  MissingBaseSalaryCard,
  PayrollSummaryAndPeriodControls,
} from "./PayrollTopPanels";
import { PayrollItemsCard, PayrollPeriodsAndRunsGrid } from "./PayrollTablesSection";
import { InitialSalaryRequestModal, PayrollDetailModal } from "./PayrollModals";

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
  const [decisionPage, setDecisionPage] = useState(1);
  const [decisionPageSize, setDecisionPageSize] = useState(20);
  const [itemPage, setItemPage] = useState(1);
  const [itemPageSize, setItemPageSize] = useState(20);
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

  const pagedItems = useMemo(() => {
    const start = (itemPage - 1) * itemPageSize;
    return filteredItems.slice(start, start + itemPageSize);
  }, [filteredItems, itemPage, itemPageSize]);

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
        const staffName = pick(row, "staffName", "StaffName", "staff_name") || "nhân sự";
        const rejectReason = pick(row, "rejectReason", "RejectReason", "reject_reason");
        if (status === "APPROVED") {
          toast({
            status: "success",
            title: "Yêu cầu lương đã được Admin duyệt",
            description: String(staffName),
          });
        } else {
          toast({
            status: "warning",
            title: "Yêu cầu lương bị Admin từ chối",
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

  const pagedDecisionRows = useMemo(() => {
    const start = (decisionPage - 1) * decisionPageSize;
    return initialSalaryDecisionRows.slice(start, start + decisionPageSize);
  }, [initialSalaryDecisionRows, decisionPage, decisionPageSize]);

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

      <PayrollSummaryAndPeriodControls
        textColor={textColor}
        textColorSecondary={textColorSecondary}
        openPeriod={openPeriod}
        runRows={runRows}
        itemRows={itemRows}
        officialPayrollTotal={officialPayrollTotal}
        missingPayrollFactsCount={missingPayrollFactsCount}
        periods={periods}
        runs={runs}
        items={items}
        payslips={payslips}
        staffs={staffs}
        periodForm={periodForm}
        setPeriodForm={setPeriodForm}
        updatePeriodEndDate={updatePeriodEndDate}
        handleCreatePeriod={handleCreatePeriod}
        savingPeriod={savingPeriod}
        autoRunLabel={autoRunLabel}
        targetBranchId={targetBranchId}
        selectedPeriodIdForBatch={selectedPeriodIdForBatch}
        selectedPeriodStatusForBatch={selectedPeriodStatusForBatch}
        handleCalculateAllStaffPayroll={handleCalculateAllStaffPayroll}
        calculatingBatchPayroll={calculatingBatchPayroll}
      />

      <MissingBaseSalaryCard
        textColor={textColor}
        textColorSecondary={textColorSecondary}
        missingBaseSalaryRows={missingBaseSalaryRows}
        loading={loading}
        loadingMissingBaseSalary={loadingMissingBaseSalary}
        handleReloadMissingBaseSalary={handleReloadMissingBaseSalary}
        handleOpenInitialSalaryRequest={handleOpenInitialSalaryRequest}
      />

      <InitialSalaryDecisionsCard
        title='Kết quả duyệt lương khởi tạo'
        textColor={textColor}
        textColorSecondary={textColorSecondary}
        initialSalaryDecisionSummary={initialSalaryDecisionSummary}
        pollInitialSalaryRequestUpdates={pollInitialSalaryRequestUpdates}
        initialSalaryDecisionRows={initialSalaryDecisionRows}
        pagedDecisionRows={pagedDecisionRows}
        decisionPage={decisionPage}
        decisionPageSize={decisionPageSize}
        setDecisionPage={setDecisionPage}
        setDecisionPageSize={setDecisionPageSize}
      />

      <PayrollPeriodsAndRunsGrid
        textColor={textColor}
        textColorSecondary={textColorSecondary}
        periodRows={periodRows}
        runSummary={runSummary}
        runRows={runRows}
        busyRunId={busyRunId}
        handleApprove={handleApprove}
        handleMarkPaid={handleMarkPaid}
        handleCancelRun={handleCancelRun}
      />

      <PayrollItemsCard
        textColor={textColor}
        textColorSecondary={textColorSecondary}
        runFilter={runFilter}
        setRunFilter={setRunFilter}
        periodFilterOptions={periodFilterOptions}
        search={search}
        setSearch={setSearch}
        handleIssueAllPayslips={handleIssueAllPayslips}
        issuingAllPayslips={issuingAllPayslips}
        issuablePayslipCount={issuablePayslipCount}
        filteredItems={filteredItems}
        pagedItems={pagedItems}
        handleOpenPayrollDetail={handleOpenPayrollDetail}
        busyItemId={busyItemId}
        handleIssuePayslip={handleIssuePayslip}
        itemPage={itemPage}
        itemPageSize={itemPageSize}
        setItemPage={setItemPage}
        setItemPageSize={setItemPageSize}
      />

      <PayrollDetailModal
        textColor={textColor}
        textColorSecondary={textColorSecondary}
        selectedPayrollItem={selectedPayrollItem}
        handleClosePayrollDetail={handleClosePayrollDetail}
        selectedPayrollBreakdown={selectedPayrollBreakdown}
        loadingPayrollDetail={loadingPayrollDetail}
        payrollDetailError={payrollDetailError}
      />

      <InitialSalaryRequestModal
        initialSalaryTarget={initialSalaryTarget}
        handleCloseInitialSalaryRequest={handleCloseInitialSalaryRequest}
        salaryCheckAsOfDate={salaryCheckAsOfDate}
        initialSalaryForm={initialSalaryForm}
        setInitialSalaryForm={setInitialSalaryForm}
        handleSubmitInitialSalaryRequest={handleSubmitInitialSalaryRequest}
        sendingInitialSalaryRequest={sendingInitialSalaryRequest}
      />

    </Box>
  );
}



