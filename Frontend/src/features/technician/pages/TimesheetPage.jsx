import React, { useState, useEffect, useCallback, useMemo } from "react";
import { request } from "@app/apiClient";
import { useAuth } from "@shared/core/context/AuthContext";
import { useBranch } from "@shared/core/context/BranchContext";
import "./Timesheet.css";

const UI_TEXT = {
  title: "L?CH CÔNG TÁC & CH?M CÔNG HÀNG NGÀY",
  breadcrumbHome: "Home",
  breadcrumbPage: "Schedule",
  filterShift: "Ca làm",
  staffColumnTeam: "T? nhóm",
  staffColumnName: "Nhân viên",
  staffColumnShiftStatus: "Tr?ng thái ca",
  staffColumnHours: "Gi? công tích luy",
  summaryStatus: "Tr?ng thái hôm nay",
  summaryHours: "Gi? công tích luy",
  summaryPending: "Công vi?c dang ch?",
  shiftLabel: "Ca sáng: 08:00 - 12:00",
  serverTime: "Gi? máy ch?",
  checkIn: "Check-in",
  checkOut: "Check-out",
  locationVerified: "GPS dã xác th?c",
  locationPending: "Ch? xác th?c v? trí",
  todayHistory: "L?ch s? ch?m công hôm nay",
  emptyHistory: "Chua có b?n ghi nào.",
  loading: "Ðang t?i d? li?u ch?m công...",
};

const VIEW_OPTIONS = [
  { value: "day", label: "Day" },
  { value: "week", label: "Week" },
  { value: "month", label: "Month" },
];

const SHIFT_OPTIONS = [
  { value: "all", label: "T?t c? ca" },
  { value: "morning", label: "Ca sáng" },
  { value: "afternoon", label: "Ca chi?u" },
];

const ATTENDANCE_STATUS_LABELS = {
  early: "Ð?n s?m",
  ontime: "Ðúng gi?",
  late: "Ði mu?n",
  absent: "V?ng m?t",
  leave: "Ngh? phép",
};

const TIME_SLOTS = ["08:00", "09:00", "10:00", "11:00", "12:00", "13:00", "14:00", "15:00", "16:00", "17:00", "18:00"];

function pad(n) {
  return n < 10 ? `0${n}` : `${n}`;
}

function formatTime(secs) {
  const h = Math.floor(secs / 3600);
  const m = Math.floor((secs % 3600) / 60);
  const s = secs % 60;
  return `${pad(h)}:${pad(m)}:${pad(s)}`;
}

function toMinutes(timeValue) {
  const [hour, minute] = String(timeValue).split(":").map(Number);
  return hour * 60 + minute;
}

function toHHmm(value) {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  return `${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function readValue(source, ...keys) {
  for (const key of keys) {
    const value = source?.[key];
    if (value !== undefined && value !== null && value !== "") {
      return value;
    }
  }
  return "";
}

function resolveDepartmentByRole(roleCodeRaw) {
  const roleCode = String(roleCodeRaw || "").toUpperCase();
  if (["TECH", "TECHNICIAN", "FOREMAN", "TEAM_LEADER"].includes(roleCode)) return "Mechanics";
  if (["RECEPTIONIST", "SERVICE_ADVISOR", "ADVISOR"].includes(roleCode)) return "Service Advisors";
  if (["STORE_KEEPER", "WAREHOUSE", "INVENTORY"].includes(roleCode)) return "Warehouse";
  return "Back Office";
}

function deriveAttendanceStatus(staffStatus, activeEntry) {
  const apiStatus = String(activeEntry?.attendanceStatus || activeEntry?.AttendanceStatus || "").trim().toUpperCase();
  if (apiStatus === "EARLY") return "early";
  if (apiStatus === "ON_TIME") return "ontime";
  if (apiStatus === "LATE") return "late";
  if (activeEntry) return "ontime";
  const normalized = String(staffStatus || "").trim().toLowerCase();
  if (normalized === "inactive") return "leave";
  if (normalized === "suspended") return "absent";
  return "late";
}

function buildTaskBlocks(assignmentsRaw) {
  const items = Array.isArray(assignmentsRaw) ? assignmentsRaw : [];
  return items
    .filter((item) => {
      const status = String(readValue(item, "status", "Status")).toUpperCase();
      return status !== "PAUSED" && status !== "CANCELED";
    })
    .slice(0, 5)
    .map((item, index) => {
      const code =
        String(readValue(item, "serviceOrderCode", "ServiceOrderCode", "orderCode", "OrderCode")).trim() ||
        `SO-${24000 + Number(readValue(item, "id", "Id") || index + 1)}`;

      const startedAt = readValue(item, "startedAt", "StartedAt", "startTime", "StartTime", "createdAt", "CreatedAt");
      const completedAt = readValue(item, "completedAt", "CompletedAt", "endTime", "EndTime");
      const start = toHHmm(startedAt) || `${8 + index}:00`;
      const end = toHHmm(completedAt) || `${Math.min(18, 10 + index)}:00`;
      const normalizedStart = toMinutes(start) < 8 * 60 ? "08:00" : start;
      const normalizedEnd = toMinutes(end) <= toMinutes(normalizedStart)
        ? `${pad(Math.min(18, Math.floor(toMinutes(normalizedStart) / 60) + 2))}:00`
        : end;

      const status = String(readValue(item, "status", "Status")).toUpperCase();
      const completed = status === "COMPLETED" || Boolean(completedAt);

      return {
        code,
        start: normalizedStart,
        end: normalizedEnd,
        status: completed ? "completed" : "inProgress",
      };
    });
}

export default function TimesheetPage() {
  const { user } = useAuth();
  const { selectedBranchId } = useBranch();

  const [activeEntry, setActiveEntry] = useState(null);
  const [todayEntries, setTodayEntries] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [now, setNow] = useState(new Date());
  const [dateValue, setDateValue] = useState(() => new Date().toISOString().slice(0, 10));
  const [viewMode, setViewMode] = useState("day");
  const [shiftFilter, setShiftFilter] = useState("all");
  const [personalTasks, setPersonalTasks] = useState([]);
  const [gpsVerified, setGpsVerified] = useState(false);

  const currentStaffId = user?.id ?? user?.staffId;
  const currentBranchId =
    Number(user?.branchId) ||
    Number(user?.BranchId) ||
    (Number(selectedBranchId) > 0 ? Number(selectedBranchId) : null);
  const currentProfileName = user?.fullName || user?.name || "Nhân viên k? thu?t";
  const currentDepartment = resolveDepartmentByRole(user?.roleCode || user?.role);

  const fetchTimesheet = useCallback(async () => {
    if (!currentStaffId) {
      setTodayEntries([]);
      setActiveEntry(null);
      setError("Thi?u thông tin nhân viên d? t?i ch?m công.");
      setLoading(false);
      return;
    }
    setLoading(true);
    setError("");
    try {
      const res = await request({
        url: "/api/timesheets/today",
        method: "GET",
        params: { staffId: currentStaffId },
      });
      const data = res?.items ?? res ?? [];
      setTodayEntries(data);
      const active = data.find((entry) => !entry.checkOutTime && !entry.CheckOutTime);
      setActiveEntry(active || null);
    } catch (fetchError) {
      setError(fetchError?.response?.data?.message || "Không th? t?i d? li?u ch?m công hôm nay.");
    } finally {
      setLoading(false);
    }
  }, [currentStaffId]);

  const fetchPersonalAssignments = useCallback(async () => {
    try {
      const res = await request({ url: "/api/service-staff-assignments", method: "GET" });
      const allAssignments = Array.isArray(res) ? res : res?.items ?? res?.data ?? [];
      const mine = allAssignments.filter((item) => Number(readValue(item, "technicianId", "TechnicianId")) === Number(currentStaffId));
      setPersonalTasks(buildTaskBlocks(mine));
    } catch {
      setPersonalTasks([]);
    }
  }, [currentStaffId]);

  useEffect(() => {
    fetchTimesheet();
    fetchPersonalAssignments();
    const timer = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(timer);
  }, [fetchTimesheet, fetchPersonalAssignments]);

  const totalSecondsToday = useMemo(() => {
    let total = 0;
    todayEntries.forEach((entry) => {
      const start = new Date(entry.checkInTime || entry.CheckInTime);
      const endValue = entry.checkOutTime || entry.CheckOutTime;
      const end = endValue ? new Date(endValue) : now;
      if (!Number.isNaN(start.getTime()) && !Number.isNaN(end.getTime()) && end >= start) {
        total += Math.floor((end - start) / 1000);
      }
    });
    return total;
  }, [todayEntries, now]);

  const attendanceStatus = deriveAttendanceStatus(user?.status, activeEntry);

  const personalMember = useMemo(() => ({
    id: String(currentStaffId || "me"),
    name: currentProfileName,
    attendanceStatus,
    shift: personalTasks.length > 0 && personalTasks.some((task) => toMinutes(task.start) >= 12 * 60) ? "afternoon" : "morning",
    shiftLabel: personalTasks.length > 0 && personalTasks.some((task) => toMinutes(task.start) >= 12 * 60) ? "Ca chi?u" : "Ca sáng",
    accumulatedHours: formatTime(totalSecondsToday).slice(0, 5),
    tasks: personalTasks,
  }), [attendanceStatus, currentProfileName, currentStaffId, personalTasks, totalSecondsToday]);

  const filteredGroups = useMemo(() => {
    const byShift = shiftFilter === "all" || personalMember.shift === shiftFilter;
    if (!byShift) return [];
    return [{ department: currentDepartment, members: [personalMember] }];
  }, [currentDepartment, personalMember, shiftFilter]);

  const summary = useMemo(() => ({
    status: ATTENDANCE_STATUS_LABELS[attendanceStatus] || "Chua xác d?nh",
    workedHours: formatTime(totalSecondsToday).slice(0, 5),
    pendingTasks: personalTasks.filter((task) => task.status === "inProgress").length,
  }), [attendanceStatus, personalTasks, totalSecondsToday]);

  const locationVerified = gpsVerified || Boolean(activeEntry);
  const timelineStart = toMinutes(TIME_SLOTS[0]);
  const timelineEnd = toMinutes(TIME_SLOTS[TIME_SLOTS.length - 1]);

  const getCurrentPosition = () =>
    new Promise((resolve, reject) => {
      if (!navigator.geolocation) {
        reject(new Error("Thi?t b? không h? tr? GPS."));
        return;
      }
      navigator.geolocation.getCurrentPosition(
        (position) => resolve(position),
        (geoError) => reject(geoError),
        { enableHighAccuracy: true, timeout: 12000, maximumAge: 0 }
      );
    });

  const handleCheckIn = async () => {
    setError("");
    if (!currentStaffId) {
      setError("Thi?u thông tin nhân viên.");
      return;
    }
    try {
      const position = await getCurrentPosition();
      const latitude = position?.coords?.latitude;
      const longitude = position?.coords?.longitude;
      const accuracyMeters = position?.coords?.accuracy;

      if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) {
        setGpsVerified(false);
        setError("Không l?y du?c t?a d? GPS h?p l?.");
        return;
      }

      await request({
        url: "/api/timesheets/check-in",
        method: "POST",
        data: {
          staffId: currentStaffId,
          branchId: currentBranchId,
          latitude,
          longitude,
          accuracyMeters,
        },
      });
      setGpsVerified(true);
      await fetchTimesheet();
    } catch (actionError) {
      setGpsVerified(false);
      setError(actionError?.response?.data?.message || "L?i khi Check-in.");
    }
  };

  const handleCheckOut = async () => {
    if (!activeEntry) return;
    setError("");
    try {
      const id = activeEntry.id || activeEntry.Id;
      await request({
        url: `/api/timesheets/${id}/check-out`,
        method: "PUT",
        data: {},
      });
      await fetchTimesheet();
    } catch (actionError) {
      setError(actionError?.response?.data?.message || "L?i khi Check-out.");
    }
  };

  return (
    <div className="schedule-page">
      <section className="schedule-shell">
        <header className="schedule-topnav">
          <div className="schedule-breadcrumbs">
            <span>{UI_TEXT.breadcrumbHome}</span>
            <span className="schedule-breadcrumb-sep">&gt;</span>
            <span>{UI_TEXT.breadcrumbPage}</span>
          </div>
          <div className="schedule-toolbar">
            <div className="schedule-view-switch" role="tablist" aria-label="Date view mode">
              {VIEW_OPTIONS.map((view) => (
                <button
                  key={view.value}
                  type="button"
                  className={`schedule-view-btn ${viewMode === view.value ? "is-active" : ""}`}
                  onClick={() => setViewMode(view.value)}
                >
                  {view.label}
                </button>
              ))}
            </div>
            <input type="date" className="schedule-date-picker" value={dateValue} onChange={(event) => setDateValue(event.target.value)} />
            <div className="schedule-profile" title={currentProfileName}>
              <span className="schedule-profile-avatar">{currentProfileName.charAt(0).toUpperCase()}</span>
              <span className="schedule-profile-name">{currentProfileName}</span>
            </div>
          </div>
        </header>

        <div className="schedule-title-wrap">
          <h1 className="schedule-page-title">{UI_TEXT.title}</h1>
          <p className="schedule-page-subtitle">Theo dõi l?ch phân công và ch?m công cá nhân theo th?i gian th?c.</p>
        </div>

        {error && <div className="schedule-alert">{error}</div>}

        <section className="schedule-summary-row">
          <article className="summary-card">
            <span className="summary-label">{UI_TEXT.summaryStatus}</span>
            <strong className="summary-value">{summary.status}</strong>
          </article>
          <article className="summary-card">
            <span className="summary-label">{UI_TEXT.summaryHours}</span>
            <strong className="summary-value">{summary.workedHours}</strong>
          </article>
          <article className="summary-card">
            <span className="summary-label">{UI_TEXT.summaryPending}</span>
            <strong className="summary-value">{summary.pendingTasks}</strong>
          </article>
        </section>

        <section className="schedule-main-layout">
          <aside className="attendance-panel">
            <h2 className="attendance-title">Check-in / Check-out</h2>
            <div className="attendance-server-time">
              <span>{UI_TEXT.serverTime}</span>
              <strong>{now.toLocaleTimeString("vi-VN", { hour12: false })}</strong>
            </div>
            <div className="attendance-shift">{UI_TEXT.shiftLabel}</div>
            <div className={`attendance-location ${locationVerified ? "is-ok" : "is-pending"}`}>
              <span className="attendance-location-dot" />
              <span>{locationVerified ? UI_TEXT.locationVerified : UI_TEXT.locationPending}</span>
            </div>
            <div className="attendance-total-hours">
              Ðã làm hôm nay: <strong>{formatTime(totalSecondsToday)}</strong>
            </div>
            <div className="attendance-actions">
              {activeEntry ? (
                <button type="button" className="attendance-btn checkout" onClick={handleCheckOut}>
                  {UI_TEXT.checkOut}
                </button>
              ) : (
                <button type="button" className="attendance-btn checkin" onClick={handleCheckIn}>
                  {UI_TEXT.checkIn}
                </button>
              )}
            </div>
            <div className="attendance-history">
              <h3>{UI_TEXT.todayHistory}</h3>
              {loading ? (
                <p>{UI_TEXT.loading}</p>
              ) : todayEntries.length === 0 ? (
                <p>{UI_TEXT.emptyHistory}</p>
              ) : (
                <ul>
                  {todayEntries.map((entry) => {
                    const checkInTime = entry.checkInTime || entry.CheckInTime;
                    const checkOutTime = entry.checkOutTime || entry.CheckOutTime;
                    return (
                      <li key={entry.id || entry.Id}>
                        <span>In: {new Date(checkInTime).toLocaleTimeString("vi-VN")}</span>
                        <span>{checkOutTime ? `Out: ${new Date(checkOutTime).toLocaleTimeString("vi-VN")}` : "Ðang làm..."}</span>
                      </li>
                    );
                  })}
                </ul>
              )}
            </div>
          </aside>

          <section className="schedule-grid-panel">
            <div className="schedule-filters">
              <label>
                <span>{UI_TEXT.filterShift}</span>
                <select value={shiftFilter} onChange={(event) => setShiftFilter(event.target.value)}>
                  {SHIFT_OPTIONS.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </select>
              </label>
            </div>

            <div className="timeline-scroll">
              <div className="timeline-header-row">
                <div className="timeline-staff-columns">
                  <span>{UI_TEXT.staffColumnTeam}</span>
                  <span>{UI_TEXT.staffColumnName}</span>
                  <span>{UI_TEXT.staffColumnShiftStatus}</span>
                  <span>{UI_TEXT.staffColumnHours}</span>
                </div>
                <div className="timeline-hours">
                  {TIME_SLOTS.map((slot) => (
                    <span key={slot}>{slot}</span>
                  ))}
                </div>
              </div>

              {filteredGroups.map((group) => (
                <div key={group.department} className="timeline-department-group">
                  <div className="timeline-department-title">{group.department}</div>
                  {group.members.map((member) => (
                    <div className="timeline-staff-row" key={member.id}>
                      <div className="timeline-staff-columns">
                        <span>{group.department}</span>
                        <span className="timeline-staff-name">{member.name}</span>
                        <span className="timeline-attendance">
                          <i className={`attendance-dot ${member.attendanceStatus}`} />
                          {ATTENDANCE_STATUS_LABELS[member.attendanceStatus]}
                        </span>
                        <span>{member.accumulatedHours}</span>
                      </div>

                      <div className="timeline-track">
                        {TIME_SLOTS.map((slot) => (
                          <span key={`${member.id}-${slot}`} className="timeline-cell" />
                        ))}
                        {member.tasks.map((task) => {
                          const taskStart = toMinutes(task.start);
                          const taskEnd = toMinutes(task.end);
                          const clampedStart = Math.max(taskStart, timelineStart);
                          const clampedEnd = Math.min(taskEnd, timelineEnd);
                          const left = ((clampedStart - timelineStart) / 60) * 100;
                          const width = ((clampedEnd - clampedStart) / 60) * 100;
                          return (
                            <div
                              key={`${member.id}-${task.code}`}
                              className={`timeline-task ${task.status === "inProgress" ? "in-progress" : "completed"}`}
                              style={{ left: `${left}px`, width: `${Math.max(width, 54)}px` }}
                              title={`${task.code} (${task.start} - ${task.end})`}
                            >
                              <strong>{task.code}</strong>
                              <span>{member.shiftLabel}</span>
                            </div>
                          );
                        })}
                      </div>
                    </div>
                  ))}
                </div>
              ))}
            </div>
          </section>
        </section>
      </section>
    </div>
  );
}
