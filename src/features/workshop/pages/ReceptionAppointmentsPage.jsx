import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  getAllAppointments,
  getBranchAppointments,
  getAppointmentDetail,
  confirmAppointment,
  checkInAppointment,
  cancelAppointment,
} from "@features/appointments/api/appointmentApi";
import { cancelGroupSessionAll } from "@features/appointments/api/groupSessionApi";
import DateRangePickerModal, { getDateRangeButtonLabel } from "@features/admin/components/DateRangePickerModal";
import { useSearch } from "@shared/core/context/SearchContext";
import { useBranch } from "@shared/core/context/BranchContext";
import { useAuth } from "@shared/core/context/AuthContext";
import ReceptionAppointmentsSection from "@features/workshop/components/ReceptionAppointmentsSection";
import AppointmentDetailModal from "@features/workshop/components/AppointmentDetailModal";

function parseBranchId(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
}

function normalizeRoleCode(value) {
  return String(value || "").trim().toUpperCase();
}

function normalizeRoleName(value) {
  return String(value || "")
    .trim()
    .toUpperCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/Đ/g, "D")
    .replace(/-/g, "_")
    .replace(/\s+/g, "_");
}

function isAdminRole(roleCode, roleName) {
  const code = normalizeRoleCode(roleCode);
  const name = normalizeRoleName(roleName);
  return code === "ADMIN" || name.includes("ADMIN");
}

function normalizeStatus(status) {
  return String(status || "").trim().toUpperCase();
}

function decodeUnicodeEscapes(text) {
  const raw = String(text ?? "");
  if (!raw || !/\\u[0-9a-fA-F]{4}/.test(raw)) return raw;
  return raw.replace(/\\u([0-9a-fA-F]{4})/g, (_, code) =>
    String.fromCharCode(parseInt(code, 16))
  );
}

function toDisplayErrorText(value, fallback = "") {
  const decoded = decodeUnicodeEscapes(value);
  return String(decoded || fallback).trim();
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

function normalizeAppointmentItem(item) {
  const source = item || {};
  const pick = (...keys) => {
    for (const key of keys) {
      const value = source?.[key];
      if (value !== undefined && value !== null) return value;
    }
    return null;
  };
  const numeric = (value) => {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  };
  const text = (value) => (value === undefined || value === null ? "" : String(value));
  const list = (value) => (Array.isArray(value) ? value : []);

  return {
    ...source,
    id: numeric(pick("id", "Id")),
    branchId: numeric(pick("branchId", "BranchId")),
    branchName: text(pick("branchName", "BranchName")),
    customerId: numeric(pick("customerId", "CustomerId")),
    customerName: text(pick("customerName", "CustomerName", "customerFullName", "CustomerFullName")),
    customerPhone: text(pick("customerPhone", "CustomerPhone")),
    startTime: pick("startTime", "StartTime"),
    endTime: pick("endTime", "EndTime"),
    status: text(pick("status", "Status")),
    createdAt: pick("createdAt", "CreatedAt"),
    updatedAt: pick("updatedAt", "UpdatedAt"),
    vehicleLicensePlate: text(pick("vehicleLicensePlate", "VehicleLicensePlate", "licensePlate", "LicensePlate")),
    vehicleId: numeric(pick("vehicleId", "VehicleId")),
    vehicleModelName: text(pick("vehicleModelName", "VehicleModelName")),
    requestedServiceIds: text(pick("requestedServiceIds", "RequestedServiceIds")),
    serviceNames: Array.isArray(pick("serviceNames", "ServiceNames"))
      ? pick("serviceNames", "ServiceNames").map((name) => String(name || "").trim()).filter(Boolean)
      : [],
    customerNotes: text(pick("customerNotes", "CustomerNotes")),
    requiresDispatchReview: Boolean(pick("requiresDispatchReview", "RequiresDispatchReview")),
    dispatchReviewNote: text(pick("dispatchReviewNote", "DispatchReviewNote")),
    requiresPartsFollowUp: Boolean(pick("requiresPartsFollowUp", "RequiresPartsFollowUp")),
    partsFollowUpNote: text(pick("partsFollowUpNote", "PartsFollowUpNote")),
    partsFollowUpServices: list(pick("partsFollowUpServices", "PartsFollowUpServices")),
    cancelReason: text(pick("cancelReason", "CancelReason")),
    GroupSessionId: numeric(pick("groupSessionId", "GroupSessionId")),
    GroupSessionCode: text(pick("groupSessionCode", "GroupSessionCode")),
    GroupSessionStatus: text(pick("groupSessionStatus", "GroupSessionStatus")),
  };
}

function getApptStatusBadge(status) {
  const normalized = normalizeStatus(status);
  if (normalized === "PARTS_PENDING") return "badge-warning";
  if (normalized === "PENDING" || normalized === "LATE") return "badge-warning";
  if (normalized === "CONFIRMED" || normalized === "PARTIAL_CONFIRMED") return "badge-active";
  if (normalized === "ARRIVED" || normalized === "ARRIVED_LATE") return "badge-success";
  if (normalized === "CANCELED" || normalized === "CANCELLED") return "badge-danger";
  if (normalized === "COMPLETED") return "badge-completed";
  return "badge-inactive";
}

function canConfirmAppointment(status) {
  const normalized = normalizeStatus(status);
  return normalized === "PENDING" || normalized === "LATE";
}

function canCheckInAppointment(status) {
  const normalized = normalizeStatus(status);
  return normalized === "CONFIRMED" || normalized === "LATE";
}

function canCancelAppointment(status) {
  const normalized = normalizeStatus(status);
  return normalized === "PENDING" || normalized === "CONFIRMED" || normalized === "LATE";
}

function canEditAppointment(status) {
  const normalized = normalizeStatus(status);
  return normalized === "PENDING" || normalized === "CONFIRMED" || normalized === "LATE";
}

function formatApptDate(value) {
  if (!value) return "--/--/----";
  const d = new Date(value);
  return Number.isNaN(d.getTime())
    ? "--/--/----"
    : d.toLocaleDateString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" });
}

function formatApptTime(value) {
  if (!value) return "--:--";
  const d = new Date(value);
  return Number.isNaN(d.getTime())
    ? "--:--"
    : d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit", timeZone: "Asia/Ho_Chi_Minh" });
}

function getAppointmentFilterTimestamp(appt) {
  // Date filter for Appointments must follow scheduled appointment time, not createdAt.
  const start = new Date(appt?.startTime || appt?.StartTime || 0).getTime();
  if (Number.isFinite(start) && start > 0) {
    return start;
  }

  const end = new Date(appt?.endTime || appt?.EndTime || 0).getTime();
  return Number.isFinite(end) ? end : 0;
}

export default function ReceptionAppointmentsPage() {
  const navigate = useNavigate();
  const { searchQuery } = useSearch();
  const { selectedBranchId } = useBranch();
  const { user } = useAuth();

  const [appointments, setAppointments] = useState([]);
  const [apptLoading, setApptLoading] = useState(false);
  const [error, setError] = useState("");
  const [modal, setModal] = useState(null);
  const [actingAppt, setActingAppt] = useState(null);
  const [actingGroup, setActingGroup] = useState(null);
  const [selectedApptForView, setSelectedApptForView] = useState(null);
  const [apptDetailLoading, setApptDetailLoading] = useState(false);
  const [apptDetailError, setApptDetailError] = useState("");
  const [apptStatusFilter, setApptStatusFilter] = useState("ALL");
  const [apptSortBy, setApptSortBy] = useState("NEWEST");
  const [apptViewMode, setApptViewMode] = useState("SINGLE");
  const [dateFrom, setDateFrom] = useState(() => toDateText(new Date()));
  const [dateTo, setDateTo] = useState(() => toDateText(new Date()));
  const [apptsPage, setApptsPage] = useState(1);
  const [apptsPageSize, setApptsPageSize] = useState(10);

  const userBranchId = parseBranchId(user?.branchId ?? user?.BranchId);
  const contextBranchId = parseBranchId(selectedBranchId);
  const effectiveBranchId = contextBranchId || userBranchId;
  const canViewAllBranches = isAdminRole(user?.roleCode ?? user?.RoleCode, user?.roleName ?? user?.RoleName);

  const normalizeAppointments = useCallback((payload) => {
    const list = Array.isArray(payload)
      ? payload
      : Array.isArray(payload?.items)
        ? payload.items
        : Array.isArray(payload?.Items)
          ? payload.Items
          : Array.isArray(payload?.data)
            ? payload.data
            : Array.isArray(payload?.Data)
              ? payload.Data
              : [];

    return list
      .map(normalizeAppointmentItem)
      .filter((item) => {
        if (item.id <= 0) return false;
        if (!effectiveBranchId && canViewAllBranches) return true;
        return !effectiveBranchId || item.branchId === 0 || item.branchId === effectiveBranchId;
      });
  }, [canViewAllBranches, effectiveBranchId]);

  const fetchAppointments = useCallback(async () => {
    if (!effectiveBranchId && !canViewAllBranches) {
      setAppointments([]);
      return;
    }
    setApptLoading(true);
    setError("");
    try {
      const data = effectiveBranchId
        ? await getBranchAppointments(effectiveBranchId)
        : await getAllAppointments();
      setAppointments(normalizeAppointments(data));
    } catch (err) {
      setError(toDisplayErrorText(err?.message, "Không thể tải dữ liệu lịch hẹn."));
    } finally {
      setApptLoading(false);
    }
  }, [canViewAllBranches, effectiveBranchId, normalizeAppointments]);

  const fetchAppointmentsSilent = useCallback(async () => {
    if (!effectiveBranchId && !canViewAllBranches) {
      setAppointments([]);
      return;
    }
    try {
      const data = effectiveBranchId
        ? await getBranchAppointments(effectiveBranchId)
        : await getAllAppointments();
      setAppointments(normalizeAppointments(data));
    } catch (err) {
      setError(toDisplayErrorText(err?.message, "Không thể tải dữ liệu lịch hẹn."));
    }
  }, [canViewAllBranches, effectiveBranchId, normalizeAppointments]);

  useEffect(() => {
    fetchAppointments();
  }, [fetchAppointments]);

  useEffect(() => {
    if (!effectiveBranchId && !canViewAllBranches) return undefined;
    const timer = setInterval(() => {
      fetchAppointmentsSilent();
    }, 15000);
    return () => clearInterval(timer);
  }, [canViewAllBranches, effectiveBranchId, fetchAppointmentsSilent]);

  const apptStatusOptions = useMemo(() => {
    const statuses = Array.from(
      new Set((appointments || []).map((item) => normalizeStatus(item.status)).filter(Boolean)),
    );
    return statuses.sort((a, b) => a.localeCompare(b));
  }, [appointments]);

  const filteredAppts = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    const queryCompact = query.replace(/[\s.-]/g, "");
    const startDate = toStartOfDay(dateFrom);
    const endDate = toEndOfDay(dateTo);

    const list = (appointments || []).filter((appt) => {
      const byStatus = apptStatusFilter === "ALL" || normalizeStatus(appt.status) === apptStatusFilter;
      const scheduledTime = getAppointmentFilterTimestamp(appt);
      const byDate =
        (!startDate || scheduledTime >= startDate.getTime()) &&
        (!endDate || scheduledTime <= endDate.getTime());

      const plate = String(appt.vehicleLicensePlate || "").toLowerCase();
      const plateCompact = plate.replace(/[\s.-]/g, "");
      const byQuery =
        !query ||
        String(appt.customerName || "").toLowerCase().includes(query) ||
        String(appt.customerPhone || "").toLowerCase().includes(query) ||
        String(appt.GroupSessionCode || "").toLowerCase().includes(query) ||
        plate.includes(query) ||
        (queryCompact && plateCompact.includes(queryCompact));

      return byStatus && byDate && byQuery;
    });

    return list.sort((a, b) => {
      const aTime = getAppointmentFilterTimestamp(a);
      const bTime = getAppointmentFilterTimestamp(b);
      const aName = String(a.customerName || "");
      const bName = String(b.customerName || "");

      if (apptSortBy === "OLDEST") return aTime - bTime;
      if (apptSortBy === "NAME_ASC") return aName.localeCompare(bName, "vi");
      if (apptSortBy === "NAME_DESC") return bName.localeCompare(aName, "vi");
      return bTime - aTime;
    });
  }, [appointments, searchQuery, apptStatusFilter, apptSortBy, dateFrom, dateTo]);

  const groupedAppts = useMemo(() => {
    const grouped = new Map();
    filteredAppts.forEach((appt) => {
      const gid = Number(appt.GroupSessionId || 0);
      const groupCode = String(appt.GroupSessionCode || "").trim();
      const hasGroupId = gid > 0;
      const hasGroupCode = Boolean(groupCode);
      if (!hasGroupId && !hasGroupCode) return;

      const groupKey = hasGroupId ? `id:${gid}` : `code:${groupCode}`;

      if (!grouped.has(groupKey)) {
        grouped.set(groupKey, {
          key: groupKey,
          id: groupKey,
          groupSessionId: hasGroupId ? gid : 0,
          groupCode: groupCode || `BG-${gid || groupKey}`,
          status: appt.GroupSessionStatus || "PENDING",
          customerName: appt.customerName || "",
          customerPhone: appt.customerPhone || "",
          appointments: [],
        });
      }

      const group = grouped.get(groupKey);
      if (!group.status && appt.GroupSessionStatus) group.status = appt.GroupSessionStatus;
      if (!group.customerName && appt.customerName) group.customerName = appt.customerName;
      if (!group.customerPhone && appt.customerPhone) group.customerPhone = appt.customerPhone;
      group.appointments.push(appt);
    });

    return Array.from(grouped.values())
      .map((group) => {
        const sortedAppointments = [...group.appointments].sort(
          (a, b) => new Date(a.startTime || 0).getTime() - new Date(b.startTime || 0).getTime(),
        );
        return {
          ...group,
          appointments: sortedAppointments,
          hasCancelableAppointments: sortedAppointments.some((appt) => canCancelAppointment(appt.status)),
        };
      })
      .sort((a, b) => {
        const aTime = new Date(a.appointments[0]?.startTime || 0).getTime();
        const bTime = new Date(b.appointments[0]?.startTime || 0).getTime();
        return apptSortBy === "OLDEST" ? aTime - bTime : bTime - aTime;
      });
  }, [filteredAppts, apptSortBy]);

  const filteredSingleAppts = useMemo(
    () =>
      filteredAppts.filter((appt) => {
        const hasGroupId = Number(appt.GroupSessionId || 0) > 0;
        const hasGroupCode = Boolean(String(appt.GroupSessionCode || "").trim());
        return !(hasGroupId || hasGroupCode);
      }),
    [filteredAppts],
  );

  const pagedSingleAppts = useMemo(() => {
    const start = (apptsPage - 1) * apptsPageSize;
    return filteredSingleAppts.slice(start, start + apptsPageSize);
  }, [filteredSingleAppts, apptsPage, apptsPageSize]);

  const pagedGroupedAppts = useMemo(() => {
    const start = (apptsPage - 1) * apptsPageSize;
    return groupedAppts.slice(start, start + apptsPageSize);
  }, [groupedAppts, apptsPage, apptsPageSize]);

  useEffect(() => {
    setApptsPage(1);
  }, [
    searchQuery,
    apptStatusFilter,
    apptSortBy,
    dateFrom,
    dateTo,
    appointments.length,
    apptViewMode,
    groupedAppts.length,
    filteredSingleAppts.length,
  ]);

  const handleApptAction = useCallback(async (appt, action) => {
    const appointment = normalizeAppointmentItem(appt);
    const id = Number(appointment?.id || 0);
    if (!id) return;

    const canConfirm = canConfirmAppointment(appointment.status);
    const canCheckIn = canCheckInAppointment(appointment.status);
    const canCancel = canCancelAppointment(appointment.status);

    if (action === "confirm" && !canConfirm) return;
    if (action === "checkin" && !canCheckIn) return;
    if (action === "cancel" && !canCancel) return;

    setActingAppt(id);
    try {
      if (action === "confirm") {
        await confirmAppointment(id);
      } else if (action === "checkin") {
        await checkInAppointment(id, { arrivedAt: new Date().toISOString() });
      } else if (action === "cancel") {
        const reasonInput = window.prompt("L\u00fd do h\u1ee7y l\u1ecbch:", "Nh\u00e2n vi\u00ean h\u1ee7y");
        if (reasonInput === null) return;
        const reason = String(reasonInput || "").trim();
        if (!reason) {
          alert("Vui l\u00f2ng nh\u1eadp l\u00fd do h\u1ee7y.");
          return;
        }
        await cancelAppointment(id, reason);
      }

      await fetchAppointmentsSilent();
    } catch (err) {
      const errorMessage = toDisplayErrorText(err?.message, "Không rõ lỗi");
      setError(`Thao tác lịch hẹn thất bại: ${errorMessage}`);
    } finally {
      setActingAppt(null);
    }
  }, [fetchAppointmentsSilent]);

  const handleGroupCancel = useCallback(async (group) => {
    const cancelableAppointments = (group.appointments || []).filter((appt) => canCancelAppointment(appt.status));
    if (cancelableAppointments.length === 0) return;

    const confirmedPlates = (group.appointments || [])
      .filter((appt) => normalizeStatus(appt.status) === "CONFIRMED")
      .map((appt) => appt.vehicleLicensePlate)
      .filter(Boolean);

    const warningMessage = confirmedPlates.length > 0
      ? `Nh\u00f3m n\u00e0y c\u00f3 xe ${confirmedPlates.join(", ")} \u0111\u00e3 x\u00e1c nh\u1eadn. B\u1ea1n c\u00f3 ch\u1eafc ch\u1eafn mu\u1ed1n h\u1ee7y to\u00e0n b\u1ed9 kh\u00f4ng?`
      : "B\u1ea1n c\u00f3 ch\u1eafc ch\u1eafn mu\u1ed1n h\u1ee7y to\u00e0n b\u1ed9 nh\u00f3m n\u00e0y kh\u00f4ng?";

    if (!window.confirm(warningMessage)) return;

    const reasonInput = window.prompt(
      "Nh\u1eadp l\u00fd do h\u1ee7y (b\u1eaft bu\u1ed9c):",
      "Qu\u00e1 gi\u1edd h\u1eb9n",
    );
    if (reasonInput === null) return;
    const reason = String(reasonInput || "").trim();
    if (!reason) {
      alert("L\u00fd do h\u1ee7y l\u00e0 b\u1eaft bu\u1ed9c.");
      return;
    }

    const groupSessionId = Number(group?.groupSessionId || 0);
    setActingGroup(group.id);
    try {
      if (groupSessionId > 0) {
        await cancelGroupSessionAll(groupSessionId, reason);
      } else {
        await Promise.allSettled(
          cancelableAppointments.map((appt) => cancelAppointment(appt.id, reason)),
        );
      }
    } catch {
      await Promise.allSettled(
        cancelableAppointments.map((appt) => cancelAppointment(appt.id, reason)),
      );
    } finally {
      setActingGroup(null);
    }

    await fetchAppointmentsSilent();
  }, [fetchAppointmentsSilent]);

  const handleOpenApptDetail = useCallback(async (appt) => {
    const base = normalizeAppointmentItem(appt);
    if (!base?.id) return;

    setSelectedApptForView(base);
    setApptDetailError("");
    setApptDetailLoading(true);
    setModal("appt_detail");

    try {
      const detail = await getAppointmentDetail(base.id);
      const normalizedDetail = normalizeAppointmentItem(detail);
      setSelectedApptForView((prev) => {
        if (!prev || Number(prev.id) !== Number(base.id)) return prev;
        return { ...prev, ...normalizedDetail };
      });
    } catch (err) {
      setApptDetailError(toDisplayErrorText(err?.message, "Không thể tải chi tiết lịch hẹn."));
    } finally {
      setApptDetailLoading(false);
    }
  }, []);

  const closeApptDetailModal = useCallback(() => {
    setModal(null);
    setSelectedApptForView(null);
    setApptDetailError("");
    setApptDetailLoading(false);
  }, []);

  const handleOpenApptEdit = useCallback((appointment) => {
    const base = normalizeAppointmentItem(appointment || selectedApptForView);
    if (!base?.id) return;
    if (!canEditAppointment(base.status)) {
      setApptDetailError("Kh\u00f4ng th\u1ec3 s\u1eeda l\u1ecbch khi xe \u0111\u00e3 \u0111\u1ebfn ho\u1eb7c l\u1ecbch \u0111\u00e3 k\u1ebft th\u00fac.");
      return;
    }

    setError("");
    setModal(null);
    navigate(`/workshop/reception/appointments/${base.id}/edit`);
  }, [navigate, selectedApptForView]);

  const openDateRangePicker = useCallback(() => {
    setModal("date_range");
  }, []);

  const defaultDateFilter = useMemo(() => toDateText(new Date()), []);
  const dateFilterButtonLabel = useMemo(
    () => getDateRangeButtonLabel(dateFrom, dateTo, { todayDateText: defaultDateFilter }),
    [dateFrom, dateTo, defaultDateFilter]
  );
  const canResetToolbarFilters = apptStatusFilter !== "ALL" || apptSortBy !== "NEWEST" || Boolean(dateFrom || dateTo);

  const resetToolbarFilters = useCallback(() => {
    setApptStatusFilter("ALL");
    setApptSortBy("NEWEST");
    setDateFrom("");
    setDateTo("");
  }, []);

  const handleApplyDateRange = useCallback(({ dateFrom: nextDateFrom, dateTo: nextDateTo }) => {
    setDateFrom(nextDateFrom || "");
    setDateTo(nextDateTo || "");
    setModal(null);
  }, []);

  return (
    <div className="reception-dashboard">
      {error && (
        <div style={{ padding: "12px", background: "#fee", color: "#e6522c", borderRadius: "8px", marginBottom: "16px", fontWeight: 600 }}>
          Lỗi: {toDisplayErrorText(error)}
        </div>
      )}

      <ReceptionAppointmentsSection
        onCreateAppointment={() => navigate("/workshop/reception/appointments/new")}
        apptStatusFilter={apptStatusFilter}
        setApptStatusFilter={setApptStatusFilter}
        apptStatusOptions={apptStatusOptions}
        apptSortBy={apptSortBy}
        setApptSortBy={setApptSortBy}
        openDateRangePicker={openDateRangePicker}
        dateFilterButtonLabel={dateFilterButtonLabel}
        canResetToolbarFilters={canResetToolbarFilters}
        onResetToolbarFilters={resetToolbarFilters}
        apptViewMode={apptViewMode}
        setApptViewMode={setApptViewMode}
        filteredSingleAppts={filteredSingleAppts}
        groupedAppts={groupedAppts}
        apptLoading={apptLoading}
        pagedSingleAppts={pagedSingleAppts}
        pagedGroupedAppts={pagedGroupedAppts}
        formatApptDate={formatApptDate}
        formatApptTime={formatApptTime}
        getApptStatusBadge={getApptStatusBadge}
        handleApptAction={handleApptAction}
        handleGroupCancel={handleGroupCancel}
        actingAppt={actingAppt}
        actingGroup={actingGroup}
        onAppointmentClick={handleOpenApptDetail}
        apptsPage={apptsPage}
        apptsPageSize={apptsPageSize}
        apptsTotalItems={apptViewMode === "GROUP" ? groupedAppts.length : filteredSingleAppts.length}
        setApptsPage={setApptsPage}
        setApptsPageSize={setApptsPageSize}
      />

      {modal === "appt_detail" && selectedApptForView && (
        <AppointmentDetailModal
          appointment={selectedApptForView}
          loading={apptDetailLoading}
          error={apptDetailError}
          onClose={closeApptDetailModal}
          onEdit={handleOpenApptEdit}
          onEditDisabled={apptDetailLoading || !canEditAppointment(selectedApptForView?.status)}
          formatApptDate={formatApptDate}
          formatApptTime={formatApptTime}
          getApptStatusBadge={getApptStatusBadge}
        />
      )}

      {modal === "date_range" && (
        <DateRangePickerModal
          isOpen={modal === "date_range"}
          onClose={() => setModal(null)}
          initialDateFrom={dateFrom}
          initialDateTo={dateTo}
          onApply={handleApplyDateRange}
        />
      )}
    </div>
  );
}
