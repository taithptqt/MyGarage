import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { request } from "@app/apiClient";
import { useAuth } from "@shared/core/context/AuthContext";
import "./ForemanBoard.css";

/* ─────────────────── helpers ─────────────────── */
function fmt(v) {
  if (!v) return "--";
  return new Date(v).toLocaleString("vi-VN", {
    day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit",
    timeZone: "Asia/Ho_Chi_Minh",
  });
}
function fmtMins(mins) {
  if (!mins || mins <= 0) return "0 phút";
  if (mins < 60) return `${mins} phút`;
  return `${Math.floor(mins / 60)}g ${mins % 60}p`;
}
function soStatus(raw = "") {
  return String(raw).toUpperCase();
}
function columnOf(status) {
  const s = soStatus(status);
  if (["CHECKED_IN", "CHECKEDIN", "IN_QUEUE"].includes(s)) return "WAITING";
  if (["IN_PROGRESS", "INPROGRESS", "EXECUTING"].includes(s)) return "INPROGRESS";
  if (["QC_PENDING", "QC_CHECKING", "QCPENDING", "QC"].includes(s)) return "QC";
  if (["READY_FOR_PICKUP", "READYFORPICKUP", "READY"].includes(s)) return "READY";
  return "WAITING";
}

const COLUMNS = [
  { key: "WAITING",    label: "Chờ bắt đầu",  icon: "⏳", color: "#6366f1", bg: "#eef2ff" },
  { key: "INPROGRESS", label: "Đang thi công", icon: "🔧", color: "#0ea5e9", bg: "#e0f2fe" },
  { key: "QC",         label: "Đang QC",       icon: "✅", color: "#f59e0b", bg: "#fffbeb" },
  { key: "READY",      label: "Sẵn sàng giao", icon: "🚗", color: "#10b981", bg: "#d1fae5" },
];

const URGENCY_COLORS = {
  HIGH:   { bg: "#fef2f2", border: "#fecaca", text: "#dc2626", dot: "#ef4444" },
  MEDIUM: { bg: "#fffbeb", border: "#fde68a", text: "#92400e", dot: "#f59e0b" },
  LOW:    { bg: "#f0fdf4", border: "#bbf7d0", text: "#166534", dot: "#22c55e" },
};

/* ─────────────────── sub-components ─────────────────── */
function StatusDot({ status }) {
  const col = columnOf(status);
  const c = COLUMNS.find(c => c.key === col) || COLUMNS[0];
  return (
    <span
      className="fb-status-dot"
      style={{ background: c.color }}
      title={c.label}
    />
  );
}

function SoKanbanCard({ order, onAssign, onViewDetail }) {
  const urgency = useMemo(() => {
    const mins = Number(order.pendingMinutes || 0);
    if (mins > 240) return "HIGH";
    if (mins > 90)  return "MEDIUM";
    return "LOW";
  }, [order.pendingMinutes]);

  const uc = URGENCY_COLORS[urgency];
  const assignees = order.assignees || [];
  const progressPct = useMemo(() => {
    const done  = Number(order.completedTasks || 0);
    const total = Number(order.totalTasks    || 0);
    if (!total) return 0;
    return Math.round((done / total) * 100);
  }, [order]);

  return (
    <article
      className="fb-card"
      style={{ borderLeftColor: uc.dot }}
      onClick={() => onViewDetail(order)}
      tabIndex={0}
      role="button"
      aria-label={`Lệnh sửa chữa ${order.code}`}
      onKeyDown={(e) => e.key === "Enter" && onViewDetail(order)}
    >
      {/* header row */}
      <div className="fb-card-head">
        <span className="fb-card-code">{order.code || `SO-${order.id}`}</span>
        <span className="fb-urgency-badge" style={{ background: uc.bg, color: uc.text, border: `1px solid ${uc.border}` }}>
          <span className="fb-urgency-dot" style={{ background: uc.dot }} />
          {urgency === "HIGH" ? "Gấp" : urgency === "MEDIUM" ? "Bình thường" : "Thấp"}
        </span>
      </div>

      {/* vehicle */}
      <div className="fb-card-vehicle">
        <span className="fb-card-plate">ðŸš˜ {order.vehicleLicensePlate || "--"}</span>
        <span className="fb-card-model">{order.vehicleDescriptor || order.vehicleModelName || "Chưa rõ"}</span>
      </div>

      {/* customer */}
      <div className="fb-card-customer">
        {order.customerName || "Khách hàng"}
      </div>

      {/* bay */}
      {order.bayCode && (
        <div className="fb-card-bay">
          ðŸ­ Khoang: <strong>{order.bayCode}</strong>
        </div>
      )}

      {/* progress bar */}
      {Number(order.totalTasks) > 0 && (
        <div className="fb-card-progress">
          <div className="fb-progress-bar">
            <div
              className="fb-progress-fill"
              style={{
                width: `${progressPct}%`,
                background: progressPct === 100 ? "#10b981" : "#3b82f6",
              }}
            />
          </div>
          <span className="fb-progress-label">
            {order.completedTasks}/{order.totalTasks} hạng mục · {progressPct}%
          </span>
        </div>
      )}

      {/* assignees */}
      {assignees.length > 0 && (
        <div className="fb-card-assignees">
          {assignees.slice(0, 3).map((a, i) => (
            <span key={i} className="fb-avatar" title={a.name}>
              {String(a.name || "?").split(" ").pop().substring(0, 2).toUpperCase()}
            </span>
          ))}
          {assignees.length > 3 && (
            <span className="fb-avatar fb-avatar-more">+{assignees.length - 3}</span>
          )}
        </div>
      )}

      {/* footer */}
      <div className="fb-card-footer">
        <span className="fb-card-time">ðŸ• {order.checkinTime ? fmt(order.checkinTime) : "--"}</span>
        {order.pendingMinutes > 0 && (
          <span className="fb-card-wait" style={{ color: urgency === "HIGH" ? "#dc2626" : "#64748b" }}>
            {fmtMins(order.pendingMinutes)} chờ
          </span>
        )}
      </div>

      {/* action */}
      <div className="fb-card-actions" onClick={(e) => e.stopPropagation()}>
        <button
          className="fb-btn-assign"
          onClick={() => onAssign(order)}
          title="Phân công thợ"
        >
          Phân công
        </button>
      </div>
    </article>
  );
}

function KanbanColumn({ col, orders, onAssign, onViewDetail }) {
  return (
    <div className="fb-column">
      <div className="fb-col-header" style={{ borderBottomColor: col.color }}>
        <div className="fb-col-title-row">
          <span className="fb-col-icon">{col.icon}</span>
          <span className="fb-col-title">{col.label}</span>
        </div>
        <span
          className="fb-col-count"
          style={{ background: col.bg, color: col.color }}
        >
          {orders.length}
        </span>
      </div>

      <div className="fb-col-body">
        {orders.length === 0 ? (
          <div className="fb-col-empty">
            <span>Không có lệnh nào</span>
          </div>
        ) : (
          orders.map((order) => (
            <SoKanbanCard
              key={order.id}
              order={order}
              onAssign={onAssign}
              onViewDetail={onViewDetail}
            />
          ))
        )}
      </div>
    </div>
  );
}

/* ─────────────── summary stats ─────────────── */
function StatCard({ label, value, sub, color, onClick, active }) {
  return (
    <button
      className={`fb-stat-card ${active ? "fb-stat-active" : ""}`}
      style={{ borderTopColor: color }}
      onClick={onClick}
    >
      <span className="fb-stat-value" style={{ color }}>{value}</span>
      <span className="fb-stat-label">{label}</span>
      {sub && <span className="fb-stat-sub">{sub}</span>}
    </button>
  );
}

/* ─────────────── main page ─────────────── */
export default function ForemanBoardPage() {
  const { user } = useAuth();
  const navigate = useNavigate();

  const [orders, setOrders]     = useState([]);
  const [loading, setLoading]   = useState(false);
  const [error, setError]       = useState("");
  const [filter, setFilter]     = useState("ALL");
  const [search, setSearch]     = useState("");
  const [lastRefresh, setLastRefresh] = useState(new Date());

  /* fetch */
  const fetchBoard = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [ordersRes, assignmentsRes, baysRes] = await Promise.all([
        request({ url: "/api/service-orders", method: "GET" }),
        request({ url: "/api/service-staff-assignments", method: "GET" }),
        request({ url: "/api/bays", method: "GET" }),
      ]);

      const raw      = Array.isArray(ordersRes)      ? ordersRes      : (ordersRes?.items  || []);
      const assigns  = Array.isArray(assignmentsRes) ? assignmentsRes : [];
      const bays     = Array.isArray(baysRes)        ? baysRes        : [];

      const bayMap = new Map(bays.map((b) => [b.id ?? b.Id, b]));

      /* Only show active orders */
      const active = raw.filter(o => {
        const s = soStatus(o.status ?? o.Status ?? "");
        return !["COMPLETED", "CANCELED", "CANCELLED"].includes(s);
      });

      /* Enrich with assignees + bay + task counts */
      const enriched = active.map(o => {
          const oid = o.id ?? o.Id;
        const myAssigns = assigns.filter(a =>
            (a.serviceOrderId ?? a.ServiceOrderId) === oid ||
          // fallback: match via detail
          false
        );
          const bayId = o.bayId ?? o.BayId ?? o.currentBayId ?? o.CurrentBayId;
        const bay   = bayId ? bayMap.get(bayId) : null;
          const totalTasks = Number(o.totalTasks ?? o.TotalTasks ?? 0);
          const completedTasks = Number(o.completedTasks ?? o.CompletedTasks ?? 0);
        return {
          id: oid,
            code: o.code ?? o.Code ?? `SO-${oid}`,
            status: o.status ?? o.Status ?? "",
            customerName: o.customerName ?? o.CustomerName ?? "",
            vehicleLicensePlate: o.vehicleLicensePlate ?? o.VehicleLicensePlate ?? "--",
            vehicleDescriptor: o.vehicleDescriptor ?? o.VehicleDescriptor ?? "",
            vehicleModelName: o.vehicleModelName ?? o.VehicleModelName ?? "",
            checkinTime: o.checkInTime ?? o.CheckInTime ?? o.checkinTime ?? o.CheckinTime,
            pendingMinutes: Number(o.pendingMinutes ?? o.PendingMinutes ?? 0),
            bayCode: bay ? (bay.code ?? bay.Code ?? bay.name ?? bay.Name ?? `Bay-${bayId}`) : (o.bayCode ?? o.BayCode ?? ""),
          assignees: myAssigns.map(a => ({
                id: a.technicianId ?? a.TechnicianId,
                name: a.technicianName ?? a.TechnicianName ?? "Thợ",
                role: a.roleInTask ?? a.RoleInTask ?? "Primary",
          })),
          totalTasks,
          completedTasks,
        };
      });

      setOrders(enriched);
      setLastRefresh(new Date());
    } catch (err) {
      setError("Không thể t?i dữ liệu. Kiểm tra kết nối máy chủ.");
      console.error(err);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchBoard(); }, [fetchBoard]);

  /* auto refresh every 45 sec */
  useEffect(() => {
    const id = setInterval(fetchBoard, 45_000);
    return () => clearInterval(id);
  }, [fetchBoard]);

  /* filter / search */
  const filtered = useMemo(() => {
    let list = orders;
    if (filter !== "ALL") {
      list = list.filter(o => columnOf(o.status) === filter);
    }
    const q = search.trim().toLowerCase();
    if (q) {
      list = list.filter(o =>
        (o.code + o.customerName + o.vehicleLicensePlate + o.vehicleDescriptor)
          .toLowerCase()
          .includes(q)
      );
    }
    return list;
  }, [orders, filter, search]);

  /* group by column */
  const byColumn = useMemo(() => {
    const map = {};
    COLUMNS.forEach(c => { map[c.key] = []; });
    filtered.forEach(o => {
      const col = columnOf(o.status);
      map[col].push(o);
    });
    return map;
  }, [filtered]);

  /* stat totals */
  const stats = useMemo(() => ({
    total:      orders.length,
    inprogress: orders.filter(o => columnOf(o.status) === "INPROGRESS").length,
    qc:         orders.filter(o => columnOf(o.status) === "QC").length,
    ready:      orders.filter(o => columnOf(o.status) === "READY").length,
    urgent:     orders.filter(o => Number(o.pendingMinutes) > 240).length,
  }), [orders]);

  /* actions */
  const handleAssign = useCallback((order) => {
    navigate(`/workshop/foreman/orders/${order.id}/assign`);
  }, [navigate]);

  const handleViewDetail = useCallback((order) => {
    navigate(`/workshop/foreman/orders/${order.id}/quote`);
  }, [navigate]);

  /* ── render ── */
  return (
    <div className="fb-page">
      {/* ── page header ── */}
      <div className="fb-header">
        <div className="fb-header-left">
          <h1 className="fb-title">
            🛡️ Bảng điều phối xưởng
          </h1>
          <p className="fb-subtitle">
            Xưởng đang hoạt động · Cập nhật lúc{" "}
            <strong>{lastRefresh.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}</strong>
          </p>
        </div>
        <div className="fb-header-actions">
          <button
            className="fb-btn-refresh"
            onClick={fetchBoard}
            disabled={loading}
            title="Làm mới"
          >
            {loading ? "⏳" : "🔄"} {loading ? "Đang tải..." : "Làm mới"}
          </button>
          <button
            className="fb-btn-bay"
            onClick={() => navigate("/workshop/foreman/bay-timeline")}
            title="Xem timeline khoang"
          >
            ðŸ­ Timeline khoang
          </button>
        </div>
      </div>

      {/* ── error ── */}
      {error && (
        <div className="fb-alert fb-alert-error">⚠️ {error}</div>
      )}

      {/* ── stat cards ── */}
      <div className="fb-stats">
        <StatCard label="Tất cả" value={stats.total}      color="#6366f1" onClick={() => setFilter("ALL")} active={filter === "ALL"} />
        <StatCard label="Đang làm" value={stats.inprogress} color="#0ea5e9" onClick={() => setFilter("INPROGRESS")} active={filter === "INPROGRESS"} sub="lệnh đang thi công" />
        <StatCard label="Đang QC"  value={stats.qc}         color="#f59e0b" onClick={() => setFilter("QC")} active={filter === "QC"} />
        <StatCard label="Sẵn giao" value={stats.ready}      color="#10b981" onClick={() => setFilter("READY")} active={filter === "READY"} />
        <StatCard label="Quá hạn" value={stats.urgent}  color="#ef4444" onClick={() => setFilter("ALL")} active={false} sub=">4 giờ chờ" />
      </div>

      {/* ── search bar ── */}
      <div className="fb-search-bar">
        <span className="fb-search-icon">ðŸ”</span>
        <input
          className="fb-search-input"
          placeholder="Tìm theo mã SO, biển số, khách hàng..."
          value={search}
          onChange={e => setSearch(e.target.value)}
        />
        {search && (
          <button className="fb-search-clear" onClick={() => setSearch("")}>✕</button>
        )}
        <span className="fb-search-count">
          {filtered.length} lệnh
        </span>
      </div>

      {/* ── kanban board ── */}
      <div className="fb-board">
        {COLUMNS.map(col => (
          <KanbanColumn
            key={col.key}
            col={col}
            orders={byColumn[col.key] || []}
            onAssign={handleAssign}
            onViewDetail={handleViewDetail}
          />
        ))}
      </div>
    </div>
  );
}


