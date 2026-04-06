import { useState, useEffect, useCallback, useMemo } from "react";
import Calendar from "react-calendar";
import "react-calendar/dist/Calendar.css";
import { useSearch } from "@context/SearchContext";
import { useBranch } from "@shared/core/context/BranchContext";
import FilterDropdown from "@shared/components/FilterDropdown";
import { getStatusLabelVi, toStatusOptionVi } from "@shared/core/utils/statusLabelUtils";
import { Modal } from "@features/admin/components/AdminModals";
import AppointmentDetailModal from "@features/workshop/components/AppointmentDetailModal";
import "./AppointmentManagementPage.css";
import {
  getAllAppointments,
  getBranchAppointments,
  getAppointmentDetail,
  confirmAppointment,
  cancelAppointment,
  checkInAppointment,
  getAllStandardServices,
} from "@features/appointments/api/appointmentApi";
import {
  getAllCustomers,
  getAllVehicles,
  getAllCustomerVehicles,
} from "@features/admin/api/customerApi";
import AppointmentForm from "@features/workshop/components/AppointmentForm";
import { saveReceptionAppointment } from "@features/workshop/utils/receptionAppointmentBooking";

const STATUS_LABELS = {
  PENDING: "Chờ xác nhận",
  LATE: "Trễ hẹn",
  CONFIRMED: "Đã xác nhận",
  ARRIVED: "Đã đến",
  ARRIVED_LATE: "Đến trễ",
  CHECKED_IN: "Đã check-in",
  CANCELED: "Đã hủy",
  CANCELLED: "Đã hủy",
  NO_SHOW: "Không đến",
  COMPLETED: "Hoàn tất",
};

const GROUP_STATUS_LABELS = {
  PENDING: "Đang chờ",
  PARTIAL_CONFIRMED: "Xác nhận một phần",
  CONFIRMED: "Đã xác nhận",
  COMPLETED: "Đã hoàn tất",
  CANCELED: "Đã hủy",
  CANCELLED: "Đã hủy",
};

const STATUS_FILTER_OPTIONS = [
  "ALL",
  "PENDING",
  "LATE",
  "CONFIRMED",
  "ARRIVED",
  "ARRIVED_LATE",
  "CANCELED",
  "NO_SHOW",
  "COMPLETED",
].map(toStatusOptionVi);

const SORT_OPTIONS = [
  { value: "NEWEST", label: "Mới nhất" },
  { value: "OLDEST", label: "Cũ nhất" },
  { value: "NAME_ASC", label: "Tên khách A-Z" },
  { value: "NAME_DESC", label: "Tên khách Z-A" },
];

const VIEW_MODES = { SINGLE: "SINGLE", GROUP: "GROUP" };

function extractCollection(payload) {
  if (Array.isArray(payload)) return payload;
  if (Array.isArray(payload?.items)) return payload.items;
  if (Array.isArray(payload?.data)) return payload.data;
  if (Array.isArray(payload?.result)) return payload.result;
  if (Array.isArray(payload?.$values)) return payload.$values;
  return [];
}

function pick(item, ...keys) {
  for (const key of keys) {
    const value = item?.[key];
    if (value !== undefined && value !== null) return value;
  }
  return null;
}

function normalizeStatus(value) {
  return String(value || "").trim().toUpperCase();
}

function toDateTime(value) {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
}

function toTimestamp(value) {
  return toDateTime(value)?.getTime() ?? 0;
}

function formatDate(value, fallback = "") {
  const date = toDateTime(value);
  if (!date) return fallback || "--";
  return date.toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" });
}

function formatTime(value) {
  if (!value) return "--";
  if (typeof value === "string") {
    const hhmmPrefix = value.match(/^(\d{2}:\d{2})/);
    if (hhmmPrefix) return hhmmPrefix[1];
    const hhmmIso = value.match(/T(\d{2}:\d{2})/);
    if (hhmmIso) return hhmmIso[1];
  }
  const date = toDateTime(value);
  if (!date) return "--";
  return date.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
}

function formatTimeRange(startTime, endTime) {
  const start = formatTime(startTime);
  const end = formatTime(endTime);
  if (start === "--" && end === "--") return "--";
  if (end === "--") return start;
  if (start === "--") return end;
  return `${start} - ${end}`;
}

function toDateText(dateObj) {
  if (!(dateObj instanceof Date) || Number.isNaN(dateObj.getTime())) return "";
  const y = dateObj.getFullYear();
  const m = String(dateObj.getMonth() + 1).padStart(2, "0");
  const d = String(dateObj.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

function toDisplayDate(dateText) {
  if (!dateText) return "--/--/----";
  const d = new Date(`${dateText}T00:00:00`);
  if (Number.isNaN(d.getTime())) return "--/--/----";
  return d.toLocaleDateString("vi-VN");
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

function normalizeAppointment(item) {
  const startTime = pick(item, "startTime", "StartTime");
  const endTime = pick(item, "endTime", "EndTime");
  const scheduledDate = pick(item, "scheduledDate", "ScheduledDate");
  return {
    id: pick(item, "id", "Id"),
    branchId: Number(pick(item, "branchId", "BranchId")) || 0,
    customerId: Number(pick(item, "customerId", "CustomerId")) || 0,
    customerName: pick(item, "customerName", "CustomerName") || "-",
    customerPhone: pick(item, "customerPhone", "CustomerPhone") || "",
    branchName: pick(item, "branchName", "BranchName") || "",
    scheduledDate: scheduledDate || formatDate(startTime, ""),
    startTime,
    endTime,
    vehicleLicensePlate: pick(item, "vehicleLicensePlate", "VehicleLicensePlate") || "",
    vehicleId: Number(pick(item, "vehicleId", "VehicleId")) || 0,
    vehicleModelName: pick(item, "vehicleModelName", "VehicleModelName") || "",
    requestedServiceIds: pick(item, "requestedServiceIds", "RequestedServiceIds") || "",
    serviceNames: Array.isArray(item?.serviceNames)
      ? item.serviceNames
      : Array.isArray(item?.ServiceNames)
        ? item.ServiceNames
        : [],
    customerNotes: pick(item, "customerNotes", "CustomerNotes") || "",
    status: normalizeStatus(pick(item, "status", "Status")),
    cancelReason: pick(item, "cancelReason", "CancelReason") || "",
    createdAt: pick(item, "createdAt", "CreatedAt"),
    GroupSessionId: pick(item, "GroupSessionId", "GroupSessionId"),
    GroupSessionCode: pick(item, "GroupSessionCode", "GroupSessionCode") || "",
    GroupSessionStatus: normalizeStatus(pick(item, "GroupSessionStatus", "GroupSessionStatus")),
  };
}

function badgeClass(status) {
  const map = {
    PENDING: "badge-warning",
    LATE: "badge-warning",
    CONFIRMED: "badge-active",
    ARRIVED: "badge-info",
    ARRIVED_LATE: "badge-info",
    CHECKED_IN: "badge-info",
    CANCELED: "badge-inactive",
    CANCELLED: "badge-inactive",
    NO_SHOW: "badge-inactive",
    COMPLETED: "badge-active",
  };
  return map[status] || "";
}

function groupBadgeClass(status) {
  const map = {
    PENDING: "badge-warning",
    PARTIAL_CONFIRMED: "badge-info",
    CONFIRMED: "badge-active",
    COMPLETED: "badge-active",
    CANCELED: "badge-inactive",
    CANCELLED: "badge-inactive",
  };
  return map[status] || "badge-info";
}

function canConfirmAppointment(status) {
  const normalized = normalizeStatus(status);
  return normalized === "PENDING" || normalized === "LATE";
}

function canCheckInAppointment(status) {
  return normalizeStatus(status) === "CONFIRMED";
}

function canCancelAppointment(status) {
  const normalized = normalizeStatus(status);
  return normalized === "PENDING" || normalized === "LATE" || normalized === "CONFIRMED";
}

function canEditAppointment(status) {
  const normalized = normalizeStatus(status);
  return normalized === "PENDING" || normalized === "CONFIRMED" || normalized === "LATE";
}

function buildRowInteraction(appt, onAppointmentClick) {
  if (!onAppointmentClick) return {};

  const openDetail = () => onAppointmentClick(appt);
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

export default function AppointmentManagementPage() {
  const { searchQuery } = useSearch();
  const { selectedBranchId } = useBranch();

  const [appointments, setAppointments] = useState([]);
  const [viewMode, setViewMode] = useState(VIEW_MODES.SINGLE);
  const [loading, setLoading] = useState(true);
  const [statusFilter, setStatusFilter] = useState("ALL");
  const [sortBy, setSortBy] = useState("NEWEST");
  const [dateFrom, setDateFrom] = useState(() => toDateText(new Date()));
  const [dateTo, setDateTo] = useState(() => toDateText(new Date()));
  const [draftDateFrom, setDraftDateFrom] = useState(() => toDateText(new Date()));
  const [draftDateTo, setDraftDateTo] = useState(() => toDateText(new Date()));
  const [showDateRangeModal, setShowDateRangeModal] = useState(false);
  const [acting, setActing] = useState(null);
  const [error, setError] = useState("");
  const [detailModal, setDetailModal] = useState(null);
  const [editingAppt, setEditingAppt] = useState(null);
  const [saving, setSaving] = useState(false);
  const [editPreparing, setEditPreparing] = useState(false);
  const [customers, setCustomers] = useState([]);
  const [vehicles, setVehicles] = useState([]);
  const [customerVehicles, setCustomerVehicles] = useState([]);
  const [services, setServices] = useState([]);

  const effectiveBranchId = useMemo(() => {
    const parsed = Number(selectedBranchId);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
  }, [selectedBranchId]);

  const showBranchColumn = effectiveBranchId === 0;

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const statusParam = statusFilter === "ALL" ? undefined : statusFilter;
      const payload = effectiveBranchId
        ? await getBranchAppointments(effectiveBranchId, statusParam)
        : await getAllAppointments(statusParam);
      const normalized = extractCollection(payload).map(normalizeAppointment).filter((item) => item.id !== undefined && item.id !== null);
      setAppointments(normalized);
    } catch {
      setError("Không thể tải danh sách lịch hẹn.");
    } finally {
      setLoading(false);
    }
  }, [effectiveBranchId, statusFilter]);

  const fetchReferenceData = useCallback(async () => {
    if (customers.length > 0 && vehicles.length > 0 && customerVehicles.length > 0 && services.length > 0) {
      return;
    }

    const [customerRes, vehicleRes, customerVehicleRes, serviceRes] = await Promise.all([
      getAllCustomers(),
      getAllVehicles(),
      getAllCustomerVehicles(),
      getAllStandardServices(),
    ]);

    setCustomers(Array.isArray(customerRes) ? customerRes : []);
    setVehicles(Array.isArray(vehicleRes) ? vehicleRes : []);
    setCustomerVehicles(Array.isArray(customerVehicleRes) ? customerVehicleRes : []);
    setServices(Array.isArray(serviceRes) ? serviceRes : []);
  }, [customers.length, vehicles.length, customerVehicles.length, services.length]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  async function handleAction(id, action) {
    setActing(id);
    setError("");
    try {
      if (action === "confirm") await confirmAppointment(id);
      else if (action === "cancel") await cancelAppointment(id, "Nhân viên hủy");
      else if (action === "checkin") await checkInAppointment(id, { arrivedAt: new Date().toISOString() });
      await fetchData();
    } catch (err) {
      setError(err?.message || "Thao tác thất bại.");
    } finally {
      setActing(null);
    }
  }

  const buildEditInitialValues = useCallback((appointment) => {
    if (!appointment) return null;
    return {
      customerId: Number(appointment.customerId || 0) || "",
      customerName: appointment.customerName || "",
      customerPhone: appointment.customerPhone || "",
      selectedVehicleIds: Number(appointment.vehicleId || 0) > 0 ? [Number(appointment.vehicleId)] : [],
      startTime: appointment.startTime || appointment.StartTime || "",
      requestedServiceIds: appointment.requestedServiceIds || appointment.RequestedServiceIds || "",
      customerNotes: appointment.customerNotes || appointment.CustomerNotes || "",
    };
  }, []);

  const closeEditApptModal = useCallback(() => {
    setEditingAppt(null);
    setEditPreparing(false);
  }, []);

  const handleOpenApptEdit = useCallback(async (appointment) => {
    const base = normalizeAppointment(appointment || detailModal);
    if (!base?.id) return;
    if (!canEditAppointment(base.status)) {
      setError("Không thể sửa lịch khi xe đã đến hoặc lịch đã kết thúc.");
      return;
    }

    setError("");
    setEditPreparing(true);
    try {
      await fetchReferenceData();
      const detail = await getAppointmentDetail(base.id);
      setEditingAppt(normalizeAppointment(detail));
      setDetailModal(null);
    } catch (err) {
      setError(err?.message || "Không thể tải dữ liệu để chỉnh sửa lịch hẹn.");
    } finally {
      setEditPreparing(false);
    }
  }, [detailModal, fetchReferenceData]);

  const handleSaveEditedAppointment = useCallback(async (formData) => {
    if (!editingAppt?.id) return;

    const branchForSave = Number(editingAppt.branchId || effectiveBranchId);
    if (!branchForSave) {
      setError("Không xác định được chi nhánh của lịch hẹn để lưu chỉnh sửa.");
      return;
    }

    setSaving(true);
    setError("");
    try {
      await saveReceptionAppointment({
        effectiveBranchId: branchForSave,
        formData,
        customers,
        vehicles,
        customerVehicles,
        services,
        replaceAppointmentId: editingAppt.id,
      });
      await fetchData();
      closeEditApptModal();
    } catch (err) {
      setError(err?.message || "Không thể cập nhật lịch hẹn.");
    } finally {
      setSaving(false);
    }
  }, [editingAppt, effectiveBranchId, customers, vehicles, customerVehicles, services, fetchData, closeEditApptModal]);

  const query = String(searchQuery || "").trim().toLowerCase();
  const queryCompact = query.replace(/[\s.-]/g, "");

  const filtered = useMemo(() => {
    const startDate = toStartOfDay(dateFrom);
    const endDate = toEndOfDay(dateTo);

    const list = appointments.filter((a) => {
      const byStatus = statusFilter === "ALL" || normalizeStatus(a.status) === statusFilter;
      const scheduled = toDateTime(a.startTime || a.scheduledDate)?.getTime() ?? 0;
      const byDate = (!startDate || scheduled >= startDate.getTime()) && (!endDate || scheduled <= endDate.getTime());
      if (!query) return byStatus && byDate;

      const plate = String(a.vehicleLicensePlate || "").toLowerCase();
      const plateCompact = plate.replace(/[\s.-]/g, "");
      const searchable = [
        a.customerName,
        a.customerPhone,
        a.vehicleLicensePlate,
        a.vehicleModelName,
        a.branchName,
        a.GroupSessionCode,
        Array.isArray(a.serviceNames) ? a.serviceNames.join(" ") : "",
      ].filter(Boolean).join(" ").toLowerCase();

      const byQuery = searchable.includes(query) || plate.includes(query) || (queryCompact && plateCompact.includes(queryCompact));
      return byStatus && byDate && byQuery;
    });

    return list.sort((a, b) => {
      const aTime = toTimestamp(a.startTime || a.scheduledDate);
      const bTime = toTimestamp(b.startTime || b.scheduledDate);
      if (sortBy === "OLDEST") return aTime - bTime;
      if (sortBy === "NAME_ASC") return String(a.customerName || "").localeCompare(String(b.customerName || ""), "vi");
      if (sortBy === "NAME_DESC") return String(b.customerName || "").localeCompare(String(a.customerName || ""), "vi");
      return bTime - aTime;
    });
  }, [appointments, dateFrom, dateTo, query, queryCompact, sortBy, statusFilter]);

  const groupedViewData = useMemo(() => {
    const grouped = new Map();
    const standaloneAppointments = [];

    for (const appt of filtered) {
      const hasGroupId = appt.GroupSessionId !== null && appt.GroupSessionId !== undefined;
      const hasGroupCode = Boolean(String(appt.GroupSessionCode || "").trim());
      if (!hasGroupId && !hasGroupCode) {
        standaloneAppointments.push(appt);
        continue;
      }

      const key = hasGroupId ? `id:${appt.GroupSessionId}` : `code:${String(appt.GroupSessionCode).trim()}`;
      if (!grouped.has(key)) {
        grouped.set(key, {
          key,
          GroupSessionId: hasGroupId ? appt.GroupSessionId : null,
          GroupSessionCode: String(appt.GroupSessionCode || "").trim(),
          GroupSessionStatus: appt.GroupSessionStatus,
          branchName: appt.branchName || "",
          customerName: appt.customerName || "",
          appointments: [],
        });
      }

      const group = grouped.get(key);
      if (!group.GroupSessionCode && appt.GroupSessionCode) group.GroupSessionCode = String(appt.GroupSessionCode).trim();
      if (!group.GroupSessionStatus && appt.GroupSessionStatus) group.GroupSessionStatus = appt.GroupSessionStatus;
      if (!group.branchName && appt.branchName) group.branchName = appt.branchName;
      if (!group.customerName && appt.customerName) group.customerName = appt.customerName;
      group.appointments.push(appt);
    }

    const groupedAppointments = Array.from(grouped.values())
      .map((group) => {
        const appts = [...group.appointments].sort((a, b) => toTimestamp(a.startTime || a.scheduledDate) - toTimestamp(b.startTime || b.scheduledDate));
        const first = appts[0];
        const last = appts[appts.length - 1];
        return {
          ...group,
          appointments: appts,
          startTime: first?.startTime || first?.scheduledDate || null,
          endTime: last?.endTime || last?.startTime || last?.scheduledDate || null,
          customerName: group.customerName || first?.customerName || "",
          confirmedCount: appts.filter((x) => ["CONFIRMED", "ARRIVED", "CHECKED_IN"].includes(x.status)).length,
          canceledCount: appts.filter((x) => ["CANCELED", "NO_SHOW"].includes(x.status)).length,
        };
      })
      .sort((a, b) => {
        if (sortBy === "OLDEST") return toTimestamp(a.startTime) - toTimestamp(b.startTime);
        if (sortBy === "NAME_ASC") return String(a.customerName || "").localeCompare(String(b.customerName || ""), "vi");
        if (sortBy === "NAME_DESC") return String(b.customerName || "").localeCompare(String(a.customerName || ""), "vi");
        return toTimestamp(b.startTime) - toTimestamp(a.startTime);
      });

    return { groupedAppointments, standaloneAppointments };
  }, [filtered, sortBy]);

  useEffect(() => {
    if (loading) return;
    if (viewMode === VIEW_MODES.GROUP && groupedViewData.groupedAppointments.length === 0) {
      setViewMode(VIEW_MODES.SINGLE);
    }
  }, [groupedViewData.groupedAppointments.length, loading, viewMode]);

  const resultCountText = useMemo(() => {
    if (viewMode === VIEW_MODES.SINGLE) return `${filtered.length} lịch hẹn`;
    const groupsCount = groupedViewData.groupedAppointments.length;
    return `${groupsCount} nhóm lịch hẹn`;
  }, [filtered.length, groupedViewData.groupedAppointments.length, viewMode]);

  const defaultDateFilter = useMemo(() => toDateText(new Date()), []);
  const isDefaultTodayDateFilter = dateFrom === defaultDateFilter && dateTo === defaultDateFilter;
  const dateFilterButtonLabel = isDefaultTodayDateFilter ? "Hôm nay" : "Chọn khoảng ngày";
  const canResetToolbarFilters = statusFilter !== "ALL" || sortBy !== "NEWEST" || !isDefaultTodayDateFilter;

  const resetToolbarFilters = () => {
    setStatusFilter("ALL");
    setSortBy("NEWEST");
    setDateFrom(defaultDateFilter);
    setDateTo(defaultDateFilter);
    setDraftDateFrom(defaultDateFilter);
    setDraftDateTo(defaultDateFilter);
  };

  const openDateRangePicker = () => {
    setDraftDateFrom(dateFrom);
    setDraftDateTo(dateTo);
    setShowDateRangeModal(true);
  };

  const applyDateRange = () => {
    setDateFrom(draftDateFrom || "");
    setDateTo(draftDateTo || "");
    setShowDateRangeModal(false);
  };

  const quickSetDateRange = (days) => {
    const now = new Date();
    const to = now.toISOString().slice(0, 10);
    const fromDate = new Date(now);
    fromDate.setDate(now.getDate() - (days - 1));
    const from = fromDate.toISOString().slice(0, 10);
    setDraftDateFrom(from);
    setDraftDateTo(to);
  };

  const handleRangeDayClick = (dateObj) => {
    const picked = toDateText(dateObj);
    if (!picked) return;
    if (!draftDateFrom || (draftDateFrom && draftDateTo)) {
      setDraftDateFrom(picked);
      setDraftDateTo("");
      return;
    }
    if (picked < draftDateFrom) return;
    setDraftDateTo(picked);
  };

  const tileClassName = ({ date, view }) => {
    if (view !== "month") return "";
    const text = toDateText(date);
    if (!text || !draftDateFrom) return "";
    if (text === draftDateFrom) return "range-start";
    if (draftDateTo && text === draftDateTo) return "range-end";
    if (draftDateTo && text > draftDateFrom && text < draftDateTo) return "range-middle";
    return "";
  };

  const renderTableHead = () => (
    <thead>
      <tr>
        <th>Khách hàng</th>
        <th>SĐT</th>
        {showBranchColumn && <th>Chi nhánh</th>}
        <th>Ngày</th>
        <th>Giờ</th>
        <th>Xe</th>
        <th className="th-center">Trạng thái</th>
        <th className="th-center action-col-header">Thao tác</th>
      </tr>
    </thead>
  );

  const renderAppointmentRow = (a, rowKeyPrefix = "") => {
    const canConfirm = canConfirmAppointment(a.status);
    const canCheckIn = canCheckInAppointment(a.status);
    const canCancel = canCancelAppointment(a.status);
    const rowInteraction = buildRowInteraction(a, setDetailModal);

    return (
      <tr key={`${rowKeyPrefix}${a.id}`} className="appointment-click-row" {...rowInteraction}>
        <td className="td-name">{a.customerName}</td>
        <td>{a.customerPhone || "-"}</td>
        {showBranchColumn && <td>{a.branchName || "-"}</td>}
        <td>{formatDate(a.startTime, a.scheduledDate || "--")}</td>
        <td>{formatTimeRange(a.startTime, a.endTime)}</td>
        <td>
          {a.vehicleLicensePlate || "-"}
          {a.vehicleModelName && <span style={{ fontSize: 11, color: "var(--mgmt-text-muted)", marginLeft: 4 }}>({a.vehicleModelName})</span>}
        </td>
        <td className="td-center"><span className={`badge ${badgeClass(a.status)}`}>{STATUS_LABELS[a.status] || getStatusLabelVi(a.status, a.status)}</span></td>
        <td className="action-col-cell">
          <div className="row-actions row-actions-balanced">
            <button
              className={`btn-icon btn-action-pill btn-action-confirm ${!canConfirm ? "is-disabled-action" : ""}`}
              title="Xác nhận"
              disabled={acting === a.id || !canConfirm}
              onClick={(event) => {
                event.stopPropagation();
                handleAction(a.id, "confirm");
              }}
            >
              Xác nhận
            </button>
            <button
              className={`btn-icon btn-action-pill btn-action-checkin ${!canCheckIn ? "is-disabled-action" : ""}`}
              title="Check-in"
              disabled={acting === a.id || !canCheckIn}
              onClick={(event) => {
                event.stopPropagation();
                handleAction(a.id, "checkin");
              }}
            >
              Check-in
            </button>
            <button
              className={`btn-icon btn-action-pill btn-action-cancel ${!canCancel ? "is-disabled-action" : ""}`}
              title="Hủy"
              disabled={acting === a.id || !canCancel}
              onClick={(event) => {
                event.stopPropagation();
                handleAction(a.id, "cancel");
              }}
            >
              Hủy
            </button>
          </div>
        </td>
      </tr>
    );
  };

  return (
    <div className="management-content appointment-management-page">
      <header className="admin-header">
        <div>
          <h1>Quản lý lịch hẹn</h1>
          <p className="header-sub">Xác nhận, hủy hoặc check-in lịch hẹn của khách hàng</p>
        </div>
      </header>

      {error && <div className="error" style={{ marginBottom: 12 }}>{error}</div>}

      <div className="table-toolbar">
        <div className="toolbar-controls" style={{ display: "flex", gap: 10, alignItems: "center", flexWrap: "wrap" }}>
          <FilterDropdown
            id="appointment-status-filter"
            label="Trạng thái"
            value={statusFilter}
            options={STATUS_FILTER_OPTIONS}
            onChange={setStatusFilter}
            minWidth={160}
          />

          <FilterDropdown
            id="appointment-sort"
            label="Sắp xếp"
            value={sortBy}
            options={SORT_OPTIONS}
            onChange={setSortBy}
            minWidth={180}
          />

          <button type="button" className="toolbar-action-btn toolbar-action-outline" style={{ height: 34, borderRadius: 10, padding: "0 12px" }} onClick={openDateRangePicker}>{dateFilterButtonLabel}</button>
          <button type="button" className="toolbar-action-btn toolbar-action-ghost" style={{ height: 34, borderRadius: 10, padding: "0 12px" }} onClick={resetToolbarFilters} disabled={!canResetToolbarFilters}>Xóa lọc</button>
        </div>
        <span className="result-count">{resultCountText}</span>
      </div>

      <p className="appointment-row-hint">Nhấn vào một dòng để xem thông tin lịch hẹn chi tiết.</p>

      <div
        style={{
          display: "inline-grid",
          gridTemplateColumns: "repeat(2, auto)",
          width: "fit-content",
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
            border: viewMode === VIEW_MODES.SINGLE ? "1px solid #c3d4ed" : "1px solid transparent",
            background: viewMode === VIEW_MODES.SINGLE ? "#ffffff" : "transparent",
            color: viewMode === VIEW_MODES.SINGLE ? "#173c73" : "#446286",
            boxShadow: viewMode === VIEW_MODES.SINGLE ? "0 3px 10px rgba(23, 60, 115, 0.12)" : "none",
          }}
          onClick={() => setViewMode(VIEW_MODES.SINGLE)}
        >
          Lịch thường ({filtered.length})
        </button>
        <button
          type="button"
          className="toolbar-action-btn"
          style={{
            minWidth: 170,
            borderRadius: 10,
            border: viewMode === VIEW_MODES.GROUP ? "1px solid #c3d4ed" : "1px solid transparent",
            background: viewMode === VIEW_MODES.GROUP ? "#ffffff" : "transparent",
            color: viewMode === VIEW_MODES.GROUP ? "#173c73" : "#446286",
            boxShadow: viewMode === VIEW_MODES.GROUP ? "0 3px 10px rgba(23, 60, 115, 0.12)" : "none",
          }}
          onClick={() => setViewMode(groupedViewData.groupedAppointments.length > 0 ? VIEW_MODES.GROUP : VIEW_MODES.SINGLE)}
        >
          Lịch nhóm ({groupedViewData.groupedAppointments.length})
        </button>
      </div>

      <div className="table-wrapper">
        {loading ? (
          <div className="loading-state"><div className="spinner" /><p>Đang tải...</p></div>
        ) : viewMode === VIEW_MODES.SINGLE ? (
          filtered.length === 0 ? (
            <div className="loading-state"><p>Không có lịch hẹn nào.</p></div>
          ) : (
            <table className="admin-table">{renderTableHead()}<tbody>{filtered.map((a) => renderAppointmentRow(a))}</tbody></table>
          )
        ) : groupedViewData.groupedAppointments.length === 0 ? (
          <div className="loading-state"><p>Không có lịch hẹn nhóm nào.</p></div>
        ) : (
          <div style={{ display: "grid", gap: 14 }}>
            {groupedViewData.groupedAppointments.map((group) => (
              <section key={group.key} style={{ border: "1px solid var(--mgmt-table-border, #edf2f7)", borderRadius: 14, overflow: "hidden", background: "var(--mgmt-surface, #fff)" }}>
                <div style={{ padding: "12px 14px", borderBottom: "1px solid var(--mgmt-table-border, #edf2f7)", display: "flex", justifyContent: "space-between", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
                  <div style={{ display: "flex", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
                    <strong>{group.GroupSessionCode || `Nhóm #${group.GroupSessionId || group.key}`}</strong>
                    <span className={`badge ${groupBadgeClass(group.GroupSessionStatus)}`}>{GROUP_STATUS_LABELS[group.GroupSessionStatus] || group.GroupSessionStatus || "Không rõ"}</span>
                    <span style={{ fontSize: 12, color: "var(--mgmt-text-muted, #718096)" }}>{group.appointments.length} lịch · Đã xác nhận {group.confirmedCount} · Đã hủy {group.canceledCount}</span>
                  </div>
                  <div style={{ display: "flex", gap: 10, alignItems: "center", flexWrap: "wrap", fontSize: 12, color: "var(--mgmt-text-muted, #718096)" }}>
                    {showBranchColumn && <span>{group.branchName || "-"}</span>}
                    <span>{formatDate(group.startTime)}</span>
                    <span>{formatTimeRange(group.startTime, group.endTime)}</span>
                  </div>
                </div>
                <div className="table-wrapper" style={{ border: "none", borderRadius: 0, margin: 0 }}>
                  <table className="admin-table">{renderTableHead()}<tbody>{group.appointments.map((a) => renderAppointmentRow(a, `${group.key}-`))}</tbody></table>
                </div>
              </section>
            ))}
          </div>
        )}
      </div>

      {editingAppt && (
        <Modal title="Chỉnh sửa lịch hẹn" onClose={closeEditApptModal} className="modal-lg">
          <div className="appt-edit-intro">
            <strong>Mã lịch hẹn #{editingAppt.id}</strong>
            <span>Điều chỉnh thông tin lịch hẹn và lưu để cập nhật lịch mới cho khách hàng.</span>
          </div>
          <AppointmentForm
            customers={customers}
            vehicles={vehicles}
            customerVehicles={customerVehicles}
            services={services}
            initialValues={buildEditInitialValues(editingAppt)}
            submitLabel="Lưu thay đổi"
            onSubmit={handleSaveEditedAppointment}
            onCancel={closeEditApptModal}
            loading={saving}
          />
        </Modal>
      )}

      {detailModal && (
        <AppointmentDetailModal
          appointment={detailModal}
          loading={false}
          error=""
          onClose={() => setDetailModal(null)}
          onEdit={handleOpenApptEdit}
          onEditDisabled={editPreparing || !canEditAppointment(detailModal?.status)}
          formatApptDate={(value) => formatDate(value, "--")}
          formatApptTime={formatTime}
          getApptStatusBadge={badgeClass}
        />
      )}

      {showDateRangeModal && (
        <Modal title="Lọc lịch theo khoảng ngày" onClose={() => setShowDateRangeModal(false)} className="date-range-modal">
          <div className="date-range-picker-shell">
            <div className="date-range-summary">
              <div className="date-pill">
                <span className="date-pill-label">Bắt đầu</span>
                <strong>{toDisplayDate(draftDateFrom)}</strong>
              </div>
              <div className="date-pill-arrow">→</div>
              <div className="date-pill">
                <span className="date-pill-label">Kết thúc</span>
                <strong>{toDisplayDate(draftDateTo)}</strong>
              </div>
            </div>

            <div className="date-range-hint">
              {!draftDateFrom && "Chọn ngày bắt đầu"}
              {draftDateFrom && !draftDateTo && "Chọn ngày kết thúc (không nhỏ hơn ngày bắt đầu)"}
              {draftDateFrom && draftDateTo && "Đã chọn xong khoảng ngày"}
            </div>

            <div className="date-range-calendar-wrap">
              <Calendar
                locale="vi-VN"
                className="date-range-calendar"
                onClickDay={handleRangeDayClick}
                tileClassName={tileClassName}
                tileDisabled={({ date, view }) => view === "month" && Boolean(draftDateFrom && !draftDateTo && toDateText(date) < draftDateFrom)}
                value={draftDateTo ? [new Date(`${draftDateFrom}T00:00:00`), new Date(`${draftDateTo}T00:00:00`)] : (draftDateFrom ? new Date(`${draftDateFrom}T00:00:00`) : new Date())}
              />
            </div>

            <div className="date-range-quick-actions">
              <button type="button" className="date-quick-btn" onClick={() => quickSetDateRange(7)}>7 ngày gần nhất</button>
              <button type="button" className="date-quick-btn" onClick={() => quickSetDateRange(30)}>30 ngày gần nhất</button>
              <button type="button" className="date-quick-btn" onClick={() => {
                const now = new Date();
                const from = new Date(now.getFullYear(), now.getMonth(), 1).toISOString().slice(0, 10);
                const to = new Date(now.getFullYear(), now.getMonth() + 1, 0).toISOString().slice(0, 10);
                setDraftDateFrom(from);
                setDraftDateTo(to);
              }}>Tháng này</button>
            </div>

            <div className="date-range-footer">
              <button type="button" className="btn-secondary-outline" style={{ height: "42px", minWidth: "90px", borderRadius: "10px", padding: "0 16px" }} onClick={() => setShowDateRangeModal(false)}>Đóng</button>
              <button type="button" className="btn-primary" style={{ height: "42px", minWidth: "110px", borderRadius: "10px", padding: "0 18px" }} onClick={applyDateRange} disabled={!draftDateFrom || !draftDateTo}>Áp dụng</button>
            </div>
          </div>
        </Modal>
      )}
    </div>
  );
}
