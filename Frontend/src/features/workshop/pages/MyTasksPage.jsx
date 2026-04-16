import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { request } from "@app/apiClient";
import { executionApi } from "@features/workshop/api/executionApi";
import { useAuth } from "@shared/core/context/AuthContext";
import "./MyTasks.css";

/* ──────── constants ──────── */
const TASK_STATUS_META = {
  PENDING:     { label: "Chờ thực hiện", color: "#6366f1", bg: "#eef2ff", icon: "⏳" },
  IN_PROGRESS: { label: "Đang làm",      color: "#0ea5e9", bg: "#e0f2fe", icon: "🔧" },
  INPROGRESS:  { label: "Đang làm",      color: "#0ea5e9", bg: "#e0f2fe", icon: "🔧" },
  PAUSED:      { label: "Tạm dừng",      color: "#f59e0b", bg: "#fffbeb", icon: "⏸️" },
  QC_PENDING:  { label: "Chờ QC",        color: "#8b5cf6", bg: "#f5f3ff", icon: "🔍" },
  COMPLETED:   { label: "Hoàn thành",    color: "#10b981", bg: "#d1fae5", icon: "✅" },
  CANCELLED:   { label: "Đã hủy",        color: "#94a3b8", bg: "#f1f5f9", icon: "❌" },
};
const defaultTaskMeta = { label: "Không rõ", color: "#94a3b8", bg: "#f1f5f9", icon: "❓" };

function getTaskMeta(status = "") {
  return TASK_STATUS_META[String(status).toUpperCase().replace(/-/g, "_")] || defaultTaskMeta;
}

const CHECKIN_STATUS_META = {
  CHECKED_IN: { label: "Đang làm việc", color: "#10b981", bg: "#d1fae5", icon: "🟢" },
  CHECKED_OUT: { label: "Đã kết ca",    color: "#94a3b8", bg: "#f1f5f9", icon: "⬛" },
};

/* ──────── helpers ──────── */
function fmt(v, onlyTime = false) {
  if (!v) return "--";
  const opts = onlyTime
    ? { hour: "2-digit", minute: "2-digit", timeZone: "Asia/Ho_Chi_Minh" }
    : { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit", timeZone: "Asia/Ho_Chi_Minh" };
  return new Date(v).toLocaleString("vi-VN", opts);
}
function fmtMins(mins) {
  if (!mins || mins <= 0) return "--";
  if (mins < 60) return `${mins} phút`;
  return `${Math.floor(mins / 60)}g${mins % 60 > 0 ? ` ${mins % 60}p` : ""}`;
}
function calcDuration(start, end = new Date()) {
  if (!start) return 0;
  return Math.round((new Date(end).getTime() - new Date(start).getTime()) / 60_000);
}

/* ──────── sub-components ──────── */
function TimesheetBar({ timesheet, onCheckIn, onCheckOut, saving }) {
  const status = timesheet ? "CHECKED_IN" : "CHECKED_OUT";
  const meta   = CHECKIN_STATUS_META[status];
  const elapsed = timesheet
    ? calcDuration(timesheet.checkInTime ?? timesheet.CheckInTime)
    : 0;

  return (
    <div className="mt-timesheet-bar">
      <div className="mt-timesheet-info">
        <span className="mt-ts-icon">{meta.icon}</span>
        <div>
          <span className="mt-ts-status" style={{ color: meta.color }}>{meta.label}</span>
          {timesheet && (
            <span className="mt-ts-since">
              Từ {fmt(timesheet.checkInTime ?? timesheet.CheckInTime, true)} · {fmtMins(elapsed)} đã làm
            </span>
          )}
        </div>
      </div>
      <div className="mt-ts-actions">
        {!timesheet ? (
          <button
            className="mt-btn-checkin"
            onClick={onCheckIn}
            disabled={saving}
          >
            {saving ? "⏳" : "🟢"} Check-in vào ca
          </button>
        ) : (
          <button
            className="mt-btn-checkout"
            onClick={onCheckOut}
            disabled={saving}
          >
            {saving ? "⏳" : "⬛"} Kết ca
          </button>
        )}
      </div>
    </div>
  );
}

function QuickStatCard({ icon, label, value, color }) {
  return (
    <div className="mt-stat" style={{ borderTopColor: color }}>
      <span className="mt-stat-icon">{icon}</span>
      <span className="mt-stat-val" style={{ color }}>{value}</span>
      <span className="mt-stat-lbl">{label}</span>
    </div>
  );
}

function TaskCard({ task, onStart, onPause, onComplete, onViewOrder }) {
  const meta = getTaskMeta(task.status);
  const s    = String(task.status ?? "").toUpperCase().replace(/-/g, "_");
  const elapsed = s === "IN_PROGRESS" || s === "INPROGRESS"
    ? calcDuration(task.startedAt)
    : null;

  return (
    <article
      className="mt-task-card"
      style={{ borderLeftColor: meta.color }}
    >
      {/* card header */}
      <div className="mt-task-head">
        <div className="mt-task-title-row">
          <span className="mt-task-icon">{meta.icon}</span>
          <span className="mt-task-name">{task.name || task.taskName || "Hạng mục sửa chữa"}</span>
        </div>
        <span
          className="mt-task-status-badge"
          style={{ background: meta.bg, color: meta.color }}
        >
          {meta.label}
        </span>
      </div>

      {/* order info */}
      <div className="mt-task-order" onClick={() => onViewOrder(task)}>
        <div className="mt-order-row">
          <span className="mt-lbl">Lệnh SC:</span>
          <span className="mt-val mt-code">{task.serviceOrderCode || `SO-${task.serviceOrderId}`}</span>
        </div>
        <div className="mt-order-row">
          <span className="mt-lbl">Xe:</span>
          <span className="mt-val">🚘 {task.vehicleLicensePlate || "--"}</span>
        </div>
        {task.vehicleDescriptor && (
          <div className="mt-order-row">
            <span className="mt-lbl">Mẫu xe:</span>
            <span className="mt-val">{task.vehicleDescriptor}</span>
          </div>
        )}
        {task.bayCode && (
          <div className="mt-order-row">
            <span className="mt-lbl">Khoang:</span>
            <span className="mt-val">🏭 {task.bayCode}</span>
          </div>
        )}
      </div>

      {/* detail */}
      {task.description && (
        <p className="mt-task-desc">{task.description}</p>
      )}

      {/* timing */}
      <div className="mt-task-timing">
        {task.estimatedDuration ? (
          <span title="Thời gian ước tính">
            📋 Dự kiến: {fmtMins(task.estimatedDuration)}
          </span>
        ) : null}
        {elapsed !== null ? (
          <span className="mt-elapsed" title="Đã thực hiện được">
            ⏱️ Đang làm: {fmtMins(elapsed)}
          </span>
        ) : null}
        {task.completedAt && (
          <span title="Hoàn thành lúc">✅ {fmt(task.completedAt)}</span>
        )}
      </div>

      {/* role */}
      {task.roleInTask && (
        <div className="mt-task-role">
          {task.roleInTask === "PRIMARY" ? "🎯 Thợ chính" : "🤝 Thợ phụ"}
        </div>
      )}

      {/* actions */}
      <div className="mt-task-actions" onClick={(e) => e.stopPropagation()}>
        {(s === "PENDING") && (
          <button className="mt-btn-start" onClick={() => onStart(task)}>
            ▶️ Bắt đầu
          </button>
        )}
        {(s === "IN_PROGRESS" || s === "INPROGRESS") && (
          <>
            <button className="mt-btn-pause" onClick={() => onPause(task)}>
              ⏸️ Tạm dừng
            </button>
            <button className="mt-btn-done" onClick={() => onComplete(task)}>
              ✅ Hoàn thành
            </button>
          </>
        )}
        {s === "PAUSED" && (
          <button className="mt-btn-start" onClick={() => onStart(task)}>
            ▶️ Tiếp tục
          </button>
        )}
        <button className="mt-btn-view" onClick={() => onViewOrder(task)}>
          📋 Xem lệnh
        </button>
      </div>
    </article>
  );
}

/* ──────── empty state ──────── */
function EmptyState({ filter }) {
  const msgs = {
    ACTIVE: { icon: "🎉", title: "Không có tác vụ đang thực hiện", desc: "Bạn chưa có hạng mục nào đang chờ hoặc đang làm." },
    ALL:    { icon: "📋", title: "Chưa có tác vụ nào", desc: "Quản đốc chưa phân công tác vụ cho bạn hôm nay." },
    COMPLETED: { icon: "✅", title: "Chưa hoàn thành tác vụ nào", desc: "Hoàn thành nhiệm vụ đầu tiên để thấy tại đây." },
  };
  const m = msgs[filter] || msgs.ALL;
  return (
    <div className="mt-empty">
      <span className="mt-empty-icon">{m.icon}</span>
      <strong>{m.title}</strong>
      <p>{m.desc}</p>
    </div>
  );
}

/* ──────── main page ──────── */
export default function MyTasksPage() {
  const { user } = useAuth();
  const navigate = useNavigate();

  const techId = user?.id ?? user?.staffId ?? null;

  const [tasks, setTasks]         = useState([]);
  const [timesheet, setTimesheet] = useState(null);
  const [loading, setLoading]     = useState(false);
  const [saving, setSaving]       = useState(false);
  const [error, setError]         = useState("");
  const [success, setSuccess]     = useState("");
  const [filter, setFilter]       = useState("ACTIVE");
  const [lastRefresh, setLastRefresh] = useState(new Date());

  /* ── fetch ── */
  const fetchData = useCallback(async () => {
    if (!techId) return;
    setLoading(true);
    setError("");
    try {
      const [assignRes, allOrdersRes] = await Promise.all([
        request({ url: "/api/service-staff-assignments", method: "GET", params: { staffId: techId } }),
        request({ url: "/api/service-orders", method: "GET" }),
      ]);
      const assigns = Array.isArray(assignRes) ? assignRes : (assignRes?.items || []);
      const allOrders = Array.isArray(allOrdersRes) ? allOrdersRes : (allOrdersRes?.items || []);

      /* Build task list from assignments */
      const orderMap = new Map(allOrders.map(o => [o.id ?? o.Id, o]));
      const enriched = assigns.map(a => {
        const soId = a.serviceOrderId ?? a.ServiceOrderId;
        const so   = orderMap.get(soId) || {};
        return {
          assignmentId: a.id ?? a.Id,
          serviceOrderId: soId,
          serviceOrderCode: a.serviceOrderCode ?? a.ServiceOrderCode ?? so.code ?? so.Code ?? `SO-${soId}`,
          vehicleLicensePlate: a.vehicleLicensePlate ?? a.VehicleLicensePlate ?? so.vehicleLicensePlate ?? so.VehicleLicensePlate ?? "--",
          vehicleDescriptor:   a.vehicleDescriptor   ?? a.VehicleDescriptor   ?? so.vehicleDescriptor   ?? so.VehicleDescriptor   ?? "",
          bayCode: a.bayCode ?? a.BayCode ?? so.bayCode ?? so.BayCode ?? "",
          name:        a.taskName     ?? a.TaskName     ?? a.serviceItemName ?? a.ServiceItemName ?? "Hạng mục",
          description: a.description  ?? a.Description  ?? "",
          status:      a.status       ?? a.Status       ?? "PENDING",
          roleInTask:  a.roleInTask   ?? a.RoleInTask   ?? "PRIMARY",
          estimatedDuration: a.estimatedDuration ?? a.EstimatedDuration ?? null,
          startedAt:    a.startedAt   ?? a.StartedAt   ?? null,
          completedAt:  a.completedAt ?? a.CompletedAt ?? null,
          assignedAt:   a.assignedAt  ?? a.AssignedAt  ?? null,
        };
      });

      setTasks(enriched);
      setLastRefresh(new Date());
    } catch (err) {
      setError("Không thể tải danh sách tác vụ. Kiểm tra kết nối.");
      console.error(err);
    } finally {
      setLoading(false);
    }
  }, [techId]);

  const fetchTimesheet = useCallback(async () => {
    if (!techId) return;
    try {
      const res = await request({ url: `/api/timesheets/active`, method: "GET", params: { staffId: techId } })
        .catch(() => null);
      if (res && (res.id || res.Id)) setTimesheet(res);
      else setTimesheet(null);
    } catch {
      setTimesheet(null);
    }
  }, [techId]);

  useEffect(() => {
    fetchData();
    fetchTimesheet();
  }, [fetchData, fetchTimesheet]);

  useEffect(() => {
    const id = setInterval(() => { fetchData(); fetchTimesheet(); }, 30_000);
    return () => clearInterval(id);
  }, [fetchData, fetchTimesheet]);

  /* ── timesheet actions ── */
  const handleCheckIn = async () => {
    setSaving(true);
    setError("");
    setSuccess("");
    try {
      const res = await executionApi.checkInTimesheet({
        staffId: techId,
        workDate: new Date().toISOString().slice(0, 10),
        notes: "Check-in từ MyTasks",
      });
      setTimesheet(res);
      setSuccess("✅ Đã check-in thành công!");
    } catch (err) {
      setError(err?.response?.data?.message || err?.message || "Check-in thất bại.");
    } finally {
      setSaving(false);
    }
  };

  const handleCheckOut = async () => {
    if (!timesheet) return;
    if (!window.confirm("Xác nhận kết ca?")) return;
    setSaving(true);
    setError("");
    setSuccess("");
    try {
      const tsId = timesheet.id ?? timesheet.Id;
      await executionApi.checkOutTimesheet(tsId, {
        checkOutTime: new Date().toISOString(),
        notes: "Check-out từ MyTasks",
      });
      setTimesheet(null);
      setSuccess("✅ Đã kết ca thành công!");
    } catch (err) {
      setError(err?.response?.data?.message || err?.message || "Check-out thất bại.");
    } finally {
      setSaving(false);
    }
  };

  /* ── task actions ── */
  const showMsg = (msg) => {
    setSuccess(msg);
    setTimeout(() => setSuccess(""), 3000);
  };

  const handleStart = async (task) => {
    setSaving(true);
    setError("");
    try {
      await request({
        url: `/api/service-staff-assignments/${task.assignmentId}/start`,
        method: "PUT",
        data: { startedAt: new Date().toISOString() },
      });
      await fetchData();
      showMsg("▶️ Đã bắt đầu tác vụ.");
    } catch (err) {
      setError(err?.response?.data?.message || "Không thể cập nhật tác vụ.");
    } finally {
      setSaving(false);
    }
  };

  const handlePause = async (task) => {
    setSaving(true);
    setError("");
    try {
      await request({
        url: `/api/service-staff-assignments/${task.assignmentId}/pause`,
        method: "PUT",
      });
      await fetchData();
      showMsg("⏸️ Đã tạm dừng tác vụ.");
    } catch (err) {
      setError(err?.response?.data?.message || "Không thể tạm dừng tác vụ.");
    } finally {
      setSaving(false);
    }
  };

  const handleComplete = async (task) => {
    if (!window.confirm(`Xác nhận hoàn thành: "${task.name}"?`)) return;
    setSaving(true);
    setError("");
    try {
      await request({
        url: `/api/service-staff-assignments/${task.assignmentId}/complete`,
        method: "PUT",
        data: { completedAt: new Date().toISOString() },
      });
      await fetchData();
      showMsg("✅ Tác vụ hoàn thành! Hệ thống sẽ cập nhật trạng thái lệnh.");
    } catch (err) {
      setError(err?.response?.data?.message || "Không thể hoàn thành tác vụ.");
    } finally {
      setSaving(false);
    }
  };

  const handleViewOrder = (task) => {
    navigate(`/workshop/technician/orders/${task.serviceOrderId}/diagnosis`);
  };

  /* ── filter & stats ── */
  const filtered = useMemo(() => {
    let list = tasks;
    if (filter === "ACTIVE") {
      list = list.filter(t => {
        const s = String(t.status || "").toUpperCase().replace(/-/g, "_");
        return ["PENDING", "IN_PROGRESS", "INPROGRESS", "PAUSED"].includes(s);
      });
    } else if (filter === "COMPLETED") {
      list = list.filter(t => ["COMPLETED", "QC_PENDING"].includes(String(t.status || "").toUpperCase().replace(/-/g, "_")));
    }
    return list;
  }, [tasks, filter]);

  const stats = useMemo(() => {
    const active    = tasks.filter(t => ["PENDING","IN_PROGRESS","INPROGRESS","PAUSED"].includes(String(t.status||"").toUpperCase().replace(/-/g,"_")));
    const doing     = active.filter(t => ["IN_PROGRESS","INPROGRESS"].includes(String(t.status||"").toUpperCase().replace(/-/g,"_")));
    const completed = tasks.filter(t => ["COMPLETED","QC_PENDING"].includes(String(t.status||"").toUpperCase().replace(/-/g,"_")));
    return { total: tasks.length, active: active.length, doing: doing.length, completed: completed.length };
  }, [tasks]);

  /* ── render ── */
  return (
    <div className="mt-page">
      {/* ── header ── */}
      <div className="mt-header">
        <div className="mt-header-left">
          <h1 className="mt-title">🔧 Tác vụ của tôi</h1>
          <p className="mt-subtitle">
            {user?.fullName || "Thợ kỹ thuật"} ·{" "}
            Hôm nay {new Date().toLocaleDateString("vi-VN")} ·{" "}
            <span>Cập nhật {lastRefresh.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}</span>
          </p>
        </div>
        <button
          className="mt-btn-refresh"
          onClick={() => { fetchData(); fetchTimesheet(); }}
          disabled={loading}
        >
          {loading ? "⏳ Đang tải..." : "🔄 Làm mới"}
        </button>
      </div>

      {/* ── alerts ── */}
      {error   && <div className="mt-alert mt-alert-error">⚠️ {error}</div>}
      {success && <div className="mt-alert mt-alert-success">{success}</div>}

      {/* ── timesheet bar ── */}
      <TimesheetBar
        timesheet={timesheet}
        onCheckIn={handleCheckIn}
        onCheckOut={handleCheckOut}
        saving={saving}
      />

      {/* ── stats ── */}
      <div className="mt-stats">
        <QuickStatCard icon="📋" label="Tổng tác vụ"    value={stats.total}     color="#6366f1" />
        <QuickStatCard icon="⏳" label="Cần xử lý"      value={stats.active}    color="#0ea5e9" />
        <QuickStatCard icon="🔧" label="Đang thực hiện" value={stats.doing}     color="#f59e0b" />
        <QuickStatCard icon="✅" label="Hoàn thành"      value={stats.completed} color="#10b981" />
      </div>

      {/* ── filter tabs ── */}
      <div className="mt-filter-tabs">
        {[
          { key: "ACTIVE",    label: `⏳ Cần xử lý (${stats.active})` },
          { key: "ALL",       label: `📋 Tất cả (${stats.total})` },
          { key: "COMPLETED", label: `✅ Đã hoàn thành (${stats.completed})` },
        ].map(tab => (
          <button
            key={tab.key}
            className={`mt-tab ${filter === tab.key ? "mt-tab-active" : ""}`}
            onClick={() => setFilter(tab.key)}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* ── task list ── */}
      {loading && tasks.length === 0 ? (
        <div className="mt-loading">
          <div className="mt-spinner" />
          <span>Đang tải danh sách tác vụ...</span>
        </div>
      ) : filtered.length === 0 ? (
        <EmptyState filter={filter} />
      ) : (
        <div className="mt-task-list">
          {filtered.map(task => (
            <TaskCard
              key={`${task.assignmentId}-${task.status}`}
              task={task}
              onStart={handleStart}
              onPause={handlePause}
              onComplete={handleComplete}
              onViewOrder={handleViewOrder}
            />
          ))}
        </div>
      )}
    </div>
  );
}
