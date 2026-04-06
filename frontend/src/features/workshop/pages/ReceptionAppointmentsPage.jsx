import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  getBranchAppointments,
  getAppointmentDetail,
  confirmAppointment,
  checkInAppointment,
  cancelAppointment,
  getAllStandardServices,
} from "@features/appointments/api/appointmentApi";
import { cancelGroupSessionAll } from "@features/appointments/api/groupSessionApi";
import {
  getAllCustomers,
  getAllVehicles,
  getAllCustomerVehicles,
} from "@features/admin/api/customerApi";
import { Modal } from "@features/admin/components/AdminModals";
import DateRangePickerModal, { getDateRangeButtonLabel } from "@features/admin/components/DateRangePickerModal";
import { useSearch } from "@shared/core/context/SearchContext";
import { useBranch } from "@shared/core/context/BranchContext";
import { useAuth } from "@shared/core/context/AuthContext";
import AppointmentForm from "@features/workshop/components/AppointmentForm";
import ReceptionAppointmentsSection from "@features/workshop/components/ReceptionAppointmentsSection";
import AppointmentDetailModal from "@features/workshop/components/AppointmentDetailModal";
import { saveReceptionAppointment } from "@features/workshop/utils/receptionAppointmentBooking";

function parseBranchId(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
}

function normalizeStatus(status) {
  return String(status || "").trim().toUpperCase();
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
    cancelReason: text(pick("cancelReason", "CancelReason")),
    GroupSessionId: numeric(pick("groupSessionId", "GroupSessionId")),
    GroupSessionCode: text(pick("groupSessionCode", "GroupSessionCode")),
    GroupSessionStatus: text(pick("groupSessionStatus", "GroupSessionStatus")),
  };
}

function getApptStatusBadge(status) {
  const normalized = normalizeStatus(status);
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
  return Number.isNaN(d.getTime()) ? "--/--/----" : d.toLocaleDateString("vi-VN");
}

function formatApptTime(value) {
  if (!value) return "--:--";
  const d = new Date(value);
  return Number.isNaN(d.getTime())
    ? "--:--"
    : d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
}

export default function ReceptionAppointmentsPage() {
  const navigate = useNavigate();
  const { searchQuery } = useSearch();
  const { selectedBranchId } = useBranch();
  const { user } = useAuth();

  const [appointments, setAppointments] = useState([]);
  const [customers, setCustomers] = useState([]);
  const [vehicles, setVehicles] = useState([]);
  const [customerVehicles, setCustomerVehicles] = useState([]);
  const [services, setServices] = useState([]);
  const [apptLoading, setApptLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [modal, setModal] = useState(null);
  const [actingAppt, setActingAppt] = useState(null);
  const [actingGroup, setActingGroup] = useState(null);
  const [selectedApptForView, setSelectedApptForView] = useState(null);
  const [apptDetailLoading, setApptDetailLoading] = useState(false);
  const [apptDetailError, setApptDetailError] = useState("");
  const [editingAppt, setEditingAppt] = useState(null);
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
      .filter((item) => item.id > 0 && (!effectiveBranchId || item.branchId === 0 || item.branchId === effectiveBranchId));
  }, [effectiveBranchId]);

  const fetchReferenceData = useCallback(async () => {
    try {
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
    } catch (err) {
      setError(err?.message || "Không thể tải dữ liệu lịch hẹn.");
    }
  }, []);

  const fetchAppointments = useCallback(async () => {
    if (!effectiveBranchId) {
      setAppointments([]);
      return;
    }
    setApptLoading(true);
    setError("");
    try {
      const data = await getBranchAppointments(effectiveBranchId);
      setAppointments(normalizeAppointments(data));
    } catch (err) {
      setError(err?.message || "Không thể tải dữ liệu lịch hẹn.");
    } finally {
      setApptLoading(false);
    }
  }, [effectiveBranchId, normalizeAppointments]);

  const fetchAppointmentsSilent = useCallback(async () => {
    if (!effectiveBranchId) {
      setAppointments([]);
      return;
    }
    try {
      const data = await getBranchAppointments(effectiveBranchId);
      setAppointments(normalizeAppointments(data));
    } catch (err) {
      setError(err?.message || "Không thể tải dữ liệu lịch hẹn.");
    }
  }, [effectiveBranchId, normalizeAppointments]);

  useEffect(() => {
    fetchReferenceData();
  }, [fetchReferenceData]);

  useEffect(() => {
    fetchAppointments();
  }, [fetchAppointments]);

  useEffect(() => {
    if (!effectiveBranchId) return undefined;
    const timer = setInterval(() => {
      fetchAppointmentsSilent();
    }, 15000);
    return () => clearInterval(timer);
  }, [effectiveBranchId, fetchAppointmentsSilent]);

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
      const scheduledTime = new Date(appt.startTime || 0).getTime();
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
      const aTime = new Date(a.startTime || 0).getTime();
      const bTime = new Date(b.startTime || 0).getTime();
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
        const reasonInput = window.prompt("Lý do hủy lịch:", "Nhân viên hủy");
        if (reasonInput === null) return;
        const reason = String(reasonInput || "").trim();
        if (!reason) {
          alert("Vui lòng nhập lý do hủy.");
          return;
        }
        await cancelAppointment(id, reason);
      }

      await fetchAppointmentsSilent();
    } catch (err) {
      setError(`Thao tác lịch hẹn thất bại: ${err?.message || "Không rõ lỗi"}`);
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
      ? `Nhóm này có xe ${confirmedPlates.join(", ")} đã xác nhận. Bạn có chắc chắn muốn hủy toàn bộ không?`
      : "Bạn có chắc chắn muốn hủy toàn bộ nhóm này không?";

    if (!window.confirm(warningMessage)) return;

    const reasonInput = window.prompt(
      "Nhập lý do hủy (bắt buộc):",
      "Quá giờ hẹn",
    );
    if (reasonInput === null) return;
    const reason = String(reasonInput || "").trim();
    if (!reason) {
      alert("Lý do hủy là bắt buộc.");
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
      setApptDetailError(err?.message || "Không thể tải chi tiết lịch hẹn.");
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

  const handleOpenApptEdit = useCallback(async (appointment) => {
    const base = normalizeAppointmentItem(appointment || selectedApptForView);
    if (!base?.id) return;
    if (!canEditAppointment(base.status)) {
      setApptDetailError("Không thể sửa lịch khi xe đã đến hoặc lịch đã kết thúc.");
      return;
    }

    setError("");
    setModal("edit_appt");
    try {
      const detail = await getAppointmentDetail(base.id);
      setEditingAppt(normalizeAppointmentItem(detail));
    } catch {
      setEditingAppt(base);
    }
  }, [selectedApptForView]);

  const closeEditApptModal = useCallback(() => {
    setModal(null);
    setEditingAppt(null);
  }, []);

  const handleSaveEditedAppointment = useCallback(async (formData) => {
    if (!editingAppt?.id) return;
    setSaving(true);
    setError("");
    try {
      await saveReceptionAppointment({
        effectiveBranchId,
        formData,
        customers,
        vehicles,
        customerVehicles,
        services,
        replaceAppointmentId: editingAppt.id,
      });
      await fetchAppointmentsSilent();
      closeEditApptModal();
    } catch (err) {
      setError(err?.message || "Không thể cập nhật lịch hẹn.");
    } finally {
      setSaving(false);
    }
  }, [editingAppt, effectiveBranchId, customers, vehicles, customerVehicles, services, fetchAppointmentsSilent, closeEditApptModal]);

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
          Lỗi: {error}
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

      {modal === "edit_appt" && editingAppt && (
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
