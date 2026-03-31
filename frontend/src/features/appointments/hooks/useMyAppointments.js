import { useCallback, useMemo, useState } from "react";
import { cancelAppointment, getMyAppointments } from "../api/appointmentApi";
import { getMyBookingGroups } from "../api/bookingGroupApi";
import { APPOINTMENT_STATUS } from "../constants/appointmentStatuses";
import {
  normalizeAppointment,
  normalizeAppointmentsResponse,
  normalizeBookingGroup,
  normalizeBookingGroupsResponse,
  sortAppointments,
  toDateTime,
} from "../utils/myAppointmentsData";

export function useMyAppointments() {
  const [appointments, setAppointments] = useState([]);
  const [bookingGroups, setBookingGroups] = useState([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState("");
  const [searchTerm, setSearchTerm] = useState("");
  const [cancelling, setCancelling] = useState(null);
  const [error, setError] = useState("");
  const [cancelTarget, setCancelTarget] = useState(null);
  const [cancelReason, setCancelReason] = useState("");

  const loadAppointments = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [appointmentsPayload, bookingGroupsPayload] = await Promise.all([
        getMyAppointments(),
        getMyBookingGroups(),
      ]);
      const list = normalizeAppointmentsResponse(appointmentsPayload)
        .map(normalizeAppointment)
        .filter((item) => item.id !== undefined && item.id !== null);
      const groups = normalizeBookingGroupsResponse(bookingGroupsPayload)
        .map(normalizeBookingGroup)
        .filter((item) => item.id !== undefined && item.id !== null);
      setAppointments(sortAppointments(list));
      setBookingGroups(groups);
    } catch (err) {
      setAppointments([]);
      setBookingGroups([]);
      setError(err?.message || "Không thể tải lịch hẹn. Vui lòng thử lại.");
    } finally {
      setLoading(false);
    }
  }, []);

  const openCancelPopup = useCallback((appointment) => {
    setCancelTarget(appointment);
    setCancelReason("");
    setError("");
  }, []);

  const closeCancelPopup = useCallback(() => {
    setCancelTarget(null);
    setCancelReason("");
  }, []);

  const handleConfirmCancel = useCallback(async () => {
    if (!cancelTarget) return;
    setCancelling(cancelTarget.id);
    setError("");
    try {
      await cancelAppointment(cancelTarget.id, cancelReason || "Khách hàng tự hủy");
      closeCancelPopup();
      await loadAppointments();
    } catch (err) {
      setError(err?.message || "Hủy lịch thất bại.");
    } finally {
      setCancelling(null);
    }
  }, [cancelReason, cancelTarget, closeCancelPopup, loadAppointments]);

  const stats = useMemo(() => {
    const now = Date.now();
    const upcomingCount = appointments.filter((item) => {
      const start = toDateTime(item.startTime)?.getTime() || 0;
      return start >= now && item.status !== APPOINTMENT_STATUS.CANCELED;
    }).length;

    return {
      total: appointments.length,
      upcoming: upcomingCount,
      confirmed: appointments.filter((item) => item.status === APPOINTMENT_STATUS.CONFIRMED).length,
      canceled: appointments.filter((item) => item.status === APPOINTMENT_STATUS.CANCELED).length,
    };
  }, [appointments]);

  const recentBookingGroups = useMemo(() => {
    return [...bookingGroups]
      .sort((a, b) => {
        const ta = toDateTime(a.createdAt)?.getTime() || 0;
        const tb = toDateTime(b.createdAt)?.getTime() || 0;
        return tb - ta;
      })
      .slice(0, 3);
  }, [bookingGroups]);

  const filtered = useMemo(() => {
    const term = searchTerm.trim().toLowerCase();
    return appointments.filter((item) => {
      if (filter && item.status !== filter) return false;
      if (!term) return true;

      const combined = [
        item.branchName,
        item.vehicleLicensePlate,
        item.vehicleModelName,
        Array.isArray(item.serviceNames) ? item.serviceNames.join(" ") : "",
      ]
        .filter(Boolean)
        .join(" ")
        .toLowerCase();

      return combined.includes(term);
    });
  }, [appointments, filter, searchTerm]);

  return {
    loading,
    filter,
    searchTerm,
    cancelling,
    error,
    cancelTarget,
    cancelReason,
    stats,
    recentBookingGroups,
    filtered,
    setFilter,
    setSearchTerm,
    setCancelReason,
    setError,
    loadAppointments,
    openCancelPopup,
    closeCancelPopup,
    handleConfirmCancel,
  };
}
