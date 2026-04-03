import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import Calendar from "react-calendar";
import "react-calendar/dist/Calendar.css";
import {
  getBranchAppointments,
  getAppointmentDetail,
  cancelAppointment,
  getAllStandardServices,
} from "@features/appointments/api/appointmentApi";
import { cancelBookingGroupAll } from "@features/appointments/api/bookingGroupApi";
import {
  getAllCustomers,
  getAllVehicles,
  getAllCustomerVehicles,
} from "@features/admin/api/customerApi";
import { Modal } from "@features/admin/components/AdminModals";
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
    bookingGroupId: numeric(pick("bookingGroupId", "BookingGroupId")),
    bookingGroupCode: text(pick("bookingGroupCode", "BookingGroupCode")),
    bookingGroupStatus: text(pick("bookingGroupStatus", "BookingGroupStatus")),
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

function canCancelAppointment(status) {
  const normalized = normalizeStatus(status);
  return normalized === "PENDING" || normalized === "CONFIRMED";
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
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [draftDateFrom, setDraftDateFrom] = useState("");
  const [draftDateTo, setDraftDateTo] = useState("");
  const [apptsPage, setApptsPage] = useState(1);
  const [apptsPageSize, setApptsPageSize] = useState(10);

  const userBranchId = parseBranchId(user?.branchId ?? user?.BranchId);
  const contextBranchId = parseBranchId(selectedBranchId);
  const effectiveBranchId = userBranchId || contextBranchId;

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
        String(appt.bookingGroupCode || "").toLowerCase().includes(query) ||
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
      const gid = Number(appt.bookingGroupId || 0);
      if (!gid) return;

      if (!grouped.has(gid)) {
        grouped.set(gid, {
          id: gid,
          groupCode: appt.bookingGroupCode || `BG-${gid}`,
          status: appt.bookingGroupStatus || "PENDING",
          customerName: appt.customerName || "",
          customerPhone: appt.customerPhone || "",
          appointments: [],
        });
      }

      grouped.get(gid).appointments.push(appt);
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
    () => filteredAppts.filter((appt) => !Number(appt.bookingGroupId || 0)),
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

  const handleApptAction = useCallback(async (id) => {
    setActingAppt(id);
    try {
      const reasonInput = window.prompt("Lý do hủy lịch:", "Nhân viên hủy");
      if (reasonInput === null) return;
      const reason = String(reasonInput || "").trim();
      if (!reason) {
        alert("Vui lòng nhập lý do hủy.");
        return;
      }
      await cancelAppointment(id, reason);
      await fetchAppointmentsSilent();
    } catch (err) {
      setError(`Thao tác lịch hẹn thất bại: ${err?.message || "Không rõ lỗi"}`);
    } finally {
      setActingAppt(null);
    }
  }, [fetchAppointmentsSilent]);

  const handleGroupCancel = useCallback(async (group) => {
    if (!group?.id) return;

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

    setActingGroup(group.id);
    try {
      await cancelBookingGroupAll(group.id, reason);
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
    setDraftDateFrom(dateFrom);
    setDraftDateTo(dateTo);
    setModal("date_range");
  }, [dateFrom, dateTo]);

  const applyDateRange = useCallback(() => {
    setDateFrom(draftDateFrom || "");
    setDateTo(draftDateTo || "");
    setModal(null);
  }, [draftDateFrom, draftDateTo]);

  const clearDateRange = useCallback(() => {
    setDateFrom("");
    setDateTo("");
    setDraftDateFrom("");
    setDraftDateTo("");
  }, []);

  const quickSetDateRange = useCallback((days) => {
    const now = new Date();
    const to = now.toISOString().slice(0, 10);
    const fromDate = new Date(now);
    fromDate.setDate(now.getDate() - (days - 1));
    const from = fromDate.toISOString().slice(0, 10);
    setDraftDateFrom(from);
    setDraftDateTo(to);
  }, []);

  const handleRangeDayClick = useCallback((dateObj) => {
    const picked = toDateText(dateObj);
    if (!picked) return;

    if (!draftDateFrom || (draftDateFrom && draftDateTo)) {
      setDraftDateFrom(picked);
      setDraftDateTo("");
      return;
    }

    if (picked < draftDateFrom) return;
    setDraftDateTo(picked);
  }, [draftDateFrom, draftDateTo]);

  const tileClassName = useCallback(({ date, view }) => {
    if (view !== "month") return "";
    const text = toDateText(date);
    if (!text || !draftDateFrom) return "";
    if (text === draftDateFrom) return "range-start";
    if (draftDateTo && text === draftDateTo) return "range-end";
    if (draftDateTo && text > draftDateFrom && text < draftDateTo) return "range-middle";
    return "";
  }, [draftDateFrom, draftDateTo]);

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
        dateFrom={dateFrom}
        dateTo={dateTo}
        clearDateRange={clearDateRange}
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
        <Modal title="Lọc lịch theo khoảng ngày" onClose={() => setModal(null)} className="date-range-modal">
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
                tileDisabled={({ date, view }) =>
                  view === "month" &&
                  Boolean(draftDateFrom && !draftDateTo && toDateText(date) < draftDateFrom)
                }
                value={
                  draftDateTo
                    ? [new Date(`${draftDateFrom}T00:00:00`), new Date(`${draftDateTo}T00:00:00`)]
                    : (draftDateFrom ? new Date(`${draftDateFrom}T00:00:00`) : new Date())
                }
              />
            </div>

            <div className="date-range-quick-actions">
              <button type="button" className="date-quick-btn" onClick={() => quickSetDateRange(7)}>7 ngày gần nhất</button>
              <button type="button" className="date-quick-btn" onClick={() => quickSetDateRange(30)}>30 ngày gần nhất</button>
              <button
                type="button"
                className="date-quick-btn"
                onClick={() => {
                  const now = new Date();
                  const from = new Date(now.getFullYear(), now.getMonth(), 1).toISOString().slice(0, 10);
                  const to = new Date(now.getFullYear(), now.getMonth() + 1, 0).toISOString().slice(0, 10);
                  setDraftDateFrom(from);
                  setDraftDateTo(to);
                }}
              >
                Tháng này
              </button>
            </div>

            <div className="date-range-footer">
              <button type="button" className="btn-secondary-outline" style={{ height: "42px", minWidth: "90px", borderRadius: "10px", padding: "0 16px" }} onClick={() => setModal(null)}>
                Đóng
              </button>
              <button
                type="button"
                className="btn-primary"
                style={{ height: "42px", minWidth: "110px", borderRadius: "10px", padding: "0 18px" }}
                onClick={applyDateRange}
                disabled={!draftDateFrom || !draftDateTo}
              >
                Áp dụng
              </button>
            </div>
          </div>
        </Modal>
      )}
    </div>
  );
}
