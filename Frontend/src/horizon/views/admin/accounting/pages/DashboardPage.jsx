import React, { useEffect, useMemo, useState } from "react";
import {
  Badge,
  Box,
  Button,
  Flex,
  Icon,
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
  useColorModeValue
} from "@chakra-ui/react";
import { useNavigate } from "react-router-dom";
import MiniStatistics from "components/card/MiniStatistics";
import Card from "components/card/Card";
import IconBox from "components/icons/IconBox";
import { useBranch } from "../../../../../context/BranchContext";
import {
  MdAccountBalanceWallet,
  MdAttachMoney,
  MdBarChart,
  MdChecklist,
  MdErrorOutline,
  MdFactCheck,
  MdPendingActions,
  MdReceiptLong,
  MdTrendingDown,
  MdTrendingUp,
  MdWarning
} from "react-icons/md";
import {
  getAccountsPayable,
  getAccountingPeriods,
  getBranches,
  getFinanceApprovalRequests,
  getInvoices,
  getPayments,
  getPayrollRuns,
  getRevenueSnapshots
} from "../api/accountingApi";
import {
  countCompletedInflowWithoutInvoice,
  formatCurrency,
  formatDate,
  formatDateTime,
  sumCompletedCollectedPayments,
  sumCompletedPaymentByTypes,
  sumBy,
  toMonthKey,
  toNumber
} from "../utils/accountingHelpers";

function fieldOf(item, ...candidates) {
  for (const key of candidates) {
    if (item && item[key] !== undefined && item[key] !== null) return item[key];
  }
  return null;
}

function snapshotDate(item) {
  return fieldOf(item, "snapshotDate", "SnapshotDate", "createdAt", "CreatedAt");
}

function toTs(value) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? 0 : date.getTime();
}

function normalizeStatus(value) {
  return String(value || "").trim().toUpperCase();
}

function isInvoiceClosed(status) {
  const normalized = normalizeStatus(status);
  return ["PAID", "CANCELED", "CANCELED", "VOID", "WRITTEN_OFF", "CLOSED"].includes(normalized);
}

function invoiceOutstanding(item) {
  const direct = toNumber(
    fieldOf(
      item,
      "outstandingAmount",
      "OutstandingAmount",
      "remainingAmount",
      "RemainingAmount",
      "unpaidAmount",
      "UnpaidAmount",
      "amountDue",
      "AmountDue"
    )
  );
  if (direct > 0) return direct;

  const total = toNumber(fieldOf(item, "totalAmount", "TotalAmount", "grandTotal", "GrandTotal", "amount", "Amount"));
  const paid = toNumber(fieldOf(item, "paidAmount", "PaidAmount", "collectedAmount", "CollectedAmount"));
  return Math.max(0, total - paid);
}

function downloadCsv(filename, rows) {
  const escaped = rows.map((row) =>
    row
      .map((cell) => {
        const value = String(cell ?? "");
        return `"${value.replace(/"/g, "\"\"")}"`;
      })
      .join(",")
  );
  const content = ["\uFEFF", ...escaped].join("\n");
  const blob = new Blob([content], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

export default function DashboardPage() {
  const navigate = useNavigate();
  const { selectedBranchId, isBranchLocked } = useBranch();
  const brandColor = useColorModeValue("brand.500", "white");
  const boxBg = useColorModeValue("secondaryGray.300", "whiteAlpha.100");
  const textColor = useColorModeValue("secondaryGray.900", "white");
  const textColorSecondary = useColorModeValue("secondaryGray.600", "secondaryGray.300");
  const borderColor = useColorModeValue("secondaryGray.200", "whiteAlpha.100");
  const panelBg = useColorModeValue("white", "navy.700");
  const subPanelBg = useColorModeValue("gray.50", "navy.800");

  const enforcedBranchId = isBranchLocked && selectedBranchId ? String(selectedBranchId) : "";

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState("");
  const [branchFilter, setBranchFilter] = useState("all");
  const [periodMode, setPeriodMode] = useState("month");
  const [selectedMonth, setSelectedMonth] = useState("");

  const [branches, setBranches] = useState([]);
  const [snapshots, setSnapshots] = useState([]);
  const [invoices, setInvoices] = useState([]);
  const [payments, setPayments] = useState([]);
  const [approvals, setApprovals] = useState([]);
  const [accountsPayable, setAccountsPayable] = useState([]);
  const [accountingPeriods, setAccountingPeriods] = useState([]);
  const [payrollRuns, setPayrollRuns] = useState([]);
  const [selectedKpiKey, setSelectedKpiKey] = useState("revenue");

  useEffect(() => {
    if (!enforcedBranchId) return;
    setBranchFilter((prev) => (prev === enforcedBranchId ? prev : enforcedBranchId));
  }, [enforcedBranchId]);

  useEffect(() => {
    let mounted = true;

    async function load() {
      setLoading(true);
      setLoadError("");
      try {
        const [
          branchData,
          snapshotData,
          invoiceData,
          paymentData,
          approvalData,
          apData,
          periodData,
          payrollData
        ] = await Promise.all([
          getBranches(),
          getRevenueSnapshots(),
          getInvoices(),
          getPayments(),
          getFinanceApprovalRequests(),
          getAccountsPayable(),
          getAccountingPeriods(),
          getPayrollRuns()
        ]);

        if (!mounted) return;

        setBranches(Array.isArray(branchData) ? branchData : []);
        setSnapshots(Array.isArray(snapshotData) ? snapshotData : []);
        setInvoices(Array.isArray(invoiceData) ? invoiceData : []);
        setPayments(Array.isArray(paymentData) ? paymentData : []);
        setApprovals(approvalData?.items || []);
        setAccountsPayable(apData?.items || []);
        setAccountingPeriods(periodData?.items || []);
        setPayrollRuns(payrollData?.items || []);

        const monthKeys = [...new Set((snapshotData || []).map((item) => toMonthKey(snapshotDate(item))).filter(Boolean))]
          .sort((a, b) => b.localeCompare(a));
        setSelectedMonth(monthKeys[0] || "");
      } catch (error) {
        if (!mounted) return;
        setLoadError(error?.message || "Không tải được dữ liệu dashboard kế toán.");
      } finally {
        if (mounted) setLoading(false);
      }
    }

    load();
    return () => {
      mounted = false;
    };
  }, []);

  const scopedBranches = useMemo(() => {
    if (!enforcedBranchId) return branches || [];
    return (branches || []).filter((item) => String(fieldOf(item, "id", "Id")) === enforcedBranchId);
  }, [branches, enforcedBranchId]);

  const branchOptions = useMemo(
    () =>
      scopedBranches.map((item) => ({
        id: String(fieldOf(item, "id", "Id")),
        name: fieldOf(item, "name", "Name") || `Branch #${fieldOf(item, "id", "Id")}`
      })),
    [scopedBranches]
  );

  const monthOptions = useMemo(
    () =>
      [...new Set((snapshots || []).map((item) => toMonthKey(snapshotDate(item))).filter(Boolean))]
        .sort((a, b) => b.localeCompare(a)),
    [snapshots]
  );

  const effectiveBranchFilter = enforcedBranchId || branchFilter;

  const filteredSnapshots = useMemo(() => {
    const byBranch = (snapshots || []).filter((item) => {
      if (effectiveBranchFilter === "all") return true;
      return String(fieldOf(item, "branchId", "BranchId")) === String(effectiveBranchFilter);
    });

    if (!byBranch.length) return [];

    if (periodMode === "day") {
      const latestTs = Math.max(...byBranch.map((item) => toTs(snapshotDate(item))));
      return byBranch.filter((item) => toTs(snapshotDate(item)) === latestTs);
    }

    if (!selectedMonth) return byBranch;
    return byBranch.filter((item) => toMonthKey(snapshotDate(item)) === selectedMonth);
  }, [snapshots, effectiveBranchFilter, periodMode, selectedMonth]);

  const filteredPayments = useMemo(
    () =>
      (payments || []).filter((item) => {
        if (effectiveBranchFilter === "all") return true;
        const paymentBranchId = fieldOf(item, "branchId", "BranchId");
        if (paymentBranchId === null || paymentBranchId === undefined) return false;
        return String(paymentBranchId) === String(effectiveBranchFilter);
      }),
    [payments, effectiveBranchFilter]
  );

  const paymentStats = useMemo(() => {
    const totalCollected = sumCompletedCollectedPayments(filteredPayments);
    const inflowNoInvoice = countCompletedInflowWithoutInvoice(filteredPayments);

    const duplicates = filteredPayments.reduce((acc, item) => {
      const code = String(fieldOf(item, "transactionCode", "TransactionCode") || "").trim();
      if (!code) return acc;
      acc[code] = (acc[code] || 0) + 1;
      return acc;
    }, {});

    const duplicateCount = Object.values(duplicates).filter((count) => count > 1).length;
    const refunds = sumCompletedPaymentByTypes(filteredPayments, ["REFUND"]);
    const reversals = sumCompletedPaymentByTypes(filteredPayments, ["REVERSAL"]);

    return {
      totalCollected,
      inflowNoInvoice,
      duplicateCount,
      refunds,
      reversals
    };
  }, [filteredPayments]);

  const filteredInvoices = useMemo(
    () =>
      (invoices || []).filter((item) => {
        if (effectiveBranchFilter === "all") return true;
        const branchId = fieldOf(item, "branchId", "BranchId");
        if (branchId === null || branchId === undefined) return false;
        return String(branchId) === String(effectiveBranchFilter);
      }),
    [invoices, effectiveBranchFilter]
  );

  const invoiceSummary = useMemo(() => {
    const now = new Date();
    const openInvoices = filteredInvoices
      .map((item) => {
        const status = normalizeStatus(fieldOf(item, "status", "Status"));
        const outstanding = invoiceOutstanding(item);
        const dueDateRaw = fieldOf(item, "dueDate", "DueDate");
        const dueDate = dueDateRaw ? new Date(dueDateRaw) : null;
        const overdue = dueDate ? dueDate.getTime() < now.getTime() && outstanding > 0 : false;
        const dueSoon = dueDate
          ? dueDate.getTime() >= now.getTime() && dueDate.getTime() - now.getTime() <= 7 * 24 * 60 * 60 * 1000
          : false;
        return {
          ...item,
          status,
          outstanding,
          dueDate,
          overdue,
          dueSoon
        };
      })
      .filter((item) => !isInvoiceClosed(item.status) && item.outstanding > 0);

    const watchList = [...openInvoices]
      .sort((a, b) => {
        if (a.overdue && !b.overdue) return -1;
        if (!a.overdue && b.overdue) return 1;
        if (a.dueSoon && !b.dueSoon) return -1;
        if (!a.dueSoon && b.dueSoon) return 1;
        return b.outstanding - a.outstanding;
      })
      .slice(0, 10);

    return {
      openCount: openInvoices.length,
      overdueCount: openInvoices.filter((x) => x.overdue).length,
      dueSoonCount: openInvoices.filter((x) => x.dueSoon).length,
      totalOutstanding: sumBy(openInvoices, (x) => x.outstanding),
      rows: watchList
    };
  }, [filteredInvoices]);

  const financeSnapshot = useMemo(() => {
    const totalRevenue = sumBy(filteredSnapshots, (item) => fieldOf(item, "totalRevenue", "TotalRevenue"));
    const totalCollected = sumBy(filteredSnapshots, (item) => fieldOf(item, "totalCollected", "TotalCollected"));
    const totalDebt = sumBy(filteredSnapshots, (item) => fieldOf(item, "totalDebt", "TotalDebt"));
    const totalExpense = sumBy(filteredSnapshots, (item) => fieldOf(item, "totalExpense", "TotalExpense"));
    const netProfit = sumBy(filteredSnapshots, (item) => fieldOf(item, "netProfit", "NetProfit"));
    const cashFlow = sumBy(filteredSnapshots, (item) => fieldOf(item, "netCashFlow", "NetCashFlow"));

    const grossMargin = totalRevenue > 0 ? ((totalRevenue - totalExpense) / totalRevenue) * 100 : 0;
    const netMargin = totalRevenue > 0 ? (netProfit / totalRevenue) * 100 : 0;

    return {
      totalRevenue,
      totalCollected,
      totalDebt,
      totalExpense,
      netProfit,
      cashFlow,
      grossMargin,
      netMargin
    };
  }, [filteredSnapshots]);

  const displayedCollected = useMemo(() => {
    if (filteredSnapshots.length) return financeSnapshot.totalCollected;
    return paymentStats.totalCollected;
  }, [filteredSnapshots.length, financeSnapshot.totalCollected, paymentStats.totalCollected]);

  const approvalsPending = useMemo(
    () =>
      (approvals || []).filter((item) => {
        const status = normalizeStatus(fieldOf(item, "status", "Status"));
        return status === "PENDING" || status === "WAITING";
      }),
    [approvals]
  );

  const apDueSummary = useMemo(() => {
    const now = new Date();
    const rows = (accountsPayable || [])
      .map((item) => {
        const dueDateValue = fieldOf(item, "dueDate", "DueDate");
        const dueDate = dueDateValue ? new Date(dueDateValue) : null;
        const outstanding = toNumber(fieldOf(item, "outstandingAmount", "OutstandingAmount", "outstanding", "Outstanding"));
        const overdue = dueDate ? dueDate.getTime() < now.getTime() && outstanding > 0 : false;
        const dueSoon = dueDate
          ? dueDate.getTime() >= now.getTime() && dueDate.getTime() - now.getTime() <= 7 * 24 * 60 * 60 * 1000
          : false;
        return { ...item, overdue, dueSoon, outstanding, dueDate };
      })
      .filter((item) => item.outstanding > 0)
      .sort((a, b) => {
        if (a.overdue && !b.overdue) return -1;
        if (!a.overdue && b.overdue) return 1;
        if (a.dueSoon && !b.dueSoon) return -1;
        if (!a.dueSoon && b.dueSoon) return 1;
        return b.outstanding - a.outstanding;
      });

    return {
      totalOutstanding: sumBy(rows, (item) => item.outstanding),
      overdueAmount: sumBy(rows.filter((item) => item.overdue), (item) => item.outstanding),
      dueSoonAmount: sumBy(rows.filter((item) => item.dueSoon), (item) => item.outstanding),
      rows: rows.slice(0, 10)
    };
  }, [accountsPayable]);

  const openPeriodLabel = useMemo(() => {
    const openPeriod = (accountingPeriods || []).find((item) => {
      const status = normalizeStatus(fieldOf(item, "status", "Status"));
      return status.includes("OPEN");
    });
    if (!openPeriod) return "Chưa tích hợp";

    const periodCode = fieldOf(openPeriod, "periodCode", "PeriodCode", "name", "Name");
    const fromDate = fieldOf(openPeriod, "startDate", "StartDate");
    const toDate = fieldOf(openPeriod, "endDate", "EndDate");
    if (periodCode) return String(periodCode);
    return `${formatDate(fromDate)} - ${formatDate(toDate)}`;
  }, [accountingPeriods]);

  const currentPayroll = useMemo(() => {
    if (!payrollRuns.length) return null;
    const sorted = [...payrollRuns].sort(
      (a, b) => toTs(fieldOf(b, "createdAt", "CreatedAt", "runDate", "RunDate")) - toTs(fieldOf(a, "createdAt", "CreatedAt", "runDate", "RunDate"))
    );
    return sorted[0] || null;
  }, [payrollRuns]);

  const currentPayrollLabel = useMemo(() => {
    if (!currentPayroll) return "Chưa tích hợp";
    const code = fieldOf(currentPayroll, "runCode", "RunCode", "name", "Name") || `Run #${fieldOf(currentPayroll, "id", "Id")}`;
    const status = normalizeStatus(fieldOf(currentPayroll, "status", "Status")) || "PENDING";
    return `${code} (${status})`;
  }, [currentPayroll]);

  const warnings = useMemo(() => {
    const rows = [];

    if (paymentStats.duplicateCount > 0) {
      rows.push({
        level: "high",
        title: `${paymentStats.duplicateCount} mã giao dịch bị trùng`,
        detail: "Nguy cơ ghi nhận doanh thu trùng. Cần rà soát đối chiếu ngay."
      });
    }

    if (paymentStats.inflowNoInvoice > 0) {
      rows.push({
        level: "high",
        title: `${paymentStats.inflowNoInvoice} payment inflow chưa gắn invoice`,
        detail: "Có nguy cơ vi phạm ràng buộc nghiệp vụ Payment-Invoice."
      });
    }

    if (invoiceSummary.overdueCount > 0) {
      rows.push({
        level: "high",
        title: `${invoiceSummary.overdueCount} hóa đơn đã quá hạn`,
        detail: "Cần ưu tiên thu hồi công nợ để giảm rủi ro dòng tiền."
      });
    }

    if (apDueSummary.overdueAmount > 0) {
      rows.push({
        level: "medium",
        title: `AP quá hạn: ${formatCurrency(apDueSummary.overdueAmount)}`,
        detail: "Rà soát kế hoạch thanh toán để tránh phạt/lệch công nợ NCC."
      });
    }

    if (approvalsPending.length > 0) {
      rows.push({
        level: "medium",
        title: `${approvalsPending.length} yêu cầu approval chưa xử lý`,
        detail: "Có thể chặn luồng hoàn tiền, điều chỉnh hoặc quyết toán."
      });
    }

    if (financeSnapshot.netProfit < 0) {
      rows.push({
        level: "medium",
        title: "Net profit đang âm",
        detail: "Theo dõi chi phí, tỷ lệ refund/reversal và biên lợi nhuận."
      });
    }

    if (!rows.length) {
      rows.push({
        level: "low",
        title: "Không có cảnh báo nghiêm trọng",
        detail: "Hệ thống đang vận hành ổn định theo dữ liệu hiện tại."
      });
    }

    return rows;
  }, [paymentStats, invoiceSummary, apDueSummary, approvalsPending.length, financeSnapshot.netProfit]);

  const topPendingApprovals = approvalsPending.slice(0, 8);

  const activeFilterLabel = useMemo(() => {
    const branchName =
      effectiveBranchFilter === "all"
        ? "Tất cả chi nhánh"
        : branchOptions.find((x) => String(x.id) === String(effectiveBranchFilter))?.name || `Chi nhánh #${effectiveBranchFilter}`;
    const periodLabel = periodMode === "month" && selectedMonth ? `Tháng ${selectedMonth}` : "Theo ngày mới nhất";
    return `${branchName} · ${periodLabel}`;
  }, [branchOptions, effectiveBranchFilter, periodMode, selectedMonth]);

  const queryString = useMemo(() => {
    const params = new URLSearchParams();
    if (effectiveBranchFilter && effectiveBranchFilter !== "all") params.set("branchId", String(effectiveBranchFilter));
    params.set("periodMode", periodMode);
    if (selectedMonth) params.set("month", selectedMonth);
    return params.toString();
  }, [effectiveBranchFilter, periodMode, selectedMonth]);

  const kpiCards = useMemo(
    () => [
      {
        key: "revenue",
        name: "Doanh thu",
        value: formatCurrency(financeSnapshot.totalRevenue),
        icon: MdBarChart
      },
      {
        key: "collected",
        name: "Đã thu",
        value: formatCurrency(displayedCollected),
        icon: MdAttachMoney
      },
      {
        key: "ar_open",
        name: "Công nợ phải thu mở",
        value: formatCurrency(invoiceSummary.totalOutstanding),
        icon: MdReceiptLong
      },
      {
        key: "approval_pending",
        name: "Approval chờ xử lý",
        value: String(approvalsPending.length),
        icon: MdPendingActions
      },
      {
        key: "ap_due",
        name: "AP quá hạn + sắp hạn",
        value: formatCurrency(apDueSummary.overdueAmount + apDueSummary.dueSoonAmount),
        icon: MdWarning
      },
      {
        key: "inflow_no_invoice",
      name: "Inflow chưa gắn invoice",
        value: String(paymentStats.inflowNoInvoice),
        icon: MdFactCheck
      },
      {
        key: "net_profit",
        name: "Net profit",
        value: formatCurrency(financeSnapshot.netProfit),
        icon: financeSnapshot.netProfit >= 0 ? MdTrendingUp : MdTrendingDown
      },
      {
        key: "refund_reversal",
        name: "Refund + reversal",
        value: formatCurrency(paymentStats.refunds + paymentStats.reversals),
        icon: MdAccountBalanceWallet
      }
    ],
    [financeSnapshot.totalRevenue, displayedCollected, invoiceSummary.totalOutstanding, approvalsPending.length, apDueSummary.overdueAmount, apDueSummary.dueSoonAmount, paymentStats.inflowNoInvoice, financeSnapshot.netProfit, paymentStats.refunds, paymentStats.reversals]
  );

  const kpiDetail = useMemo(() => {
    if (selectedKpiKey === "approval_pending") {
      return {
        title: "Chi tiết approvals chờ xử lý",
        columns: ["Loại", "Lý do", "Tạo lúc"],
        rows: topPendingApprovals.map((item) => [
          fieldOf(item, "requestType", "RequestType") || "N/A",
          fieldOf(item, "reason", "Reason") || "--",
          formatDateTime(fieldOf(item, "createdAt", "CreatedAt"))
        ])
      };
    }

    if (selectedKpiKey === "ar_open") {
      return {
        title: "Top hóa đơn cần theo dõi",
        columns: ["Hóa đơn", "Khách hàng", "Hạn", "Còn phải thu"],
        rows: invoiceSummary.rows.slice(0, 8).map((item, idx) => [
          fieldOf(item, "invoiceCode", "InvoiceCode", "code", "Code") || `INV-${fieldOf(item, "id", "Id", idx + 1)}`,
          fieldOf(item, "customerName", "CustomerName", "receiverName", "ReceiverName") || "--",
          formatDate(fieldOf(item, "dueDate", "DueDate")),
          formatCurrency(item.outstanding)
        ])
      };
    }

    if (selectedKpiKey === "ap_due") {
      return {
        title: "Top AP đến hạn / quá hạn",
        columns: ["Supplier/AP", "Hạn", "Outstanding", "Mức độ"],
        rows: apDueSummary.rows.slice(0, 8).map((item, idx) => [
          fieldOf(item, "supplierName", "SupplierName", "apCode", "ApCode", "name", "Name") || `AP #${idx + 1}`,
          formatDate(item.dueDate),
          formatCurrency(item.outstanding),
          item.overdue ? "Quá hạn" : item.dueSoon ? "Sắp hạn" : "Ổn định"
        ])
      };
    }

    if (selectedKpiKey === "inflow_no_invoice") {
      return {
      title: "Top payment inflow chưa gắn invoice",
        columns: ["Transaction code", "S? ti?n", "Th?i di?m"],
        rows: filteredPayments
          .filter((item) => {
            const direction = normalizeStatus(fieldOf(item, "direction", "Direction"));
            const status = normalizeStatus(fieldOf(item, "status", "Status"));
            const invoiceId = fieldOf(item, "invoiceId", "InvoiceId");
            const isInflow = direction === "INFLOW" || direction === "IN";
            return isInflow && status === "COMPLETED" && !invoiceId;
          })
          .slice(0, 8)
          .map((item) => [
            fieldOf(item, "transactionCode", "TransactionCode") || "--",
            formatCurrency(toNumber(fieldOf(item, "amount", "Amount"))),
            formatDateTime(fieldOf(item, "paidAt", "PaidAt", "createdAt", "CreatedAt"))
          ])
      };
    }

    return {
      title: "Tóm tắt KPI đã chọn",
      columns: ["Chỉ số", "Giá trị"],
      rows: [
        ["Bộ lọc hiện tại", activeFilterLabel],
        ["Doanh thu", formatCurrency(financeSnapshot.totalRevenue)],
        ["Đã thu", formatCurrency(displayedCollected)],
        ["Công nợ phải thu mở", formatCurrency(invoiceSummary.totalOutstanding)],
        ["AP quá hạn + sắp hạn", formatCurrency(apDueSummary.overdueAmount + apDueSummary.dueSoonAmount)],
        ["Net profit", formatCurrency(financeSnapshot.netProfit)]
      ]
    };
  }, [selectedKpiKey, topPendingApprovals, invoiceSummary.rows, apDueSummary.rows, filteredPayments, activeFilterLabel, financeSnapshot.totalRevenue, displayedCollected, invoiceSummary.totalOutstanding, apDueSummary.overdueAmount, apDueSummary.dueSoonAmount, financeSnapshot.netProfit]);

  function goTo(path) {
    navigate(queryString ? `${path}?${queryString}` : path);
  }

  function handleExportCsv() {
    const rows = [
      ["Dashboard kế toán V2", new Date().toISOString()],
      ["B? l?c", activeFilterLabel],
      [],
      ["KPI", "Giá trị"],
      ["Doanh thu", formatCurrency(financeSnapshot.totalRevenue)],
      ["Đã thu", formatCurrency(displayedCollected)],
      ["Công nợ phải thu mở", formatCurrency(invoiceSummary.totalOutstanding)],
      ["AP quá hạn + sắp hạn", formatCurrency(apDueSummary.overdueAmount + apDueSummary.dueSoonAmount)],
      ["Approval chờ xử lý", approvalsPending.length],
      ["Inflow chưa gắn invoice", paymentStats.inflowNoInvoice],
      ["Net profit", formatCurrency(financeSnapshot.netProfit)],
      [],
      ["Cảnh báo", "Mức độ", "Chi tiết"],
      ...warnings.map((w) => [w.title, w.level, w.detail])
    ];
    downloadCsv(`dashboard-ke-toan-${new Date().toISOString().slice(0, 10)}.csv`, rows);
  }

  function handlePrintPdf() {
    window.print();
  }

  if (loading) {
    return (
      <Flex pt='20px' align='center' justify='center' minH='280px' direction='column' gap='12px'>
        <Spinner size='lg' color='brand.500' />
        <Text color={textColorSecondary}>Đang tải dữ liệu dashboard kế toán...</Text>
      </Flex>
    );
  }

  return (
    <Box pt={{ base: "10px", md: "0px", xl: "0px" }}>
      <Card p='18px' mb='20px'>
        <Flex gap='12px' wrap='wrap' align='center' justify='space-between'>
          <Box>
            <Text color={textColor} fontSize='xl' fontWeight='800'>Dashboard vận hành kế toán</Text>
            <Text color={textColorSecondary} fontSize='sm'>
              Tổng hợp công nợ, thu chi, approvals, AP và chất lượng dữ liệu trong một màn hình.
            </Text>
            <Badge mt='8px' colorScheme='blue'>{activeFilterLabel}</Badge>
          </Box>

          <Flex gap='10px' wrap='wrap'>
            <Select value={periodMode} onChange={(e) => setPeriodMode(e.target.value)} w='140px'>
              <option value='day'>Theo ngày</option>
              <option value='month'>Theo tháng</option>
            </Select>
            <Select
              value={selectedMonth}
              onChange={(e) => setSelectedMonth(e.target.value)}
              isDisabled={periodMode !== "month"}
              w='150px'>
              {monthOptions.length ? (
                monthOptions.map((monthKey) => (
                  <option value={monthKey} key={monthKey}>
                    {monthKey}
                  </option>
                ))
              ) : (
                <option value=''>Không có dữ liệu</option>
              )}
            </Select>
            <Select
              value={enforcedBranchId || branchFilter}
              onChange={(e) => setBranchFilter(e.target.value)}
              isDisabled={Boolean(enforcedBranchId)}
              w='190px'>
              {!enforcedBranchId ? <option value='all'>Tất cả chi nhánh</option> : null}
              {branchOptions.map((branch) => (
                <option value={branch.id} key={branch.id}>
                  {branch.name}
                </option>
              ))}
            </Select>
            <Button colorScheme='blue' variant='outline' onClick={handleExportCsv}>
              Xu?t CSV
            </Button>
            <Button colorScheme='blue' variant='solid' onClick={handlePrintPdf}>
              In / PDF
            </Button>
          </Flex>
        </Flex>
      </Card>

      {loadError ? (
        <Card p='16px' mb='20px'>
          <Text color='red.400' fontWeight='600'>
            {loadError}
          </Text>
        </Card>
      ) : null}

      <SimpleGrid columns={{ base: 1, md: 2, lg: 3, "2xl": 4 }} gap='20px' mb='20px'>
        {kpiCards.map((kpi) => {
          const isActive = selectedKpiKey === kpi.key;
          return (
            <Box
              key={kpi.key}
              borderRadius='20px'
              border='2px solid'
              borderColor={isActive ? "brand.500" : "transparent"}
              cursor='pointer'
              onClick={() => setSelectedKpiKey(kpi.key)}>
              <MiniStatistics
                startContent={
                  <IconBox
                    w='56px'
                    h='56px'
                    bg={kpi.key === "net_profit" ? (financeSnapshot.netProfit >= 0 ? "linear-gradient(90deg, #16a34a 0%, #22c55e 100%)" : "linear-gradient(90deg, #ef4444 0%, #f97316 100%)") : boxBg}
                    icon={<Icon w='30px' h='30px' as={kpi.icon} color={kpi.key === "net_profit" ? "white" : brandColor} />}
                  />
                }
                name={kpi.name}
                value={kpi.value}
              />
            </Box>
          );
        })}
      </SimpleGrid>

      <SimpleGrid columns={{ base: 1, xl: 3 }} gap='20px' mb='20px'>
        <Card p='20px' bg={panelBg} gridColumn={{ base: "auto", xl: "span 2" }}>
          <Flex justify='space-between' align='center' mb='12px'>
            <Text color={textColor} fontWeight='700' fontSize='lg'>{kpiDetail.title}</Text>
            <Badge colorScheme='purple'>{selectedKpiKey}</Badge>
          </Flex>
          {kpiDetail.rows.length ? (
            <Box maxH='320px' overflowY='auto' overflowX='auto'>
              <Table size='sm'>
                <Thead>
                  <Tr>
                    {kpiDetail.columns.map((col) => (
                      <Th key={col}>{col}</Th>
                    ))}
                  </Tr>
                </Thead>
                <Tbody>
                  {kpiDetail.rows.map((row, idx) => (
                    <Tr key={`${selectedKpiKey}-${idx}`}>
                      {row.map((cell, cellIdx) => (
                        <Td key={`${selectedKpiKey}-${idx}-${cellIdx}`} color={cellIdx === 0 ? textColor : textColorSecondary} fontWeight={cellIdx === 0 ? "600" : "500"}>
                          {cell}
                        </Td>
                      ))}
                    </Tr>
                  ))}
                </Tbody>
              </Table>
            </Box>
          ) : (
              <Text color={textColorSecondary}>Không có dữ liệu chi tiết cho KPI này.</Text>
          )}
        </Card>

        <Card p='20px' bg={panelBg}>
          <Text color={textColor} fontWeight='700' fontSize='lg' mb='12px'>Điều hướng nhanh</Text>
          <Flex direction='column' gap='10px'>
            <Button variant='outline' onClick={() => goTo("/admin/accounting/invoices")}>Mở hóa đơn</Button>
            <Button variant='outline' onClick={() => goTo("/admin/accounting/payments")}>M? thu chi</Button>
            <Button variant='outline' onClick={() => goTo("/admin/accounting/approvals")}>M? approvals</Button>
            <Button variant='outline' onClick={() => goTo("/admin/accounting/payroll")}>M? payroll</Button>
            <Button variant='outline' onClick={() => goTo("/admin/accounting/reports")}>Mở báo cáo</Button>
          </Flex>
        </Card>
      </SimpleGrid>

      <SimpleGrid columns={{ base: 1, xl: 2 }} gap='20px' mb='20px'>
        <Card p='20px' bg={panelBg}>
          <Flex justify='space-between' align='center' mb='12px'>
            <Text color={textColor} fontWeight='700' fontSize='lg'>Hàng đợi cần xử lý</Text>
            <Badge colorScheme='purple'>{topPendingApprovals.length} item</Badge>
          </Flex>

          {topPendingApprovals.length ? (
            <Box maxH='320px' overflowY='auto'>
              <Table size='sm'>
                <Thead>
                  <Tr>
                    <Th>Loại</Th>
                    <Th>Lý do</Th>
                    <Th>Trạng thái</Th>
                    <Th>Tạo lúc</Th>
                  </Tr>
                </Thead>
                <Tbody>
                  {topPendingApprovals.map((item) => (
                    <Tr key={String(fieldOf(item, "id", "Id"))}>
                      <Td color={textColor} fontWeight='600'>{fieldOf(item, "requestType", "RequestType") || "N/A"}</Td>
                      <Td color={textColorSecondary}>{fieldOf(item, "reason", "Reason") || "--"}</Td>
                      <Td>
                        <Badge colorScheme='orange'>{fieldOf(item, "status", "Status") || "PENDING"}</Badge>
                      </Td>
                      <Td color={textColorSecondary}>{formatDateTime(fieldOf(item, "createdAt", "CreatedAt"))}</Td>
                    </Tr>
                  ))}
                </Tbody>
              </Table>
            </Box>
          ) : (
            <Text color={textColorSecondary}>Không có yêu cầu chờ duyệt.</Text>
          )}
        </Card>

        <Card p='20px' bg={panelBg}>
          <Flex justify='space-between' align='center' mb='12px'>
            <Text color={textColor} fontWeight='700' fontSize='lg'>Trạng thái vận hành</Text>
            <Flex gap='8px' wrap='wrap'>
              <Badge colorScheme='blue'>Kỳ kế toán: {openPeriodLabel}</Badge>
              <Badge colorScheme='green'>Payroll: {currentPayrollLabel}</Badge>
            </Flex>
          </Flex>

          <SimpleGrid columns={{ base: 1, md: 2 }} gap='10px'>
            <Box p='10px' border='1px solid' borderColor={borderColor} borderRadius='12px' bg={subPanelBg}>
              <Text fontSize='xs' color={textColorSecondary}>Gross margin</Text>
              <Text color={textColor} fontWeight='700'>{financeSnapshot.grossMargin.toFixed(2)}%</Text>
            </Box>
            <Box p='10px' border='1px solid' borderColor={borderColor} borderRadius='12px' bg={subPanelBg}>
              <Text fontSize='xs' color={textColorSecondary}>Net margin</Text>
              <Text color={textColor} fontWeight='700'>{financeSnapshot.netMargin.toFixed(2)}%</Text>
            </Box>
            <Box p='10px' border='1px solid' borderColor={borderColor} borderRadius='12px' bg={subPanelBg}>
              <Text fontSize='xs' color={textColorSecondary}>AP total outstanding</Text>
              <Text color={textColor} fontWeight='700'>{formatCurrency(apDueSummary.totalOutstanding)}</Text>
            </Box>
            <Box p='10px' border='1px solid' borderColor={borderColor} borderRadius='12px' bg={subPanelBg}>
              <Text fontSize='xs' color={textColorSecondary}>Invoice m?</Text>
              <Text color={textColor} fontWeight='700'>{invoiceSummary.openCount}</Text>
            </Box>
          </SimpleGrid>
        </Card>
      </SimpleGrid>

      <SimpleGrid columns={{ base: 1, xl: 2 }} gap='20px' mb='20px'>
        <Card p='20px' bg={panelBg}>
          <Flex justify='space-between' align='center' mb='12px'>
          <Text color={textColor} fontWeight='700' fontSize='lg'>AR - Hóa đơn cần theo dõi</Text>
            <Flex gap='8px' wrap='wrap'>
              <Badge colorScheme='red'>Quá hạn: {invoiceSummary.overdueCount}</Badge>
              <Badge colorScheme='orange'>Sắp hạn: {invoiceSummary.dueSoonCount}</Badge>
            </Flex>
          </Flex>

          {invoiceSummary.rows.length ? (
            <Box maxH='360px' overflowY='auto' overflowX='auto'>
              <Table size='sm'>
                <Thead>
                  <Tr>
                    <Th>Hóa đơn</Th>
                    <Th>Khách hàng</Th>
                  <Th>Hạn thanh toán</Th>
                    <Th isNumeric>Còn phải thu</Th>
                    <Th>Ưu tiên</Th>
                  </Tr>
                </Thead>
                <Tbody>
                  {invoiceSummary.rows.map((item, idx) => {
                    const code = fieldOf(item, "invoiceCode", "InvoiceCode", "code", "Code") || `INV-${fieldOf(item, "id", "Id", idx + 1)}`;
                    const customer = fieldOf(item, "customerName", "CustomerName", "receiverName", "ReceiverName") || "--";
                    const dueDate = fieldOf(item, "dueDate", "DueDate");
                    const level = item.overdue ? "Quá hạn" : item.dueSoon ? "Sắp hạn" : "Theo dõi";
                    return (
                      <Tr key={`${code}-${idx}`}>
                        <Td color={textColor} fontWeight='600'>{code}</Td>
                        <Td color={textColorSecondary}>{customer}</Td>
                        <Td color={textColorSecondary}>{formatDate(dueDate)}</Td>
                        <Td color={textColor} isNumeric>{formatCurrency(item.outstanding)}</Td>
                        <Td>
                          <Badge colorScheme={item.overdue ? "red" : item.dueSoon ? "orange" : "blue"}>{level}</Badge>
                        </Td>
                      </Tr>
                    );
                  })}
                </Tbody>
              </Table>
            </Box>
          ) : (
            <Text color={textColorSecondary}>Không có hóa đơn mở cần theo dõi.</Text>
          )}
        </Card>

        <Card p='20px' bg={panelBg}>
          <Flex justify='space-between' align='center' mb='12px'>
            <Text color={textColor} fontWeight='700' fontSize='lg'>AP đến hạn / quá hạn</Text>
            <Flex gap='8px' wrap='wrap'>
              <Badge colorScheme='red'>{formatCurrency(apDueSummary.overdueAmount)}</Badge>
              <Badge colorScheme='orange'>{formatCurrency(apDueSummary.dueSoonAmount)}</Badge>
            </Flex>
          </Flex>

          {apDueSummary.rows.length ? (
            <Box maxH='360px' overflowY='auto' overflowX='auto'>
              <Table size='sm'>
                <Thead>
                  <Tr>
                    <Th>Supplier/AP</Th>
                    <Th>Due date</Th>
                    <Th isNumeric>Outstanding</Th>
                    <Th>Trạng thái</Th>
                  </Tr>
                </Thead>
                <Tbody>
                  {apDueSummary.rows.map((item, index) => {
                    const title =
                      fieldOf(item, "supplierName", "SupplierName", "apCode", "ApCode", "name", "Name") || `AP #${index + 1}`;
                    return (
                      <Tr key={`${title}-${index}`}>
                        <Td color={textColor} fontWeight='600'>{title}</Td>
                        <Td color={textColorSecondary}>{formatDate(item.dueDate)}</Td>
                        <Td color={textColor} isNumeric>{formatCurrency(item.outstanding)}</Td>
                        <Td>
                          <Badge colorScheme={item.overdue ? "red" : item.dueSoon ? "orange" : "green"}>
                            {item.overdue ? "Quá hạn" : item.dueSoon ? "Sắp đến hạn" : "Ổn định"}
                          </Badge>
                        </Td>
                      </Tr>
                    );
                  })}
                </Tbody>
              </Table>
            </Box>
          ) : (
            <Text color={textColorSecondary}>Chưa có dữ liệu accounts payable từ API.</Text>
          )}
        </Card>
      </SimpleGrid>

      <Card p='20px' bg={panelBg}>
        <Flex justify='space-between' align='center' mb='12px'>
          <Text color={textColor} fontWeight='700' fontSize='lg'>Cảnh báo thất thoát / lệch dữ liệu</Text>
          <Badge colorScheme={warnings.some((w) => w.level === "high") ? "red" : warnings.some((w) => w.level === "medium") ? "orange" : "green"}>
            {warnings.some((w) => w.level === "high") ? "ƯU TIÊN CAO" : "ỔN ĐỊNH"}
          </Badge>
        </Flex>

        <SimpleGrid columns={{ base: 1, xl: 2 }} gap='12px'>
          {warnings.map((item) => (
            <Flex
              key={item.title}
              align='flex-start'
              gap='10px'
              p='12px'
              border='1px solid'
              borderColor={borderColor}
              borderRadius='14px'
              bg={subPanelBg}>
              <Icon
                as={item.level === "high" ? MdWarning : item.level === "medium" ? MdErrorOutline : MdChecklist}
                w='20px'
                h='20px'
                color={item.level === "high" ? "red.400" : item.level === "medium" ? "orange.400" : "green.400"}
                mt='2px'
              />
              <Box>
                <Text color={textColor} fontWeight='700' fontSize='sm'>{item.title}</Text>
                <Text color={textColorSecondary} fontSize='xs'>{item.detail}</Text>
              </Box>
            </Flex>
          ))}
        </SimpleGrid>
      </Card>
    </Box>
  );
}


