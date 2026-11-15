import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { createChatHubConnection } from "@features/chat/realtime/chatHubConnection";
import { getAllCustomers, getAllVehicles } from "@features/admin/api/customerApi";
import { getAllStaff } from "@features/admin/api/staffApi";
import DateRangePickerModal, { getDateRangeButtonLabel } from "@features/admin/components/DateRangePickerModal";
import { getStatusBadge } from "@features/admin/utils/adminUtils";
import { useSearch } from "@shared/core/context/SearchContext";
import { useBranch } from "@shared/core/context/BranchContext";
import { useAuth } from "@shared/core/context/AuthContext";
import TablePagination from "@shared/components/TablePagination";
import FilterDropdown from "@shared/components/FilterDropdown";
import { getStatusLabelVi, toStatusOptionVi } from "@shared/core/utils/statusLabelUtils";
import ServiceOrderDetailModal from "@features/workshop/components/ServiceOrderDetailModal";
const AUTO_DISPATCH_WARNING_PREFIX = "[AUTO_DISPATCH_WARNING]:";
const DIAGNOSIS_DISPATCH_REVIEW_STORAGE_KEY = "mygarage.diagnosisDispatchReviewOrders";

function parseBranchId(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
}

function toDateText(dateObj) {
  if (!(dateObj instanceof Date) || Number.isNaN(dateObj.getTime())) return "";
  const y = dateObj.getFullYear();
  const m = String(dateObj.getMonth() + 1).padStart(2, "0");
  const d = String(dateObj.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

function toStartOfDay(dateText) {
  if (!dateText) return null;
  const d = new Date(`${dateText}T00:00:00`);
  return Number.isNaN(d.getTime()) ? null : d;
}

function toEndOfDay(dateText) {
  if (!dateText) return null;
  const d = new Date(`${dateText}T23:59:59.999`);
  return Number.isNaN(d.getTime()) ? null : d;
}

function normalizeStatus(status) {
  return String(status || "").trim().toUpperCase();
}

const ORDER_VIEW_MODE = {
  SINGLE: "SINGLE",
  GROUP: "GROUP",
};

const GROUP_STATUS_PRIORITY = [
  "WAITING_APPROVAL",
  "SENT_TO_CUSTOMER",
  "CUSTOMER_REJECTED",
  "QUOTING",
  "DIAGNOSING",
  "IN_PROGRESS",
  "REPAIRING",
  "READY",
  "WAITING_PAYMENT",
  "CHECKED_IN",
  "RECEPTION",
  "NEW",
  "CLOSED",
  "COMPLETED",
  "CANCELED",
  "CANCELED",
];

const QUOTE_WAITING_STATUSES = ["WAITING_APPROVAL", "SENT_TO_CUSTOMER", "CUSTOMER_REJECTED", "QUOTING"];

function resolveGroupStatus(statuses) {
  if (!Array.isArray(statuses) || statuses.length === 0) return "NEW";
  const normalized = statuses.map((status) => normalizeStatus(status)).filter(Boolean);
  if (normalized.length === 0) return "NEW";

  const sorted = [...normalized].sort((a, b) => {
    const idxA = GROUP_STATUS_PRIORITY.indexOf(a);
    const idxB = GROUP_STATUS_PRIORITY.indexOf(b);
    const rankA = idxA >= 0 ? idxA : GROUP_STATUS_PRIORITY.length + 1;
    const rankB = idxB >= 0 ? idxB : GROUP_STATUS_PRIORITY.length + 1;
    if (rankA !== rankB) return rankA - rankB;
    return a.localeCompare(b);
  });
  return sorted[0];
}

function parseApiDate(value) {
  if (!value) return null;
  if (value instanceof Date) {
    return Number.isNaN(value.getTime()) ? null : value;
  }

  const raw = String(value).trim();
  if (!raw) return null;
  const hasTimezone = /(?:Z|[+\-]\d{2}:\d{2})$/i.test(raw);
  const normalized = hasTimezone ? raw : `${raw}Z`;
  const parsed = new Date(normalized);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

function toApiDateMillis(value) {
  return parseApiDate(value)?.getTime() ?? 0;
}

function formatOrderDate(value) {
  const parsed = parseApiDate(value);
  if (!parsed) return "-";
  return parsed.toLocaleDateString("vi-VN", {
    timeZone: "Asia/Ho_Chi_Minh",
  });
}

function formatOrderTime(value) {
  const parsed = parseApiDate(value);
  if (!parsed) return "--:--";
  return parsed.toLocaleTimeString("vi-VN", {
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
    timeZone: "Asia/Ho_Chi_Minh",
  });
}

function buildOrderRowInteraction(order, onOrderClick) {
  if (!onOrderClick) return {};
  const openDetail = () => onOrderClick(order);
  return {
    role: "button",
    tabIndex: 0,
    onClick: openDetail,
    onKeyDown: (event) => {
      if (event.key === "Enter" || event.key === " ") {
        event.preventDefault();
        openDetail();
      }
    },
  };
}

function hasOrderDispatchReview(order) {
  return Boolean(
    order?.requiresDispatchReview
    || order?.RequiresDispatchReview
    || order?.hasDiagnosisDispatchWarning
    || order?.HasDiagnosisDispatchWarning
  );
}

function hasOrderPartsFollowUp(order) {
  return Boolean(
    order?.requiresPartsFollowUp
    || order?.RequiresPartsFollowUp
  );
}

function hasOrderSupplementaryQuoteProposal(order) {
  return Boolean(
    order?.requiresSupplementaryQuote
    || order?.RequiresSupplementaryQuote
    || Number(order?.supplementaryPendingCount ?? order?.SupplementaryPendingCount ?? 0) > 0
  );
}

function parseDispatchReviewOrderIds() {
  if (typeof window === "undefined") return new Set();
  try {
    const raw = window.localStorage.getItem(DIAGNOSIS_DISPATCH_REVIEW_STORAGE_KEY);
    const list = JSON.parse(raw || "[]");
    if (!Array.isArray(list)) return new Set();
    return new Set(
      list
        .map((value) => Number(value))
        .filter((value) => Number.isFinite(value) && value > 0)
    );
  } catch {
    return new Set();
  }
}

function getOrderIssueLabel(order) {
  if (hasOrderPartsFollowUp(order)) {
    return "Thiếu phụ tùng, cần gọi khách";
  }
  if (hasOrderSupplementaryQuoteProposal(order)) {
    const pendingCount = Number(order?.supplementaryPendingCount ?? order?.SupplementaryPendingCount ?? 0);
    return pendingCount > 0
      ? `Có ${pendingCount} đề xuất phải lập báo giá bổ sung`
      : "Có đề xuất phải lập báo giá bổ sung";
  }
  if (hasOrderDispatchReview(order)) {
    return "Cần điều phối thủ công";
  }
  return "";
}

function OrderIssueIndicator({ order }) {
  const issueLabel = getOrderIssueLabel(order);
  const isVisible = Boolean(issueLabel);
  if (!isVisible) return null;

  return (
    <span
      className="appointment-issue-indicator"
      title={issueLabel}
      aria-label={issueLabel}
    >
      !
    </span>
  );
}


export default function ReceptionOrdersSection() {
  const navigate = useNavigate();
  const location = useLocation();
  const { searchQuery } = useSearch();
  const { selectedBranchId } = useBranch();
  const { user } = useAuth();

  const [orders, setOrders] = useState([]);
  const [customers, setCustomers] = useState([]);
  const [vehicles, setVehicles] = useState([]);
  const [staff, setStaff] = useState([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [modal, setModal] = useState(null);
  const [selectedOrderForView, setSelectedOrderForView] = useState(null);
  const [orderStatusFilter, setOrderStatusFilter] = useState("ALL");
  const [orderSortBy, setOrderSortBy] = useState("CREATED_DESC");
  const [orderViewMode, setOrderViewMode] = useState(ORDER_VIEW_MODE.SINGLE);
  const [dateFrom, setDateFrom] = useState(() => toDateText(new Date()));
  const [dateTo, setDateTo] = useState(() => toDateText(new Date()));
  const [ordersPage, setOrdersPage] = useState(1);
  const [ordersPageSize, setOrdersPageSize] = useState(10);
  const [actingGroup, setActingGroup] = useState(null);
  const [supplementaryToast, setSupplementaryToast] = useState(null);
  const supplementaryToastTimerRef = useRef(null);

  const contextBranchId = parseBranchId(selectedBranchId);
  const userBranchId = parseBranchId(user?.branchId ?? user?.BranchId);
  const effectiveBranchId = contextBranchId || userBranchId;

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [oRes, cRes, vRes, sRes, diagnosisRes, supplementaryRes] = await Promise.all([
        serviceOrderApi.getAll(),
        getAllCustomers(),
        getAllVehicles(),
        getAllStaff(),
        serviceOrderApi.getDiagnoses().catch(() => []),
        serviceOrderApi.getPendingSupplementaryProposals(
          effectiveBranchId ? { branchId: effectiveBranchId } : undefined
        ).catch(() => []),
      ]);

      const diagnosisList = Array.isArray(diagnosisRes) ? diagnosisRes : [];
      const supplementaryList = Array.isArray(supplementaryRes) ? supplementaryRes : [];
      const diagnosisByOrderId = new Map();
      const supplementaryByOrderId = new Map(
        supplementaryList
          .map((item) => {
            const soId = Number(item?.serviceOrderId ?? item?.ServiceOrderId ?? 0);
            const pendingCount = Number(item?.pendingCount ?? item?.PendingCount ?? 0);
            if (!Number.isFinite(soId) || soId <= 0) return null;
            return [soId, Number.isFinite(pendingCount) ? Math.max(0, pendingCount) : 0];
          })
          .filter(Boolean),
      );
      const persistedDispatchReviewOrderIds = parseDispatchReviewOrderIds();
      let shouldPersistDispatchReviewOrderIds = false;
      diagnosisList.forEach((item) => {
        const serviceOrderId = Number(item?.serviceOrderId ?? item?.ServiceOrderId ?? 0);
        if (!Number.isFinite(serviceOrderId) || serviceOrderId <= 0) return;
        if (!diagnosisByOrderId.has(serviceOrderId)) diagnosisByOrderId.set(serviceOrderId, []);
        diagnosisByOrderId.get(serviceOrderId).push(item);
      });

      const baseOrders = (Array.isArray(oRes) ? oRes : []).map((order) => {
        const orderId = Number(order?.id ?? order?.Id ?? 0);
        const orderStatus = String(order?.status ?? order?.Status ?? "").trim().toUpperCase();
        const relatedDiagnoses = diagnosisByOrderId.get(orderId) || [];
        const hasAutoDispatchWarning = relatedDiagnoses.some((diag) => {
          const note = String(diag?.supportRequestNote ?? diag?.SupportRequestNote ?? "").trim();
          return note.startsWith(AUTO_DISPATCH_WARNING_PREFIX);
        });
        const hasPendingUnassigned = relatedDiagnoses.some((diag) => {
          const status = String(diag?.status ?? diag?.Status ?? "").trim().toUpperCase();
          const teamId = Number(diag?.assignedTeamId ?? diag?.AssignedTeamId ?? 0);
          const technicianId = Number(diag?.technicianId ?? diag?.TechnicianId ?? 0);
          return status === "PENDING" && (teamId <= 0 || technicianId <= 0);
        });

        const diagnosisDispatchIssue =
          hasAutoDispatchWarning || (hasPendingUnassigned && orderStatus === "DIAGNOSING");
        const supplementaryPendingCount = supplementaryByOrderId.get(orderId) || 0;

        return {
          ...order,
          __diagnosisDispatchIssue: diagnosisDispatchIssue,
          __persistedDispatchReviewIssue: persistedDispatchReviewOrderIds.has(orderId),
          requiresDispatchReview: diagnosisDispatchIssue || persistedDispatchReviewOrderIds.has(orderId),
          hasDiagnosisDispatchWarning: diagnosisDispatchIssue || persistedDispatchReviewOrderIds.has(orderId),
          requiresSupplementaryQuote: supplementaryPendingCount > 0,
          supplementaryPendingCount,
        };
      });

      const enrichedOrders = baseOrders.map((order) => {
        const orderId = Number(order?.id ?? order?.Id ?? 0);
        const diagnosisDispatchIssue = Boolean(order?.__diagnosisDispatchIssue);
        const persistedDispatchIssue = Boolean(order?.__persistedDispatchReviewIssue);
        const hasDispatchIssue = diagnosisDispatchIssue || persistedDispatchIssue;
        if (!hasDispatchIssue && persistedDispatchIssue && persistedDispatchReviewOrderIds.has(orderId)) {
          persistedDispatchReviewOrderIds.delete(orderId);
          shouldPersistDispatchReviewOrderIds = true;
        }

        // Internal fields are used only during in-memory enrichment and should not leak to UI consumers.
        const { __diagnosisDispatchIssue, __persistedDispatchReviewIssue, ...cleanOrder } = order;
        return {
          ...cleanOrder,
          requiresDispatchReview: hasDispatchIssue,
          hasDiagnosisDispatchWarning: hasDispatchIssue,
        };
      });

      if (shouldPersistDispatchReviewOrderIds) {
        window.localStorage.setItem(
          DIAGNOSIS_DISPATCH_REVIEW_STORAGE_KEY,
          JSON.stringify(Array.from(persistedDispatchReviewOrderIds))
        );
      }

      setOrders(enrichedOrders);
      setCustomers(Array.isArray(cRes) ? cRes : []);
      setVehicles(Array.isArray(vRes) ? vRes : []);

      const filteredStaff = (Array.isArray(sRes) ? sRes : []).filter((item) => {
        const roleCode = String(item.roleCode ?? item.RoleCode ?? "").trim().toUpperCase();
        const isReceptionist = roleCode === "RECEPTIONIST";
        const staffBranchId = Number(item.branchId ?? item.BranchId ?? 0);
        const branchMatched = !effectiveBranchId || staffBranchId === effectiveBranchId;
        return isReceptionist && branchMatched;
      });
      setStaff(filteredStaff);
    } catch (err) {
      setError(err?.message || "Không thể tải dữ liệu tiếp nhận.");
    } finally {
      setLoading(false);
    }
  }, [effectiveBranchId]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  useEffect(() => {
    let isDisposed = false;
    const connection = createChatHubConnection();

    const handleSupplementaryUpdated = (payload) => {
      if (isDisposed) return;
      const orderId = Number(payload?.serviceOrderId ?? payload?.ServiceOrderId ?? 0);
      if (Number.isFinite(orderId) && orderId > 0) {
        const orderCode = String(payload?.serviceOrderCode ?? payload?.ServiceOrderCode ?? "").trim();
        const message = orderCode
          ? `Có đề xuất phát sinh mới cho lệnh ${orderCode}. Vui lòng lập báo giá bổ sung.`
          : "Có đề xuất phát sinh mới. Vui lòng lập báo giá bổ sung.";
        setSupplementaryToast({ orderId, orderCode, message });
        if (supplementaryToastTimerRef.current) clearTimeout(supplementaryToastTimerRef.current);
        supplementaryToastTimerRef.current = setTimeout(() => {
          setSupplementaryToast(null);
          supplementaryToastTimerRef.current = null;
        }, 8000);
        fetchData();
      }
    };

    connection.on("SupplementaryProposalUpdated", handleSupplementaryUpdated);

    connection
      .start()
      .catch(() => {
        // realtime is best-effort, polling/refresh still works.
      });

    return () => {
      isDisposed = true;
      connection.off("SupplementaryProposalUpdated", handleSupplementaryUpdated);
      connection.stop().catch(() => {});
    };
  }, [fetchData]);

  useEffect(() => () => {
    if (supplementaryToastTimerRef.current) {
      clearTimeout(supplementaryToastTimerRef.current);
      supplementaryToastTimerRef.current = null;
    }
  }, []);

  const getOrderCustomerName = useCallback((order) => {
    if (order.customerName) return order.customerName;
    const customer = customers.find((c) => Number(c.id) === Number(order.customerId));
    return customer?.fullName || "-";
  }, [customers]);

  const getOrderCustomerPhone = useCallback((order) => {
    if (order.customerPhone) return order.customerPhone;
    const customer = customers.find((c) => Number(c.id) === Number(order.customerId));
    return customer?.phone || "--";
  }, [customers]);

  const getOrderVehiclePlate = useCallback((order) => {
    if (order.vehicleLicensePlate) return order.vehicleLicensePlate;
    const vehicle = vehicles.find((v) => Number(v.id) === Number(order.vehicleId));
    return vehicle?.licensePlate || "N/A";
  }, [vehicles]);

  const orderStatusOptions = useMemo(() => {
    const statuses = Array.from(
      new Set((orders || []).map((order) => normalizeStatus(order.status)).filter(Boolean))
    );
    return statuses.sort((a, b) => a.localeCompare(b));
  }, [orders]);

  const filteredOrders = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    const queryCompact = query.replace(/[\s.-]/g, "");
    const startDate = toStartOfDay(dateFrom);
    const endDate = toEndOfDay(dateTo);

    const list = (orders || []).filter((order) => {
      const byStatus = orderStatusFilter === "ALL" || normalizeStatus(order.status) === orderStatusFilter;
      const createdAtTime = toApiDateMillis(order.createdAt);
      const byDate =
        (!startDate || createdAtTime >= startDate.getTime()) &&
        (!endDate || createdAtTime <= endDate.getTime());

      const plate = String(getOrderVehiclePlate(order) || "").toLowerCase();
      const plateCompact = plate.replace(/[\s.-]/g, "");
      const byQuery =
        !query ||
        String(order.code || "").toLowerCase().includes(query) ||
        getOrderCustomerName(order).toLowerCase().includes(query) ||
        String(getOrderCustomerPhone(order) || "").toLowerCase().includes(query) ||
        String(order.status || "").toLowerCase().includes(query) ||
        plate.includes(query) ||
        (queryCompact && plateCompact.includes(queryCompact));

      return byStatus && byDate && byQuery;
    });

    return list.sort((a, b) => {
      const createdA = toApiDateMillis(a.createdAt);
      const createdB = toApiDateMillis(b.createdAt);
      const checkInA = toApiDateMillis(a.checkInTime);
      const checkInB = toApiDateMillis(b.checkInTime);
      const scheduledA = toApiDateMillis(a.estimatedHandover);
      const scheduledB = toApiDateMillis(b.estimatedHandover);

      if (orderSortBy === "CREATED_ASC") {
        if (createdA !== createdB) return createdA - createdB;
      } else if (orderSortBy === "CHECKIN_DESC") {
        if (checkInB !== checkInA) return checkInB - checkInA;
      } else if (orderSortBy === "CHECKIN_ASC") {
        if (checkInA !== checkInB) return checkInA - checkInB;
      } else if (orderSortBy === "SCHEDULE_DESC") {
        if (scheduledB !== scheduledA) return scheduledB - scheduledA;
      } else if (createdB !== createdA) {
        return createdB - createdA;
      }

      return Number(b.id || 0) - Number(a.id || 0);
    });
  }, [orders, searchQuery, getOrderCustomerName, getOrderCustomerPhone, getOrderVehiclePlate, orderStatusFilter, orderSortBy, dateFrom, dateTo]);

  const singleOrders = useMemo(
    () => filteredOrders.filter((order) => Number(order.groupSessionId ?? order.GroupSessionId ?? 0) <= 0),
    [filteredOrders]
  );

  const groupedOrders = useMemo(() => {
    const map = new Map();

    filteredOrders.forEach((order, index) => {
      const groupSessionId = Number(order.groupSessionId ?? order.GroupSessionId ?? 0);
      if (!Number.isFinite(groupSessionId) || groupSessionId <= 0) return;

      if (!map.has(groupSessionId)) {
        map.set(groupSessionId, {
          id: groupSessionId,
          groupCode: `GS-${groupSessionId}`,
          orders: [],
          firstIndex: index,
        });
      }

      const group = map.get(groupSessionId);
      group.orders.push(order);
      if (index < group.firstIndex) group.firstIndex = index;
    });

    return Array.from(map.values())
      .map((group) => {
        const customerName =
          group.orders
            .map((order) => getOrderCustomerName(order))
            .find((name) => String(name || "").trim() && String(name || "").trim() !== "-") || "-";

        const customerPhone =
          group.orders
            .map((order) => getOrderCustomerPhone(order))
            .find((phone) => String(phone || "").trim() && String(phone || "").trim() !== "--") || "--";

        const statuses = group.orders.map((order) => normalizeStatus(order.status)).filter(Boolean);

        return {
          ...group,
          customerName,
          customerPhone,
          status: resolveGroupStatus(statuses),
        };
      })
      .sort((a, b) => a.firstIndex - b.firstIndex);
  }, [filteredOrders, getOrderCustomerName, getOrderCustomerPhone]);

  const pagedSingleOrders = useMemo(() => {
    const start = (ordersPage - 1) * ordersPageSize;
    return singleOrders.slice(start, start + ordersPageSize);
  }, [singleOrders, ordersPage, ordersPageSize]);

  const pagedGroupedOrders = useMemo(() => {
    const start = (ordersPage - 1) * ordersPageSize;
    return groupedOrders.slice(start, start + ordersPageSize);
  }, [groupedOrders, ordersPage, ordersPageSize]);

  const ordersTotalItems = orderViewMode === ORDER_VIEW_MODE.GROUP ? groupedOrders.length : singleOrders.length;

  useEffect(() => {
    setOrdersPage(1);
  }, [orderViewMode, orderStatusFilter, orderSortBy, dateFrom, dateTo, searchQuery]);

  const activeOrdersCount = useMemo(
    () => orders.filter((order) => !["COMPLETED", "CANCELED", "CANCELED"].includes(normalizeStatus(order.status))).length,
    [orders]
  );


  const statusPermitUpdate = useCallback(
    (status) => !["COMPLETED", "CANCELED", "CANCELED"].includes(normalizeStatus(status)),
    []
  );

  const handleCompleteOrder = useCallback(async (order) => {
    if (!statusPermitUpdate(order.status)) {
      alert("Lệnh này đã kết thúc hoặc đã hủy, không thể bàn giao.");
      return;
    }
    if (!window.confirm(`Xác nhận bàn giao xe ${order.code} cho kháchờ`)) return;

    setSaving(true);
    try {
      await serviceOrderApi.update(order.id, { ...order, status: "COMPLETED" });
      await fetchData();
    } catch (err) {
      setError("Lỗi khi bàn giao: " + err.message);
    } finally {
      setSaving(false);
    }
  }, [fetchData, statusPermitUpdate]);

  const handleCancelOrder = useCallback(async (order) => {
    if (["COMPLETED", "CANCEL_REQUESTED", "CANCELED"].includes(order.status)) {
      alert("Lệnh này không thể hủy ở trạng thái hiện tại.");
      return;
    }
    const reason = window.prompt(`Lý do hủy lệnh ${order.code}:`, "Khách hàng yêu cầu hủy");
    if (reason === null) return;
    if (!window.confirm("Bạn có chắc chắn muốn hủy lệnh này? Thao tác này không thể hoàn tác.")) return;

    setSaving(true);
    try {
      await serviceOrderApi.update(order.id, { ...order, status: "CANCELED" });
      await fetchData();
    } catch (err) {
      setError("Lỗi khi hủy lệnh: " + err.message);
    } finally {
      setSaving(false);
    }
  }, [fetchData]);

  const handleCancelGroupOrders = useCallback(async (group) => {
    const cancellableOrders = (group?.orders || []).filter((order) => statusPermitUpdate(order?.status));
    if (cancellableOrders.length === 0) return;

    const warningMessage = cancellableOrders.length === (group?.orders || []).length
      ? "Bạn có chắc chắn muốn hủy toàn bộ nhóm lệnh này không?"
      : "Nhóm này có một số lệnh không thể hủy theo trạng thái hiện tại. Bạn có chắc chắn muốn hủy các lệnh còn lại không?";
    if (!window.confirm(warningMessage)) return;

    const reasonInput = window.prompt(
      "Nhập lý do hủy (bắt buộc):",
      "Khách hàng yêu cầu hủy",
    );
    if (reasonInput === null) return;
    const reason = String(reasonInput || "").trim();
    if (!reason) {
      alert("Lý do hủy là bắt buộc.");
      return;
    }

    setActingGroup(group.id);
    setSaving(true);
    try {
      await Promise.all(
        cancellableOrders.map((order) => serviceOrderApi.update(order.id, { ...order, status: "CANCELED", cancelReason: reason })),
      );
      await fetchData();
    } catch (err) {
      setError("Lỗi khi hủy nhóm lệnh: " + (err?.message || "Không rõ lỗi"));
    } finally {
      setActingGroup(null);
      setSaving(false);
    }
  }, [fetchData, statusPermitUpdate]);

  useEffect(() => {
    const params = new URLSearchParams(location.search);
    const viewOrderId = Number(params.get("viewOrderId") || 0);
    if (!viewOrderId || !Array.isArray(orders) || orders.length === 0) return;
    const found = orders.find((order) => Number(order.id) === viewOrderId);
    if (!found) return;

    setSelectedOrderForView(found);
    setModal("order_detail");

    params.delete("viewOrderId");
    const nextSearch = params.toString();
    navigate(
      { pathname: location.pathname, search: nextSearch ? `?${nextSearch}` : "" },
      { replace: true }
    );
  }, [location.pathname, location.search, navigate, orders]);

  const openDateRangePicker = useCallback(() => {
    setModal("date_range");
  }, []);

  const applyDateRange = useCallback(({ dateFrom: nextDateFrom, dateTo: nextDateTo }) => {
    setDateFrom(nextDateFrom || "");
    setDateTo(nextDateTo || "");
    setModal(null);
  }, []);

  const defaultDateFilter = useMemo(() => toDateText(new Date()), []);
  const dateFilterButtonLabel = useMemo(
    () => getDateRangeButtonLabel(dateFrom, dateTo, { todayDateText: defaultDateFilter }),
    [dateFrom, dateTo, defaultDateFilter]
  );
  const canResetToolbarFilters = orderStatusFilter !== "ALL" || orderSortBy !== "CREATED_DESC" || Boolean(dateFrom || dateTo);

  const resetToolbarFilters = useCallback(() => {
    setOrderStatusFilter("ALL");
    setOrderSortBy("CREATED_DESC");
    setDateFrom("");
    setDateTo("");
  }, []);

  return (
    <div className="reception-dashboard">
      <div className="stats-bar">
        <div className="stat-card">
          <div className="stat-value">{activeOrdersCount}</div>
          <div className="stat-label">Xe đang ở xưởng</div>
        </div>
        <div className="stat-card">
          <div className="stat-value">{orders.filter((order) => ["NEW", "RECEPTION"].includes(order.status)).length}</div>
          <div className="stat-label">Mới tiếp nhận</div>
        </div>
        <div className="stat-card">
          <div className="stat-value" style={{ color: "#ffb547" }}>{orders.filter((order) => QUOTE_WAITING_STATUSES.includes(normalizeStatus(order.status))).length}</div>
          <div className="stat-label">Chờ báo giá</div>
        </div>
        <div className="stat-card">
          <div className="stat-value" style={{ color: "#05cd99" }}>{orders.filter((order) => order.status === "READY").length}</div>
          <div className="stat-label">Sẵn sàng bàn giao</div>
        </div>
      </div>


      {error && (
        <div style={{ padding: "12px", background: "#fee", color: "#e6522c", borderRadius: "8px", marginBottom: "16px", fontWeight: 600 }}>
          Lỗi: {error}
        </div>
      )}

      {supplementaryToast && (
        <div
          style={{
            marginBottom: 16,
            padding: "12px 14px",
            borderRadius: 10,
            border: "1px solid #f2d37b",
            background: "#fff9e8",
            color: "#6b4d00",
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            gap: 12,
            flexWrap: "wrap",
          }}
        >
          <span style={{ fontWeight: 600 }}>{supplementaryToast.message}</span>
          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
            {Number.isFinite(Number(supplementaryToast.orderId)) && Number(supplementaryToast.orderId) > 0 ? (
              <button
                type="button"
                className="toolbar-action-btn toolbar-action-outline"
                onClick={() => navigate(`/workshop/reception/orders/${supplementaryToast.orderId}/quote?intent=supplementary`)}
              >
                Mở báo giá PS
              </button>
            ) : null}
            <button
              type="button"
              className="toolbar-action-btn toolbar-action-ghost"
              onClick={() => setSupplementaryToast(null)}
            >
              Đóng
            </button>
          </div>
        </div>
      )}

      <OrdersTableContent
        navigate={navigate}
        onRefresh={fetchData}
        orderStatusFilter={orderStatusFilter}
        setOrderStatusFilter={setOrderStatusFilter}
        orderStatusOptions={orderStatusOptions}
        orderSortBy={orderSortBy}
        setOrderSortBy={setOrderSortBy}
        openDateRangePicker={openDateRangePicker}
        dateFilterButtonLabel={dateFilterButtonLabel}
        canResetToolbarFilters={canResetToolbarFilters}
        onResetToolbarFilters={resetToolbarFilters}
        filteredOrders={filteredOrders}
        singleOrders={singleOrders}
        groupedOrders={groupedOrders}
        orderViewMode={orderViewMode}
        setOrderViewMode={setOrderViewMode}
        loading={loading}
        pagedSingleOrders={pagedSingleOrders}
        pagedGroupedOrders={pagedGroupedOrders}
        setSelectedOrderForView={setSelectedOrderForView}
        setModal={setModal}
        getOrderCustomerName={getOrderCustomerName}
        getOrderCustomerPhone={getOrderCustomerPhone}
        getOrderVehiclePlate={getOrderVehiclePlate}
        statusPermitUpdate={statusPermitUpdate}
        handleCompleteOrder={handleCompleteOrder}
        handleCancelOrder={handleCancelOrder}
        handleCancelGroupOrders={handleCancelGroupOrders}
        actingGroup={actingGroup}
        ordersPage={ordersPage}
        ordersPageSize={ordersPageSize}
        ordersTotalItems={ordersTotalItems}
        setOrdersPage={setOrdersPage}
        setOrdersPageSize={setOrdersPageSize}
        getStatusBadge={getStatusBadge}
      />

      {modal === "date_range" && (
        <DateRangePickerModal
          isOpen={modal === "date_range"}
          onClose={() => setModal(null)}
          initialDateFrom={dateFrom}
          initialDateTo={dateTo}
          onApply={applyDateRange}
        />
      )}

      {modal === "order_detail" && selectedOrderForView && (
        <ServiceOrderDetailModal
          order={selectedOrderForView}
          customers={customers}
          vehicles={vehicles}
          staff={staff}
          onRefresh={fetchData}
          onClose={() => { setModal(null); setSelectedOrderForView(null); }}
        />
      )}
    </div>
  );
}
function OrdersTableContent({
  navigate,
  onRefresh,
  orderStatusFilter,
  setOrderStatusFilter,
  orderStatusOptions,
  orderSortBy,
  setOrderSortBy,
  openDateRangePicker,
  dateFilterButtonLabel,
  canResetToolbarFilters,
  onResetToolbarFilters,
  filteredOrders,
  singleOrders,
  groupedOrders,
  orderViewMode,
  setOrderViewMode,
  loading,
  pagedSingleOrders,
  pagedGroupedOrders,
  setSelectedOrderForView,
  setModal,
  getOrderCustomerName,
  getOrderCustomerPhone,
  getOrderVehiclePlate,
  statusPermitUpdate,
  handleCompleteOrder,
  handleCancelOrder,
  handleCancelGroupOrders,
  actingGroup,
  ordersPage,
  ordersPageSize,
  ordersTotalItems,
  setOrdersPage,
  setOrdersPageSize,
  getStatusBadge,
}) {
  const orderFilterOptions = [toStatusOptionVi("ALL"), ...orderStatusOptions.map(toStatusOptionVi)];
  const orderSortOptions = [
    { value: "CREATED_DESC", label: "Mới nhất" },
    { value: "CREATED_ASC", label: "Cũ nhất" },
    { value: "SCHEDULE_DESC", label: "Lịch hẹn mới nhất" },
    { value: "CHECKIN_DESC", label: "Check-in mới nhất" },
    { value: "CHECKIN_ASC", label: "Check-in cũ nhất" },
  ];

  const isGroupMode = orderViewMode === ORDER_VIEW_MODE.GROUP;

  const openOrderDetail = useCallback((order) => {
    setSelectedOrderForView(order);
    setModal("order_detail");
  }, [setModal, setSelectedOrderForView]);

  const renderOrderActionButtons = useCallback((order) => (
    <div style={{ display: "flex", gap: 8, justifyContent: "center", alignItems: "center", width: "100%" }}>
      {hasOrderSupplementaryQuoteProposal(order) ? (
        <button
          type="button"
          className="btn-icon btn-action-pill btn-action-outline"
          style={{
            width: "auto",
            minWidth: 90,
            margin: 0,
          }}
          title={"Mở báo giá bổ sung"}
          onClick={(event) => {
            event.stopPropagation();
            navigate(`/workshop/reception/orders/${order.id}/quote?intent=supplementary`);
          }}
        >
          Báo giá PS
        </button>
      ) : null}
      {normalizeStatus(order.status) === "READY" ? (
        <button
          className="btn-success btn-sm"
          onClick={(event) => {
            event.stopPropagation();
            handleCompleteOrder(order);
          }}
        >
          ✅ Bàn giao
        </button>
      ) : null}
      {statusPermitUpdate(order.status) ? (
        <button
          type="button"
          className="btn-icon btn-action-pill btn-action-cancel"
          style={{
            width: "auto",
            minWidth: 74,
            margin: 0,
          }}
          title={"Hủy lệnh"}
          onClick={(event) => {
            event.stopPropagation();
            handleCancelOrder(order);
          }}
        >
          {"Hủy"}
        </button>
      ) : null}
    </div>
  ), [handleCancelOrder, handleCompleteOrder, navigate, statusPermitUpdate]);

  return (
    <>
      <div className="workshop-global-actions" style={{ marginBottom: 14 }}>
        <button
          type="button"
          className="toolbar-action-btn toolbar-action-outline workshop-global-refresh-btn"
          onClick={onRefresh}
          disabled={loading}
        >
          🔄 Làm mới
        </button>
      </div>

      <div className="table-toolbar">
        <button className="btn-primary" onClick={() => navigate("/workshop/reception/create")}>+ Tiếp nhận xe mới</button>
        <div className="toolbar-controls">
          <FilterDropdown
            id="order-status-filter"
            label="Trạng thái"
            value={orderStatusFilter}
            options={orderFilterOptions}
            onChange={setOrderStatusFilter}
            minWidth={160}
          />
          <FilterDropdown
            id="order-sort"
            label="Sắp xếp"
            value={orderSortBy}
            options={orderSortOptions}
            onChange={setOrderSortBy}
            minWidth={220}
          />
          <button type="button" className="toolbar-action-btn toolbar-action-outline" onClick={openDateRangePicker}>
            {dateFilterButtonLabel || "Chọn khoảng ngày"}
          </button>
          <button type="button" className="toolbar-action-btn toolbar-action-ghost" onClick={onResetToolbarFilters} disabled={!canResetToolbarFilters}>
            Xóa lọc
          </button>
        </div>
        <span className="result-count">
          {isGroupMode ? `${groupedOrders.length} nhóm lệnh` : `${singleOrders.length} lệnh thường`}
        </span>
      </div>

      <p className="appointment-row-hint">Nhấn vào một dòng để xem chi tiết lệnh sửa chữa.</p>

      <div
        style={{
          display: "inline-grid",
          gridTemplateColumns: "repeat(2, minmax(0, 1fr))",
          gap: 8,
          background: "#edf3fb",
          border: "1px solid #d3e0f1",
          borderRadius: 12,
          padding: 6,
          marginBottom: 14,
        }}
      >
        <button
          type="button"
          className="toolbar-action-btn"
          style={{
            minWidth: 170,
            borderRadius: 10,
            border: orderViewMode === ORDER_VIEW_MODE.SINGLE ? "1px solid #c3d4ed" : "1px solid transparent",
            background: orderViewMode === ORDER_VIEW_MODE.SINGLE ? "#ffffff" : "transparent",
            color: orderViewMode === ORDER_VIEW_MODE.SINGLE ? "#173c73" : "#446286",
            boxShadow: orderViewMode === ORDER_VIEW_MODE.SINGLE ? "0 3px 10px rgba(23, 60, 115, 0.12)" : "none",
          }}
          onClick={() => setOrderViewMode(ORDER_VIEW_MODE.SINGLE)}
        >
          Lệnh thường ({singleOrders.length})
        </button>
        <button
          type="button"
          className="toolbar-action-btn"
          style={{
            minWidth: 170,
            borderRadius: 10,
            border: orderViewMode === ORDER_VIEW_MODE.GROUP ? "1px solid #c3d4ed" : "1px solid transparent",
            background: orderViewMode === ORDER_VIEW_MODE.GROUP ? "#ffffff" : "transparent",
            color: orderViewMode === ORDER_VIEW_MODE.GROUP ? "#173c73" : "#446286",
            boxShadow: orderViewMode === ORDER_VIEW_MODE.GROUP ? "0 3px 10px rgba(23, 60, 115, 0.12)" : "none",
          }}
          onClick={() => setOrderViewMode(ORDER_VIEW_MODE.GROUP)}
        >
          Lệnh nhóm ({groupedOrders.length})
        </button>
      </div>

      <div className="workshop-table-card">
        {loading ? (
          <div className="loading-state"><div className="spinner" /><p>⏳ Đang tải danh sách lệnh...</p></div>
        ) : filteredOrders.length === 0 ? (
          <table className="admin-table">
            <tbody>
              <tr>
                <td colSpan="7" style={{ textAlign: "center", padding: "40px", color: "#a3aed0" }}>
                  Chưa có lệnh sửa chữa nào.
                </td>
              </tr>
            </tbody>
          </table>
        ) : isGroupMode ? (
          groupedOrders.length === 0 ? (
            <div style={{ textAlign: "center", padding: 40, color: "#a3aed0" }}>Không có lệnh nhóm nào.</div>
          ) : (
            <div style={{ display: "grid", gap: 16, padding: 14 }}>
              {pagedGroupedOrders.map((group) => {
                const hasCancelableOrders = (group.orders || []).some((order) => statusPermitUpdate(order.status));
                const groupActionDisabled = actingGroup === group.id || !hasCancelableOrders;
                return (
                  <article
                    key={group.id}
                    style={{
                      border: "2px solid #c3d6f2",
                      borderRadius: 14,
                      background: "#f5f9ff",
                      boxShadow: "0 6px 20px rgba(23, 60, 115, 0.08)",
                      padding: 14,
                    }}
                  >
                    <header
                      style={{
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "center",
                        gap: 12,
                        marginBottom: 12,
                        paddingBottom: 10,
                        borderBottom: "1px solid #d9e6fb",
                        flexWrap: "wrap",
                      }}
                    >
                      <div>
                        <h4 style={{ margin: 0, color: "#17325b" }}>{group.groupCode || `GS-${group.id}`}</h4>
                        <p style={{ margin: "4px 0 0", fontSize: 13, color: "#5b6f8c" }}>
                          {group.customerName || "Khách hàng"} - {group.customerPhone || "--"} - {group.orders.length} lệnh
                        </p>
                      </div>
                      <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                        <span className={`badge ${getStatusBadge(group.status)}`} style={{ height: 30, display: "inline-flex", alignItems: "center" }}>
                          {getStatusLabelVi(group.status, group.status)}
                        </span>
                        <button
                          className={`btn-icon btn-action-pill btn-action-cancel ${groupActionDisabled ? "is-disabled-action" : ""}`}
                          title={"Hủy nhóm lệnh"}
                          style={{
                            height: 30,
                            display: "inline-flex",
                            alignItems: "center",
                            justifyContent: "center",
                            lineHeight: 1,
                            margin: 0,
                          }}
                          disabled={groupActionDisabled}
                          onClick={(event) => {
                            event.stopPropagation();
                            handleCancelGroupOrders(group);
                          }}
                        >
                          Hủy
                        </button>
                      </div>
                    </header>

                    <div
                      style={{
                        overflowX: "auto",
                        border: "1px solid #dbe7f8",
                        borderRadius: 12,
                        background: "#ffffff",
                      }}
                    >
                      <table className="admin-table reception-group-orders-table">
                        <colgroup>
                          <col className="col-order-code" />
                          <col className="col-plate" />
                          <col className="col-checkin" />
                          <col className="col-handover" />
                          <col className="col-status" />
                          <col className="col-actions" />
                        </colgroup>
                        <thead>
                          <tr>
                            <th>Mã lệnh</th>
                            <th>Biển số</th>
                            <th>Thời điểm vào</th>
                            <th>Hẹn trả</th>
                            <th className="th-center col-status-fixed">
                              <span className="status-col-label">Trạng thái</span>
                            </th>
                            <th className="col-actions col-actions-compact th-center">Thao tác</th>
                          </tr>
                        </thead>
                        <tbody>
                          {group.orders.map((order) => {
                            const rowInteraction = buildOrderRowInteraction(order, openOrderDetail);
                            return (
                              <tr key={order.id} className="appointment-click-row" {...rowInteraction}>
                                <td className="td-id">{order.code}</td>
                                <td>{getOrderVehiclePlate(order)}</td>
                                <td>
                                  {order.checkInTime
                                    ? `${formatOrderDate(order.checkInTime)} ${formatOrderTime(order.checkInTime)}`
                                    : "-"}
                                </td>
                                <td>
                                  {order.estimatedHandover
                                    ? `${formatOrderDate(order.estimatedHandover)} ${formatOrderTime(order.estimatedHandover)}`
                                    : "Chưa hẹn"}
                                </td>
                                <td className="th-center col-status-fixed">
                                  <div className="appointment-status-cell">
                                    <span className="appointment-status-main">
                                      <span className={`badge ${getStatusBadge(order.status)}`}>
                                        {getStatusLabelVi(order.status, order.status)}
                                      </span>
                                      <OrderIssueIndicator order={order} />
                                    </span>
                                  </div>
                                </td>
                                <td className="col-actions col-actions-compact th-center" onClick={(event) => event.stopPropagation()}>
                                  {renderOrderActionButtons(order)}
                                </td>
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                    </div>
                  </article>
                );
              })}
            </div>
          )
        ) : (
          <table className="admin-table">
            <thead>
              <tr>
                <th>Mã lệnh</th>
                <th>Khách hàng</th>
                <th>SĐT</th>
                <th>Biển số</th>
                <th className="th-center col-status-fixed">
                  <span className="status-col-label">Trạng thái</span>
                </th>
                <th>Thời điểm vào</th>
                <th>Hẹn trả</th>
                <th className="col-actions col-actions-compact th-center">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {singleOrders.length === 0 ? (
                <tr>
                  <td colSpan="8" style={{ textAlign: "center", padding: "40px", color: "#a3aed0" }}>
                    Không có lệnh thường nào.
                  </td>
                </tr>
              ) : (
                pagedSingleOrders.map((order) => {
                  const rowInteraction = buildOrderRowInteraction(order, openOrderDetail);
                  return (
                    <tr key={order.id} className="appointment-click-row" {...rowInteraction}>
                      <td className="td-id">{order.code}</td>
                      <td style={{ fontWeight: 600 }}>{getOrderCustomerName(order)}</td>
                      <td>{getOrderCustomerPhone(order)}</td>
                      <td>{getOrderVehiclePlate(order)}</td>
                      <td className="th-center col-status-fixed">
                        <div className="appointment-status-cell">
                          <span className="appointment-status-main">
                            <span className={`badge ${getStatusBadge(order.status)}`}>
                              {getStatusLabelVi(order.status, order.status)}
                            </span>
                            <OrderIssueIndicator order={order} />
                          </span>
                        </div>
                      </td>
                      <td>
                        {order.checkInTime
                          ? `${formatOrderDate(order.checkInTime)} ${formatOrderTime(order.checkInTime)}`
                          : "-"}
                      </td>
                      <td>
                        {order.estimatedHandover
                          ? `${formatOrderDate(order.estimatedHandover)} ${formatOrderTime(order.estimatedHandover)}`
                          : "Chưa hẹn"}
                      </td>
                      <td className="col-actions col-actions-compact th-center" onClick={(event) => event.stopPropagation()}>
                        {renderOrderActionButtons(order)}
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        )}
      </div>
      <TablePagination
        totalItems={ordersTotalItems}
        currentPage={ordersPage}
        pageSize={ordersPageSize}
        onPageChange={setOrdersPage}
        onPageSizeChange={(size) => {
          setOrdersPageSize(size);
          setOrdersPage(1);
        }}
      />
    </>
  );
}




