import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { request } from "@app/apiClient";
import { executionApi } from "@features/workshop/api/executionApi";
import { useAuth } from "@shared/core/context/AuthContext";
import "./BayTimeline.css";

/* ─────────── helpers ─────────── */
function fmt(v, opts = {}) {
  if (!v) return "--";
  return new Date(v).toLocaleString("vi-VN", {
    timeZone: "Asia/Ho_Chi_Minh",
    hour: "2-digit", minute: "2-digit",
    ...opts,
  });
}
function fmtDuration(mins) {
  if (!mins || mins <= 0) return "0 phút";
  if (mins < 60) return `${mins} phút`;
  const h = Math.floor(mins / 60), m = mins % 60;
  return m > 0 ? `${h}g${m}p` : `${h} giờ`;
}
function minutesSince(dateStr) {
  if (!dateStr) return 0;
  return Math.floor((Date.now() - new Date(dateStr).getTime()) / 60_000);
}

const BAY_STATUS_META = {
  AVAILABLE:    { label: "Trống",       color: "#10b981", bg: "#d1fae5", border: "#6ee7b7" },
  INUSE:        { label: "Đang dùng",   color: "#3b82f6", bg: "#dbeafe", border: "#93c5fd" },
  IN_USE:       { label: "Đang dùng",   color: "#3b82f6", bg: "#dbeafe", border: "#93c5fd" },
  MAINTENANCE:  { label: "Bảo trì",     color: "#f59e0b", bg: "#fef9c3", border: "#fde68a" },
  OUTOFSERVICE: { label: "Ngừng HĐ",    color: "#ef4444", bg: "#fee2e2", border: "#fca5a5" },
  OUT_OF_SERVICE:{ label: "Ngừng HĐ",   color: "#ef4444", bg: "#fee2e2", border: "#fca5a5" },
};
const defaultMeta = { label: "Không rõ", color: "#94a3b8", bg: "#f1f5f9", border: "#cbd5e1" };

function getBayMeta(status = "") {
  return BAY_STATUS_META[String(status).toUpperCase().replace(/-/g, "")] || defaultMeta;
}

const SERVICE_AREA_ICONS = {
  CHASSIS:   "🏗️",
  ENGINE:    "⚙️",
  ELECTRICAL:"⚡",
  PAINTING:  "🎨",
  GENERAL:   "🔧",
};

/* ─────────── AllocateBayModal ─────────── */
function AllocateBayModal({ bay, serviceOrders, onConfirm, onClose, saving }) {
  const [selectedSoId, setSelectedSoId] = useState("");
  const [plannedDuration, setPlannedDuration] = useState(60);
  const [notes, setNotes] = useState("");
  const [errMsg, setErrMsg] = useState("");

  const handleSubmit = (e) => {
    e.preventDefault();
    if (!selectedSoId) { setErrMsg("Vui lòng chọn lệnh sửa chữa."); return; }
    onConfirm({ bayId: bay.id, serviceOrderId: Number(selectedSoId), plannedDuration: Number(plannedDuration), notes });
  };

  const availableOrders = useMemo(() =>
    serviceOrders.filter(o => {
      const s = String(o.status ?? "").toUpperCase();
      return ["CHECKED_IN","CHECKEDIN","IN_QUEUE","QUOTED_APPROVED","QUOTEDAPPROVED"].includes(s);
    }), [serviceOrders]
  );

  return (
    <div className="bt-modal-backdrop" onClick={onClose}>
      <div className="bt-modal" onClick={e => e.stopPropagation()}>
        <div className="bt-modal-header">
          <h2>🏭 Phân xe vào khoang {bay.code || bay.name}</h2>
          <button className="bt-modal-close" onClick={onClose}>✕</button>
        </div>
        <form className="bt-modal-body" onSubmit={handleSubmit}>
          {errMsg && <div className="bt-alert bt-alert-error">{errMsg}</div>}

          <div className="bt-form-group">
            <label>Lệnh sửa chữa <span className="bt-required">*</span></label>
            <select
              value={selectedSoId}
              onChange={e => setSelectedSoId(e.target.value)}
              className="bt-select"
            >
              <option value="">-- Chọn lệnh sửa chữa --</option>
              {availableOrders.map(o => (
                <option key={o.id} value={o.id}>
                  {o.code || `SO-${o.id}`} · {o.vehicleLicensePlate || "--"} · {o.customerName || ""}
                </option>
              ))}
            </select>
            {availableOrders.length === 0 && (
              <p className="bt-hint">Không có lệnh nào đang chờ phân khoang.</p>
            )}
          </div>

          <div className="bt-form-group">
            <label>Thời gian dự kiến (phút)</label>
            <div className="bt-input-row">
              <input
                type="number"
                min={15} max={600} step={15}
                value={plannedDuration}
                onChange={e => setPlannedDuration(e.target.value)}
                className="bt-input"
              />
              <span className="bt-input-unit">{fmtDuration(Number(plannedDuration))}</span>
            </div>
          </div>

          <div className="bt-form-group">
            <label>Ghi chú</label>
            <textarea
              value={notes}
              onChange={e => setNotes(e.target.value)}
              className="bt-textarea"
              placeholder="Lý do, chú ý đặc biệt..."
              rows={2}
            />
          </div>

          <div className="bt-modal-actions">
            <button type="button" className="bt-btn-ghost" onClick={onClose}>Hủy</button>
            <button type="submit" className="bt-btn-primary" disabled={saving}>
              {saving ? "Đang phân..." : "✅ Xác nhận phân khoang"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

/* ─────────── BayCard ─────────── */
function BayCard({ bay, onAllocate, onRelease }) {
  const meta     = getBayMeta(bay.status);
  const areaIcon = SERVICE_AREA_ICONS[String(bay.serviceArea || "GENERAL").toUpperCase()] || "🔧";
  const occupiedMins = minutesSince(bay.currentAllocatedAt);
  const isOccupied   = ["INUSE", "IN_USE"].includes(String(bay.status || "").toUpperCase().replace(/-/g,""));

  return (
    <article
      className="bt-bay-card"
      style={{ borderTopColor: meta.color, background: isOccupied ? "#f8fbff" : "#ffffff" }}
    >
      {/* header */}
      <div className="bt-bay-head">
        <div className="bt-bay-identity">
          <span className="bt-bay-icon">{areaIcon}</span>
          <div>
            <span className="bt-bay-code">{bay.code || bay.name || `Khoang ${bay.id}`}</span>
            <span className="bt-bay-area">{bay.serviceArea || "General"}</span>
          </div>
        </div>
        <span
          className="bt-bay-status-badge"
          style={{ background: meta.bg, color: meta.color, border: `1px solid ${meta.border}` }}
        >
          {meta.label}
        </span>
      </div>

      {/* occupied info */}
      {isOccupied && bay.currentSoCode && (
        <div className="bt-bay-occupied">
          <div className="bt-occupied-row">
            <span className="bt-occ-label">Lệnh:</span>
            <span className="bt-occ-value bt-occ-code">{bay.currentSoCode}</span>
          </div>
          {bay.currentPlate && (
            <div className="bt-occupied-row">
              <span className="bt-occ-label">Biển số:</span>
              <span className="bt-occ-value">🚘 {bay.currentPlate}</span>
            </div>
          )}
          {bay.currentCustomer && (
            <div className="bt-occupied-row">
              <span className="bt-occ-label">Khách:</span>
              <span className="bt-occ-value">👤 {bay.currentCustomer}</span>
            </div>
          )}
          <div className="bt-occupied-row">
            <span className="bt-occ-label">Vào lúc:</span>
            <span className="bt-occ-value">{fmt(bay.currentAllocatedAt)}</span>
          </div>
          {occupiedMins > 0 && (
            <div className="bt-time-pill" style={{ color: occupiedMins > 240 ? "#dc2626" : "#0369a1" }}>
              ⏱️ {fmtDuration(occupiedMins)} đã sử dụng
            </div>
          )}
        </div>
      )}

      {/* empty state */}
      {!isOccupied && bay.status === "AVAILABLE" && (
        <div className="bt-bay-empty">🟢 Sẵn sàng tiếp nhận</div>
      )}
      {!isOccupied && bay.status !== "AVAILABLE" && (
        <div className="bt-bay-empty" style={{ color: "#92400e" }}>
          {bay.maintenanceNote || "Đang trong tình trạng đặc biệt"}
        </div>
      )}

      {/* actions */}
      <div className="bt-bay-actions">
        {bay.status === "AVAILABLE" && (
          <button className="bt-btn-allocate" onClick={() => onAllocate(bay)}>
            📥 Phân xe vào
          </button>
        )}
        {isOccupied && (
          <button className="bt-btn-release" onClick={() => onRelease(bay)}>
            📤 Giải phóng khoang
          </button>
        )}
      </div>
    </article>
  );
}

/* ─────────── main page ─────────── */
export default function BayTimelinePage() {
  const { user } = useAuth();
  const navigate = useNavigate();

  const [bays, setBays]             = useState([]);
  const [orders, setOrders]         = useState([]);
  const [loading, setLoading]       = useState(false);
  const [error, setError]           = useState("");
  const [saving, setSaving]         = useState(false);
  const [allocatingBay, setAllocatingBay] = useState(null);
  const [filterArea, setFilterArea] = useState("ALL");
  const [filterStatus, setFilterStatus] = useState("ALL");
  const [lastRefresh, setLastRefresh] = useState(new Date());

  /* fetch */
  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [baysRes, ordersRes, allocationsRes] = await Promise.all([
        request({ url: "/api/bays", method: "GET" }),
        request({ url: "/api/service-orders", method: "GET" }),
        request({ url: "/api/bays/timeline", method: "GET", params: { date: new Date().toISOString().slice(0, 10) } })
          .catch(() => []),
      ]);

      const rawBays   = Array.isArray(baysRes)   ? baysRes   : (baysRes?.items   || []);
      const rawOrders = Array.isArray(ordersRes)  ? ordersRes : (ordersRes?.items || []);
      const allocs    = Array.isArray(allocationsRes) ? allocationsRes : [];

      /* Build alloc map: bayId → active allocation */
      const allocMap = new Map();
      allocs.forEach(a => {
        if (!a.releasedAt && !a.ReleasedAt) {
          const bid = a.bayId ?? a.BayId;
          allocMap.set(bid, a);
        }
      });

      /* Enrich bays */
      const enriched = rawBays.map(b => {
        const bid   = b.id ?? b.Id;
        const alloc = allocMap.get(bid);
        const soId  = alloc ? (alloc.serviceOrderId ?? alloc.ServiceOrderId) : (b.currentOccupiedServiceOrderId ?? b.CurrentOccupiedVehicleId);
        const so    = soId ? rawOrders.find(o => (o.id ?? o.Id) === soId) : null;
        return {
          id: bid,
          code: b.code ?? b.Code ?? b.name ?? b.Name ?? `Bay-${bid}`,
          name: b.name ?? b.Name ?? b.code ?? b.Code ?? `Khoang ${bid}`,
          status: b.operationalStatus ?? b.OperationalStatus ?? b.status ?? b.Status ?? "AVAILABLE",
          serviceArea: b.serviceArea ?? b.ServiceArea ?? "GENERAL",
          maintenanceNote: b.maintenanceNote ?? b.MaintenanceNote ?? "",
          currentSoCode:     so ? (so.code ?? so.Code ?? `SO-${soId}`) : null,
          currentPlate:      so ? (so.vehicleLicensePlate ?? so.VehicleLicensePlate) : null,
          currentCustomer:   so ? (so.customerName ?? so.CustomerName) : null,
          currentAllocatedAt: alloc ? (alloc.allocatedAt ?? alloc.AllocatedAt) : null,
          plannedDuration:   alloc ? (alloc.plannedDuration ?? alloc.PlannedDuration) : null,
          allocationId:      alloc ? (alloc.id ?? alloc.Id) : null,
        };
      });

      setBays(enriched);
      setOrders(rawOrders.map(o => ({
        id: o.id ?? o.Id,
        code: o.code ?? o.Code,
        status: o.status ?? o.Status ?? "",
        customerName: o.customerName ?? o.CustomerName ?? "",
        vehicleLicensePlate: o.vehicleLicensePlate ?? o.VehicleLicensePlate ?? "",
      })));
      setLastRefresh(new Date());
    } catch (err) {
      setError("Không thể tải dữ liệu khoang. Kiểm tra kết nối.");
      console.error(err);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);
  useEffect(() => {
    const id = setInterval(fetchData, 30_000);
    return () => clearInterval(id);
  }, [fetchData]);

  /* handle allocate */
  const handleConfirmAllocate = async ({ bayId, serviceOrderId, plannedDuration, notes }) => {
    setSaving(true);
    try {
      await executionApi.allocateBay(bayId, {
        serviceOrderId,
        plannedDuration,
        notes,
        allocatedBy: user?.id ?? user?.staffId ?? null,
      });
      setAllocatingBay(null);
      await fetchData();
    } catch (err) {
      setError(err?.response?.data?.message || err?.message || "Không thể phân khoang.");
    } finally {
      setSaving(false);
    }
  };

  /* handle release */
  const handleRelease = async (bay) => {
    if (!window.confirm(`Xác nhận giải phóng khoang "${bay.code}"?`)) return;
    setSaving(true);
    try {
      await executionApi.releaseBay(bay.id, {
        releasedBy: user?.id ?? user?.staffId ?? null,
      });
      await fetchData();
    } catch (err) {
      setError(err?.response?.data?.message || err?.message || "Không thể giải phóng khoang.");
    } finally {
      setSaving(false);
    }
  };

  /* filter */
  const serviceAreas = useMemo(() => {
    const areas = [...new Set(bays.map(b => b.serviceArea).filter(Boolean))];
    return ["ALL", ...areas];
  }, [bays]);

  const filtered = useMemo(() => {
    let list = bays;
    if (filterArea !== "ALL")   list = list.filter(b => b.serviceArea === filterArea);
    if (filterStatus !== "ALL") {
      list = list.filter(b => {
        const s = String(b.status || "").toUpperCase().replace(/-/g, "");
        if (filterStatus === "AVAILABLE")   return s === "AVAILABLE";
        if (filterStatus === "OCCUPIED")    return ["INUSE"].includes(s);
        if (filterStatus === "MAINTENANCE") return ["MAINTENANCE", "OUTOFSERVICE"].includes(s);
        return true;
      });
    }
    return list;
  }, [bays, filterArea, filterStatus]);

  /* stats */
  const stats = useMemo(() => ({
    total:       bays.length,
    available:   bays.filter(b => String(b.status||"").toUpperCase().replace(/-/g,"") === "AVAILABLE").length,
    occupied:    bays.filter(b => ["INUSE"].includes(String(b.status||"").toUpperCase().replace(/-/g,""))).length,
    maintenance: bays.filter(b => ["MAINTENANCE","OUTOFSERVICE"].includes(String(b.status||"").toUpperCase().replace(/-/g,""))).length,
  }), [bays]);

  const loadFactor = stats.total > 0 ? Math.round((stats.occupied / stats.total) * 100) : 0;

  return (
    <div className="bt-page">
      {/* ── header ── */}
      <div className="bt-header">
        <div className="bt-header-left">
          <h1 className="bt-title">🏭 Timeline Khoang Sửa Chữa</h1>
          <p className="bt-subtitle">
            Quản lý phân bổ khoang theo thời gian thực ·{" "}
            <strong>Hôm nay — {new Date().toLocaleDateString("vi-VN")}</strong>
          </p>
        </div>
        <div className="bt-header-actions">
          <button
            className="bt-btn-icon"
            onClick={fetchData}
            disabled={loading}
            title="Làm mới"
          >
            {loading ? "⏳" : "🔄"} {loading ? "Đang tải..." : `Cập nhật · ${fmt(lastRefresh)}`}
          </button>
          <button className="bt-btn-back" onClick={() => navigate("/workshop/foreman")}>
            ← Bảng Kanban
          </button>
        </div>
      </div>

      {error && <div className="bt-alert bt-alert-error">⚠️ {error}</div>}

      {/* ── summary stats ── */}
      <div className="bt-stats-row">
        <div className="bt-stat" style={{ borderTopColor: "#6366f1" }}>
          <span className="bt-stat-num" style={{ color: "#6366f1" }}>{stats.total}</span>
          <span className="bt-stat-lbl">Tổng khoang</span>
        </div>
        <div className="bt-stat" style={{ borderTopColor: "#10b981" }}>
          <span className="bt-stat-num" style={{ color: "#10b981" }}>{stats.available}</span>
          <span className="bt-stat-lbl">🟢 Đang trống</span>
        </div>
        <div className="bt-stat" style={{ borderTopColor: "#3b82f6" }}>
          <span className="bt-stat-num" style={{ color: "#3b82f6" }}>{stats.occupied}</span>
          <span className="bt-stat-lbl">🔵 Đang dùng</span>
        </div>
        <div className="bt-stat" style={{ borderTopColor: "#f59e0b" }}>
          <span className="bt-stat-num" style={{ color: "#f59e0b" }}>{stats.maintenance}</span>
          <span className="bt-stat-lbl">🟡 Bảo trì</span>
        </div>
        <div className="bt-stat bt-stat-load" style={{ borderTopColor: loadFactor > 90 ? "#ef4444" : loadFactor > 60 ? "#f59e0b" : "#10b981" }}>
          <div className="bt-load-ring" style={{ "--pct": `${loadFactor}%` }}>
            <span className="bt-load-num">{loadFactor}%</span>
          </div>
          <span className="bt-stat-lbl">Tải (Load factor)</span>
        </div>
      </div>

      {/* ── filters ── */}
      <div className="bt-filters">
        <div className="bt-filter-group">
          <span className="bt-filter-label">Khu vực:</span>
          {serviceAreas.map(a => (
            <button
              key={a}
              className={`bt-filter-chip ${filterArea === a ? "bt-chip-active" : ""}`}
              onClick={() => setFilterArea(a)}
            >
              {a === "ALL" ? "Tất cả" : `${SERVICE_AREA_ICONS[a] || "🔧"} ${a}`}
            </button>
          ))}
        </div>
        <div className="bt-filter-group">
          <span className="bt-filter-label">Trạng thái:</span>
          {["ALL", "AVAILABLE", "OCCUPIED", "MAINTENANCE"].map(s => (
            <button
              key={s}
              className={`bt-filter-chip ${filterStatus === s ? "bt-chip-active" : ""}`}
              onClick={() => setFilterStatus(s)}
            >
              {s === "ALL" ? "Tất cả" : s === "AVAILABLE" ? "🟢 Trống" : s === "OCCUPIED" ? "🔵 Có xe" : "🟡 Bảo trì"}
            </button>
          ))}
        </div>
      </div>

      {/* ── bay grid ── */}
      {loading && bays.length === 0 ? (
        <div className="bt-loading">
          <div className="bt-spinner" />
          <span>Đang tải danh sách khoang...</span>
        </div>
      ) : (
        <div className="bt-bay-grid">
          {filtered.length === 0 ? (
            <div className="bt-empty">Không có khoang nào phù hợp với bộ lọc.</div>
          ) : (
            filtered.map(bay => (
              <BayCard
                key={bay.id}
                bay={bay}
                onAllocate={b => setAllocatingBay(b)}
                onRelease={handleRelease}
              />
            ))
          )}
        </div>
      )}

      {/* ── allocate modal ── */}
      {allocatingBay && (
        <AllocateBayModal
          bay={allocatingBay}
          serviceOrders={orders}
          onConfirm={handleConfirmAllocate}
          onClose={() => setAllocatingBay(null)}
          saving={saving}
        />
      )}
    </div>
  );
}
