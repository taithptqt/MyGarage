import { useCallback, useMemo, useState } from "react";
import { cancelAppointment, getMyAppointments } from "@features/appointments/api/appointmentApi";
import { getMyBookingGroupDetail, getMyBookingGroups } from "@features/appointments/api/bookingGroupApi";
import { APPOINTMENT_STATUS } from "@features/appointments/constants/appointmentStatuses";
import {
  normalizeAppointment,
  normalizeAppointmentsResponse,
  normalizeBookingGroup,
  normalizeBookingGroupsResponse,
  sortAppointments,
  toDateTime,
} from "@features/appointments/utils/myAppointmentsData";

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

      const groupsHeader = normalizeBookingGroupsResponse(bookingGroupsPayload)
        .map(normalizeBookingGroup)
        .filter((item) => item.id !== undefined && item.id !== null);

      const groups = await Promise.all(
        groupsHeader.map(async (group) => {
          if (!group.id) return group;
          try {
            const detailPayload = await getMyBookingGroupDetail(group.id);
            return normalizeBookingGroup(detailPayload);
          } catch {
            return group;
          }
        }),
      );

      const groupedAppointmentIds = new Set(
        groups
          .flatMap((group) => (Array.isArray(group.appointments) ? group.appointments : []))
          .map((item) => item.id),
      );

      const standaloneAppointments = list.filter((item) => !groupedAppointmentIds.has(item.id));

      setAppointments(sortAppointments(standaloneAppointments));
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

  const allAppointments = useMemo(() => {
    return [
      ...appointments,
      ...bookingGroups.flatMap((group) => (Array.isArray(group.appointments) ? group.appointments : [])),
    ];
  }, [appointments, bookingGroups]);

  const stats = useMemo(() => {
    const now = Date.now();
    const upcomingCount = allAppointments.filter((item) => {
      const start = toDateTime(item.startTime)?.getTime() || 0;
      return start >= now && item.status !== APPOINTMENT_STATUS.CANCELED;
    }).length;

    return {
      total: allAppointments.length,
      upcoming: upcomingCount,
      confirmed: allAppointments.filter((item) => item.status === APPOINTMENT_STATUS.CONFIRMED).length,
      canceled: allAppointments.filter((item) => item.status === APPOINTMENT_STATUS.CANCELED).length,
    };
  }, [allAppointments]);

  const bookingGroupsSorted = useMemo(() => {
    return [...bookingGroups].sort((a, b) => {
      const ta = toDateTime(a.createdAt)?.getTime() || 0;
      const tb = toDateTime(b.createdAt)?.getTime() || 0;
      return tb - ta;
    });
  }, [bookingGroups]);

  const matchSearchTerm = useCallback((item, term) => {
    if (!term) return true;

    const combined = [
      item?.branchName,
      item?.vehicleLicensePlate,
      item?.vehicleModelName,
      Array.isArray(item?.serviceNames) ? item.serviceNames.join(" ") : "",
    ]
      .filter(Boolean)
      .join(" ")
      .toLowerCase();

    return combined.includes(term);
  }, []);

  const filteredAppointments = useMemo(() => {
    const term = searchTerm.trim().toLowerCase();
    return appointments.filter((item) => {
      if (filter && item.status !== filter) return false;
      return matchSearchTerm(item, term);
    });
  }, [appointments, filter, searchTerm, matchSearchTerm]);

  const filteredBookingGroups = useMemo(() => {
    const term = searchTerm.trim().toLowerCase();

    return bookingGroupsSorted
      .map((group) => {
        const groupCodeMatches = String(group.groupCode || "").toLowerCase().includes(term);
        const groupBranchMatches = String(group.branchName || "").toLowerCase().includes(term);

        const appointmentsInGroup = (Array.isArray(group.appointments) ? group.appointments : []).filter((item) => {
          if (filter && item.status !== filter) return false;
          if (!term) return true;
          if (groupCodeMatches || groupBranchMatches) return true;
          return matchSearchTerm(item, term);
        });

        return {
          ...group,
          appointments: appointmentsInGroup,
        };
      })
      .filter((group) => group.appointments.length > 0 || (!filter && !term));
  }, [bookingGroupsSorted, filter, searchTerm, matchSearchTerm]);

  return {
    loading,
    filter,
    searchTerm,
    cancelling,
    error,
    cancelTarget,
    cancelReason,
    stats,
    bookingGroups: bookingGroupsSorted,
    filteredAppointments,
    filteredBookingGroups,
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
