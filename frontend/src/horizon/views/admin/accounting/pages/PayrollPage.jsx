import React, { useEffect, useMemo, useState } from "react";
import {
  Badge,
  Box,
  Button,
  Flex,
  FormControl,
  FormLabel,
  Input,
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
  createPayrollItem,
  createPayrollPeriod,
  createPayrollRun,
  createPayslip,
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
    attendanceDeduction: "0",
    overtimeAmount: "0",
    allowanceAmount: "0",
    commissionAmount: "0",
    bonusAmount: "0",
    penaltyAmount: "0",
    advanceDeduction: "0",
    loanDeduction: "0",
    insuranceEmployeeAmount: "0",
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

function safeTime(value) {
  const ts = Date.parse(String(value || ""));
  return Number.isNaN(ts) ? 0 : ts;
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
  const [savingPeriod, setSavingPeriod] = useState(false);
  const [savingItem, setSavingItem] = useState(false);
  const [applyingSalaryCoefficient, setApplyingSalaryCoefficient] = useState(false);
  const [salaryCoefficientInfo, setSalaryCoefficientInfo] = useState(null);

  const [periodForm, setPeriodForm] = useState(() => createDefaultPeriodForm());
  const [itemForm, setItemForm] = useState(() => createDefaultItemForm());

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

    const scopedStaffs = enforcedBranchId
      ? staffList.filter((staff) => {
        const staffBranchId = toId(pick(staff, "branchId", "BranchId"));
        return !staffBranchId || staffBranchId === enforcedBranchId;
      })
      : staffList;

    const scopedBranches = enforcedBranchId
      ? branchList.filter((branch) => toId(pick(branch, "id", "Id", "branchId", "BranchId")) === enforcedBranchId)
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
  }, [enforcedBranchId]);

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

  const runRows = useMemo(() => {
    const normalized = (runs.items || []).map((run, idx) => {
      const id = toId(pick(run, "id", "Id")) || idx + 1;
      const status = String(pick(run, "status", "Status") || "DRAFT").toUpperCase();
      const payrollPeriodId = toId(pick(run, "payrollPeriodId", "PayrollPeriodId"));
      const periodCode = pick(run, "periodCode", "PeriodCode") || `KY-${payrollPeriodId || "--"}`;
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
    if (!enforcedBranchId) return normalized;
    return normalized.filter((run) => run.branchId === enforcedBranchId);
  }, [branchNameById, runs.items, enforcedBranchId]);

  const runById = useMemo(() => {
    const map = new Map();
    runRows.forEach((run) => map.set(run.id, run));
    return map;
  }, [runRows]);

  const itemRows = useMemo(() => {
    const normalized = (items.items || []).map((item, idx) => {
      const id = toId(pick(item, "id", "Id")) || idx + 1;
      const payrollRunId = toId(pick(item, "payrollRunId", "PayrollRunId"));
      const staffId = toId(pick(item, "staffId", "StaffId"));
      const { grossPay, totalDeduction, netPay } = calcItem(item);
      const runStatus = payrollRunId ? runById.get(payrollRunId)?.status || "DRAFT" : "DRAFT";
      return {
        id,
        payrollRunId,
        runNo: payrollRunId ? runById.get(payrollRunId)?.runNo || `DOT-${payrollRunId}` : "--",
        staffName: staffId ? staffNameById.get(staffId) || `Staff #${staffId}` : "--",
        status: String(pick(item, "status", "Status") || "DRAFT").toUpperCase(),
        runStatus,
        canIssuePayslip: runStatus === "APPROVED" || runStatus === "PAID",
        grossPay,
        totalDeduction,
        netPay,
        hasPayslip: payslipItemIds.has(id),
      };
    });
    if (!enforcedBranchId) return normalized;
    return normalized.filter((item) => item.payrollRunId && runById.has(item.payrollRunId));
  }, [items.items, payslipItemIds, runById, staffNameById, enforcedBranchId]);

  const filteredItems = useMemo(() => {
    return itemRows.filter((item) => {
      const passRun = runFilter === "ALL" || String(item.payrollRunId || "") === runFilter;
      const searchTarget = normalizeText([item.staffName, item.runNo, item.status].join(" "));
      const passSearch = !search || searchTarget.includes(normalizeText(search));
      return passRun && passSearch;
    });
  }, [itemRows, runFilter, search]);

  const payrollTotal = useMemo(() => sumBy(itemRows, (item) => item.netPay), [itemRows]);
  const revenueTotal = useMemo(
    () => sumBy(snapshots || [], (snapshot) => pick(snapshot, "totalRevenue", "TotalRevenue")),
    [snapshots],
  );
  const payrollRevenueRatio = revenueTotal > 0 ? (payrollTotal / revenueTotal) * 100 : 0;

  const openPeriod = useMemo(
    () =>
      (periods.items || []).find(
        (period) => String(pick(period, "status", "Status") || "").toUpperCase() === "OPEN",
      ),
    [periods.items],
  );
  const openPeriodId = useMemo(() => toId(pick(openPeriod, "id", "Id")), [openPeriod]);

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
      if (!openPeriodId || run.payrollPeriodId !== openPeriodId) return false;
      if (targetBranchId && run.branchId !== targetBranchId) return false;
      return true;
    });
    if (!drafts.length) return null;
    return [...drafts].sort((a, b) => {
      const byTime = safeTime(b.createdAt) - safeTime(a.createdAt);
      if (byTime !== 0) return byTime;
      return Number(b.id) - Number(a.id);
    })[0];
  }, [runRows, openPeriodId, targetBranchId]);

  const autoRunLabel = useMemo(() => {
    if (!openPeriodId) return "Chưa có kỳ lương đang mở";
    if (activeDraftRun) return `${activeDraftRun.runNo} · ${activeDraftRun.periodCode}`;
    const periodCode = pick(openPeriod, "periodCode", "PeriodCode") || `KY-${openPeriodId}`;
    return `Sẽ tự tạo đợt Nháp cho kỳ ${periodCode}`;
  }, [activeDraftRun, openPeriod, openPeriodId]);

  const itemPreview = useMemo(() => calcItem(itemForm), [itemForm]);

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
        baseSalary: toNumber(itemForm.baseSalary),
        attendanceDeduction: toNumber(itemForm.attendanceDeduction),
        overtimeAmount: toNumber(itemForm.overtimeAmount),
        allowanceAmount: toNumber(itemForm.allowanceAmount),
        commissionAmount: toNumber(itemForm.commissionAmount),
        bonusAmount: toNumber(itemForm.bonusAmount),
        penaltyAmount: toNumber(itemForm.penaltyAmount),
        advanceDeduction: toNumber(itemForm.advanceDeduction),
        loanDeduction: toNumber(itemForm.loanDeduction),
        insuranceEmployeeAmount: toNumber(itemForm.insuranceEmployeeAmount),
        pitAmount: toNumber(itemForm.pitAmount),
        otherDeduction: toNumber(itemForm.otherDeduction),
        netPay: itemPreview.netPay,
        status: "DRAFT",
      });
      toast({ status: "success", title: "Đã thêm dòng lương." });
      setItemForm(createDefaultItemForm());
      setSalaryCoefficientInfo(null);
      await reloadItemsAndPayslips();
    } catch (err) {
      setError(toFriendlyError(err, "Thêm dòng lương thất bại."));
    } finally {
      setSavingItem(false);
    }
  }

  async function handleApplySalaryCoefficient() {
    const staffId = toId(itemForm.staffId);
    if (!staffId) {
      setError("Vui lòng chọn nhân sự trước khi áp hệ số lương.");
      return;
    }

    const asOfDateRaw =
      pick(openPeriod, "endDate", "EndDate") ||
      pick(openPeriod, "payrollDate", "PayrollDate") ||
      new Date().toISOString();
    const asOfDate = toDateInputValue(asOfDateRaw);

    setApplyingSalaryCoefficient(true);
    setError("");
    try {
      const data = await getStaffSalaryCoefficient(staffId, asOfDate);
      const baseSalary = toNumber(pick(data, "baseSalary", "BaseSalary"));
      const coefficient = toNumber(pick(data, "coefficient", "Coefficient")) || 1;
      const calculatedSalary = toNumber(pick(data, "calculatedSalary", "CalculatedSalary"));
      const yearsOfService = toNumber(pick(data, "yearsOfService", "YearsOfService"));
      const appliedSalary = calculatedSalary > 0 ? calculatedSalary : Math.round(baseSalary * coefficient);

      if (!appliedSalary || appliedSalary <= 0) {
        throw new Error("Nhân sự chưa có mức lương cơ bản để áp hệ số.");
      }

      setItemForm((prev) => ({ ...prev, baseSalary: String(Math.round(appliedSalary)) }));
      setSalaryCoefficientInfo({
        coefficient,
        yearsOfService,
        baseSalary,
        calculatedSalary: appliedSalary,
        asOfDate: pick(data, "asOfDate", "AsOfDate") || asOfDate,
        policyReference: pick(data, "policyReference", "PolicyReference") || "",
      });

      toast({
        status: "success",
        title: "Đã áp hệ số lương theo thâm niên.",
        description: `Hệ số ${coefficient.toFixed(4)} · Lương áp dụng ${formatCurrency(appliedSalary)}`,
      });
    } catch (err) {
      setSalaryCoefficientInfo(null);
      setError(toFriendlyError(err, "Không áp được hệ số lương."));
    } finally {
      setApplyingSalaryCoefficient(false);
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
      <Box mb='20px'>
        <Text color={textColor} fontSize='2xl' fontWeight='700'>Tiền lương</Text>
        <Text color={textColorSecondary} fontSize='sm'>
          Theo dõi kỳ lương, đợt tính lương, chi tiết lương và phát hành phiếu lương cho từng nhân sự.
        </Text>
      </Box>

      {error ? (
        <Card p='16px' mb='20px'>
          <Text color='red.400' fontWeight='600'>{error}</Text>
        </Card>
      ) : null}

      <SimpleGrid columns={{ base: 1, md: 2, xl: 5 }} gap='20px' mb='20px'>
        <MiniStatistics
          name='Kỳ đang mở'
          value={String(pick(openPeriod, "periodCode", "PeriodCode") || "Chưa có")}
        />
        <MiniStatistics name='Đợt tính lương' value={String(runRows.length)} />
        <MiniStatistics name='Dòng lương' value={String(itemRows.length)} />
        <MiniStatistics name='Tổng lương thực nhận' value={formatCurrency(payrollTotal)} />
        <MiniStatistics name='Lương / Doanh thu' value={`${payrollRevenueRatio.toFixed(2)}%`} />
      </SimpleGrid>

      <Card p='16px' mb='20px'>
        <Flex wrap='wrap' gap='10px' align='center'>
          <Badge colorScheme={periods.available ? "green" : "orange"}>API Kỳ lương: {periods.available ? "OK" : "Chưa có"}</Badge>
          <Badge colorScheme={runs.available ? "green" : "orange"}>API Đợt lương: {runs.available ? "OK" : "Chưa có"}</Badge>
          <Badge colorScheme={items.available ? "green" : "orange"}>API Dòng lương: {items.available ? "OK" : "Chưa có"}</Badge>
          <Badge colorScheme={payslips.available ? "green" : "orange"}>API Phiếu lương: {payslips.available ? "OK" : "Chưa có"}</Badge>
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
              Lưu ý: Ngày trả lương phải bằng hoặc sau ngày kết thúc kỳ.
            </Text>
            <Button size='sm' colorScheme='blue' onClick={handleCreatePeriod} isLoading={savingPeriod}>
              Tạo kỳ lương
            </Button>
          </SimpleGrid>
        </Card>

        <Card p='16px'>
          <Text color={textColor} fontWeight='700' mb='10px'>Thêm dòng lương</Text>
          <SimpleGrid columns={1} gap='10px'>
            <SimpleGrid columns={2} gap='10px'>
              <FormControl>
                <FormLabel fontSize='xs' mb='4px'>Đợt tính lương (tự động)</FormLabel>
                <Input
                  value={autoRunLabel}
                  isReadOnly
                />
              </FormControl>
              <FormControl>
                <FormLabel fontSize='xs' mb='4px'>Nhân sự</FormLabel>
                <Select
                  value={itemForm.staffId}
                  onChange={(e) => {
                    const newStaffId = e.target.value;
                    setItemForm((prev) => ({ ...prev, staffId: newStaffId }));
                    setSalaryCoefficientInfo(null);
                    
                    if (newStaffId) {
                      const asOfDateRaw =
                        pick(openPeriod, "endDate", "EndDate") ||
                        pick(openPeriod, "payrollDate", "PayrollDate") ||
                        new Date().toISOString();
                      const asOfDate = toDateInputValue(asOfDateRaw);
                      
                      getStaffBaseSalary(newStaffId, asOfDate)
                        .then((data) => {
                          const bs = toNumber(pick(data, "baseSalary", "BaseSalary"));
                          if (bs > 0) {
                            setItemForm((prev) => ({ ...prev, baseSalary: String(Math.round(bs)) }));
                          }
                        })
                        .catch(() => {});
                    } else {
                      setItemForm((prev) => ({ ...prev, baseSalary: "" }));
                    }
                  }}>
                  <option value=''>Chọn nhân sự</option>
                  {(staffs || []).map((staff) => {
                    const id = toId(pick(staff, "id", "Id"));
                    if (!id) return null;
                    return (
                      <option value={id} key={id}>
                        {pick(staff, "fullName", "FullName") || `Staff #${id}`}
                      </option>
                    );
                  })}
                </Select>
              </FormControl>
            </SimpleGrid>
            <Flex justify='space-between' align='center' wrap='wrap' gap='8px'>
              <Text color={textColorSecondary} fontSize='xs'>
                Áp dụng hệ số theo thâm niên từ hợp đồng mẫu (mốc 15/20/25/30 năm).
              </Text>
              <Button
                size='xs'
                colorScheme='teal'
                variant='outline'
                onClick={handleApplySalaryCoefficient}
                isLoading={applyingSalaryCoefficient}
                isDisabled={!toId(itemForm.staffId)}>
                Áp hệ số lương
              </Button>
            </Flex>
            {salaryCoefficientInfo ? (
              <Text color='teal.500' fontSize='xs'>
                Thâm niên: {salaryCoefficientInfo.yearsOfService} năm · Hệ số: {Number(salaryCoefficientInfo.coefficient || 1).toFixed(4)}
                {" · "}Lương gốc: {formatCurrency(salaryCoefficientInfo.baseSalary)} · Lương áp dụng:{" "}
                {formatCurrency(salaryCoefficientInfo.calculatedSalary)} · Mốc tính: {formatDate(salaryCoefficientInfo.asOfDate)}
              </Text>
            ) : null}
            <SimpleGrid columns={2} gap='10px'>
              <Input
                type='number'
                placeholder='Lương cơ bản'
                value={itemForm.baseSalary}
                onChange={(e) => {
                  setItemForm((prev) => ({ ...prev, baseSalary: e.target.value }));
                  if (salaryCoefficientInfo) setSalaryCoefficientInfo(null);
                }}
              />
              <Input type='number' placeholder='Phụ cấp' value={itemForm.allowanceAmount} onChange={(e) => setItemForm((prev) => ({ ...prev, allowanceAmount: e.target.value }))} />
              <Input type='number' placeholder='Tiền tăng ca' value={itemForm.overtimeAmount} onChange={(e) => setItemForm((prev) => ({ ...prev, overtimeAmount: e.target.value }))} />
              <Input type='number' placeholder='Hoa hồng' value={itemForm.commissionAmount} onChange={(e) => setItemForm((prev) => ({ ...prev, commissionAmount: e.target.value }))} />
              <Input type='number' placeholder='Thưởng' value={itemForm.bonusAmount} onChange={(e) => setItemForm((prev) => ({ ...prev, bonusAmount: e.target.value }))} />
              <Input type='number' placeholder='Khấu trừ công thiếu' value={itemForm.attendanceDeduction} onChange={(e) => setItemForm((prev) => ({ ...prev, attendanceDeduction: e.target.value }))} />
              <Input type='number' placeholder='Phạt' value={itemForm.penaltyAmount} onChange={(e) => setItemForm((prev) => ({ ...prev, penaltyAmount: e.target.value }))} />
              <Input type='number' placeholder='Tạm ứng' value={itemForm.advanceDeduction} onChange={(e) => setItemForm((prev) => ({ ...prev, advanceDeduction: e.target.value }))} />
              <Input type='number' placeholder='Khoản vay' value={itemForm.loanDeduction} onChange={(e) => setItemForm((prev) => ({ ...prev, loanDeduction: e.target.value }))} />
              <Input type='number' placeholder='Bảo hiểm NLĐ' value={itemForm.insuranceEmployeeAmount} onChange={(e) => setItemForm((prev) => ({ ...prev, insuranceEmployeeAmount: e.target.value }))} />
              <Input type='number' placeholder='PIT' value={itemForm.pitAmount} onChange={(e) => setItemForm((prev) => ({ ...prev, pitAmount: e.target.value }))} />
              <Input type='number' placeholder='Khấu trừ khác' value={itemForm.otherDeduction} onChange={(e) => setItemForm((prev) => ({ ...prev, otherDeduction: e.target.value }))} />
            </SimpleGrid>
            <Text color={textColorSecondary} fontSize='xs'>
              Tổng thu nhập: {formatCurrency(itemPreview.grossPay)} · Tổng khấu trừ: {formatCurrency(itemPreview.totalDeduction)} · Thực nhận: {formatCurrency(itemPreview.netPay)}
            </Text>
            {!openPeriodId ? (
              <Text color='orange.400' fontSize='xs'>Cần có kỳ lương đang mở để thêm dòng lương.</Text>
            ) : null}
            <Button size='sm' colorScheme='blue' onClick={handleCreateItem} isLoading={savingItem} isDisabled={!openPeriodId}>
              Thêm dòng lương
            </Button>
          </SimpleGrid>
        </Card>
      </SimpleGrid>

      <SimpleGrid columns={{ base: 1, xl: 2 }} gap='20px' mb='20px'>
        <Card p='20px'>
          <Text color={textColor} fontWeight='700' mb='10px'>Danh sách kỳ lương</Text>
          {(periods.items || []).length ? (
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
                {(periods.items || []).slice(0, 8).map((period, idx) => {
                  const status = String(pick(period, "status", "Status") || "").toUpperCase();
                  return (
                    <Tr key={pick(period, "id", "Id") || idx}>
                      <Td color={textColor} fontWeight='600'>
                        {pick(period, "periodCode", "PeriodCode") || `PERIOD-${idx + 1}`}
                      </Td>
                      <Td color={textColorSecondary}>{formatDate(pick(period, "startDate", "StartDate"))}</Td>
                      <Td color={textColorSecondary}>{formatDate(pick(period, "endDate", "EndDate"))}</Td>
                      <Td color={textColorSecondary}>{formatDate(pick(period, "payrollDate", "PayrollDate"))}</Td>
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
            <Text color={textColor} fontWeight='700'>Danh sách đợt tính lương</Text>
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
                  <Th>Mã đợt</Th>
                  <Th>Kỳ</Th>
                  <Th>Chi nhánh</Th>
                  <Th>Ngày tạo</Th>
                  <Th>Status</Th>
                  <Th textAlign='right'>Thao tác</Th>
                </Tr>
              </Thead>
              <Tbody>
                {runRows.slice(0, 10).map((run) => (
                  <Tr key={run.id}>
                    <Td color={textColor} fontWeight='600'>{run.runNo}</Td>
                    <Td color={textColorSecondary}>{run.periodCode}</Td>
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
            <Text color={textColorSecondary}>Chưa có đợt tính lương.</Text>
          )}
        </Card>
      </SimpleGrid>

      <Card p='20px' mb='20px'>
        <Flex wrap='wrap' gap='10px' mb='10px'>
          <Select value={runFilter} onChange={(e) => setRunFilter(e.target.value)} maxW='220px'>
            <option value='ALL'>Tất cả đợt tính lương</option>
            {runRows.map((run) => (
              <option value={String(run.id)} key={run.id}>{run.runNo}</option>
            ))}
          </Select>
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder='Tìm theo nhân sự / đợt / trạng thái'
            maxW='320px'
          />
        </Flex>

        {filteredItems.length ? (
          <Table size='sm'>
            <Thead>
              <Tr>
                <Th>Nhân sự</Th>
                <Th>Mã đợt</Th>
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
                  <Td color={textColor} fontWeight='600'>{item.staffName}</Td>
                  <Td color={textColorSecondary}>{item.runNo}</Td>
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

      <Card p='20px'>
        <Text color={textColor} fontWeight='700' mb='10px'>Lịch sử phát hành phiếu lương</Text>
        {(payslips.items || []).length ? (
          <Table size='sm'>
            <Thead>
              <Tr>
                <Th>Mã phiếu</Th>
                <Th>Dòng lương</Th>
                <Th>Thời gian phát hành</Th>
                <Th>Người phát hành</Th>
              </Tr>
            </Thead>
            <Tbody>
              {(payslips.items || []).slice(0, 12).map((slip, idx) => (
                <Tr key={pick(slip, "id", "Id") || idx}>
                  <Td color={textColor} fontWeight='600'>{pick(slip, "slipNo", "SlipNo") || `PS-${idx + 1}`}</Td>
                  <Td color={textColorSecondary}>#{pick(slip, "payrollItemId", "PayrollItemId") || "--"}</Td>
                  <Td color={textColorSecondary}>{formatDateTime(pick(slip, "issuedAt", "IssuedAt"))}</Td>
                  <Td color={textColorSecondary}>#{pick(slip, "issuedBy", "IssuedBy") || "--"}</Td>
                </Tr>
              ))}
            </Tbody>
          </Table>
        ) : (
          <Text color={textColorSecondary}>Chưa có phiếu lương nào được phát hành.</Text>
        )}
      </Card>
    </Box>
  );
}
