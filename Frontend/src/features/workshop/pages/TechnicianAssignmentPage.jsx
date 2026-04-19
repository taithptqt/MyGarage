import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { request } from "@app/apiClient";
import { useAuth } from "@shared/core/context/AuthContext";
import "./TechnicianAssignment.css";

/* ─── helpers ─── */
function fmt(v) {
  if (!v) return "--";
  return new Date(v).toLocaleString("vi-VN", {
    day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit",
    timeZone: "Asia/Ho_Chi_Minh",
  });
}

const SPECIALTY_ICONS = {
  ENGINE:    "⚙️",  ELECTRICAL: "⚡", CHASSIS:   "🏗️",
  PAINTING:  "ðŸŽ¨",  GENERAL:    "ðŸ”§", BODYWORK:  "ðŸš™",
};
const SPECIALTY_VI = {
  ENGINE:    "Động cơ", ELECTRICAL: "Điện", CHASSIS:  "Gầm",
  PAINTING:  "Son",     GENERAL:    "Tổng h?p", BODYWORK: "Thân vỏ",
};

/* ─── sub-components ─── */
function StaffSuggestionCard({ staff, selected, onToggle, splitRatio, onRatioChange, isFirst }) {
  const specialty = String(staff.specialty || staff.Specialty || "GENERAL").toUpperCase();
  const icon = SPECIALTY_ICONS[specialty] || "ðŸ”§";
  const workload = Number(staff.currentWorkload ?? staff.CurrentWorkload ?? 0);

  return (
    <div
      className={`ta-staff-card ${selected ? "ta-staff-selected" : ""}`}
      onClick={onToggle}
      role="checkbox"
      aria-checked={selected}
      tabIndex={0}
      onKeyDown={e => e.key === " " && onToggle()}
    >
      <div className="ta-staff-select-indicator">
        <span className={`ta-checkbox ${selected ? "ta-cb-checked" : ""}`}>
          {selected ? "✓" : ""}
        </span>
      </div>
      <div className="ta-staff-avatar">
          {String(staff.fullName ?? staff.FullName ?? "?").split(" ").pop().substring(0, 2).toUpperCase()}
      </div>
      <div className="ta-staff-info">
          <span className="ta-staff-name">{staff.fullName ?? staff.FullName ?? "Thợ"}</span>
        <span className="ta-staff-specialty">
          {icon} {SPECIALTY_VI[specialty] || specialty}
        </span>
        <div className="ta-staff-workload">
          <div className="ta-wl-bar">
            <div
              className="ta-wl-fill"
              style={{
                width: `${Math.min(workload, 100)}%`,
                background: workload > 80 ? "#ef4444" : workload > 50 ? "#f59e0b" : "#10b981",
              }}
            />
          </div>
          <span className="ta-wl-label">Tải: {workload}%</span>
        </div>
      </div>
      {selected && (
        <div className="ta-ratio-input" onClick={e => e.stopPropagation()}>
          <label>
            {isFirst ? "Thợ chính" : "Thợ phụ"}
          </label>
          <div className="ta-ratio-row">
            <input
              type="number"
              min={10} max={100} step={5}
              value={splitRatio}
              onChange={e => onRatioChange(Number(e.target.value))}
              className="ta-ratio-num"
            />
            <span>%</span>
          </div>
        </div>
      )}
    </div>
  );
}

function ServiceItemRow({ item, assignments, availableStaff, onAssignChange }) {
  const assigned = assignments[item.id] || [];

  return (
    <div className={`ta-item-row ${assigned.length > 0 ? "ta-item-assigned" : ""}`}>
      <div className="ta-item-info">
        <span className="ta-item-name">{item.name || item.serviceName || `Hạng mục #${item.id}`}</span>
        {item.flatRateHours && (
          <span className="ta-item-hrs">⏱ {item.flatRateHours}h (định mức)</span>
        )}
        {item.specialty && (
          <span className="ta-item-spec">
            {SPECIALTY_ICONS[item.specialty?.toUpperCase()] || "🔧"} Cần: {SPECIALTY_VI[item.specialty?.toUpperCase()] || item.specialty}
          </span>
        )}
      </div>
      <div className="ta-item-assign">
        {assigned.length === 0 ? (
          <span className="ta-item-unassigned">Chưa phân công</span>
        ) : (
          assigned.map((a, i) => (
            <span key={i} className={`ta-item-assignee ${a.role === "PRIMARY" ? "ta-assignee-primary" : "ta-assignee-assist"}`}>
              {a.role === "PRIMARY" ? "ðŸŽ¯" : "ðŸ¤"} {a.staffName}
              {a.splitRatio ? ` (${a.splitRatio}%)` : ""}
            </span>
          ))
        )}
      </div>
    </div>
  );
}

/* ─── main ─── */
export default function TechnicianAssignmentPage() {
  const { orderId } = useParams();
  const { user }    = useAuth();
  const navigate    = useNavigate();

  const [order, setOrder]         = useState(null);
  const [items, setItems]         = useState([]);
  const [staff, setStaff]         = useState([]);
  const [loading, setLoading]     = useState(true);
  const [saving, setSaving]       = useState(false);
  const [error, setError]         = useState("");
  const [success, setSuccess]     = useState("");

  /* assignments[itemId] = [{ staffId, staffName, role, splitRatio }] */
  const [assignments, setAssignments] = useState({});
  /* selectedItemId: which item is being edited */
  const [activeItemId, setActiveItemId] = useState(null);
  /* selectedStaff for active item */
  const [pickedStaff, setPickedStaff]   = useState(new Map()); // staffId → { splitRatio }

  /* ── fetch ── */
  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [orderRes, staffRes] = await Promise.all([
        request({ url: `/api/service-orders/${orderId}`, method: "GET" }),
        request({ url: "/api/staff/available", method: "GET",
          params: { branchId: user?.branchId ?? user?.BranchId ?? "", date: new Date().toISOString().slice(0, 10) } })
          .catch(() => request({ url: "/api/staff", method: "GET" })),
      ]);

      const raw    = orderRes?.data ?? orderRes;
      const rawItems = raw?.serviceOrderDetails ?? raw?.ServiceOrderDetails ?? raw?.items ?? [];
      const rawStaff = Array.isArray(staffRes) ? staffRes : (staffRes?.items ?? []);

      setOrder(raw);
      setItems(rawItems.map(it => ({
        id: it.id ?? it.Id,
        name: it.serviceName ?? it.ServiceName ?? it.name ?? `Hạng mục #${it.id ?? it.Id}`,
        specialty: it.specialty ?? it.Specialty ?? "",
        flatRateHours: it.flatRateHours ?? it.FlatRateHours ?? null,
      })));
      setStaff(rawStaff.map(s => ({
        id: s.id ?? s.Id ?? s.staffId ?? s.StaffId,
        fullName: s.fullName ?? s.FullName ?? s.name ?? s.Name ?? "Thợ",
        specialty: s.specialty ?? s.Specialty ?? "GENERAL",
        currentWorkload: Number(s.currentWorkload ?? s.CurrentWorkload ?? 0),
      })));

      // Load existing assignments
      const assignRes = await request({ url: "/api/service-staff-assignments", method: "GET",
        params: { serviceOrderId: orderId } })
        .catch(() => []);
      const existing = Array.isArray(assignRes) ? assignRes : (assignRes?.items ?? []);
      const amap = {};
      existing.forEach(a => {
        const iid = a.serviceOrderDetailId ?? a.ServiceOrderDetailId;
        if (!iid) return;
        if (!amap[iid]) amap[iid] = [];
        amap[iid].push({
          staffId: a.staffId ?? a.StaffId,
          staffName: a.staffName ?? a.StaffName ?? a.staffFullName ?? "Thợ",
          role: a.roleInTask ?? a.RoleInTask ?? "PRIMARY",
          splitRatio: a.splitRatio ?? a.SplitRatio ?? 100,
        });
      });
      setAssignments(amap);
    } catch (err) {
      setError("Không thể t?i dữ liệu. Kiểm tra kết nối máy chủ.");
      console.error(err);
    } finally {
      setLoading(false);
    }
  }, [orderId, user]);

  useEffect(() => { fetchData(); }, [fetchData]);

  /* ── when active item changes, pre-load pickedStaff ── */
  useEffect(() => {
    if (!activeItemId) { setPickedStaff(new Map()); return; }
    const cur = assignments[activeItemId] || [];
    const m = new Map(cur.map(a => [a.staffId, { splitRatio: a.splitRatio }]));
    setPickedStaff(m);
  }, [activeItemId, assignments]);

  /* ── toggle staff selection ── */
  const toggleStaff = (staffId) => {
    setPickedStaff(prev => {
      const next = new Map(prev);
      if (next.has(staffId)) { next.delete(staffId); }
      else { next.set(staffId, { splitRatio: next.size === 0 ? 70 : 30 }); }
      return next;
    });
  };

  const updateRatio = (staffId, ratio) => {
    setPickedStaff(prev => {
      const next = new Map(prev);
      if (next.has(staffId)) next.set(staffId, { splitRatio: ratio });
      return next;
    });
  };

  /* ── validate total ratio ── */
  const totalRatio = useMemo(() => {
    let sum = 0;
    pickedStaff.forEach(v => { sum += Number(v.splitRatio || 0); });
    return sum;
  }, [pickedStaff]);
  const ratioOk = pickedStaff.size === 0 || (totalRatio === 100);

  /* ── apply picks to assignments ── */
  const applyPicks = () => {
    if (!activeItemId) return;
    const entries = [];
    let i = 0;
    pickedStaff.forEach((v, staffId) => {
      const s = staff.find(s => s.id === staffId);
      entries.push({
        staffId,
        staffName: s?.fullName ?? "Thợ",
        role: i === 0 ? "PRIMARY" : "ASSISTANT",
        splitRatio: v.splitRatio,
      });
      i++;
    });
    setAssignments(prev => ({ ...prev, [activeItemId]: entries }));
    setActiveItemId(null);
  };

  /* ── bulk submit ── */
  const handleSubmit = async () => {
    const payload = [];
    items.forEach(it => {
      const aList = assignments[it.id] || [];
      aList.forEach(a => {
        payload.push({
          serviceOrderId:       Number(orderId),
          serviceOrderDetailId: it.id,
          staffId:              a.staffId,
          roleInTask:           a.role,
          splitRatio:           a.splitRatio,
          assignedBy:           user?.id ?? user?.staffId,
        });
      });
    });
    if (payload.length === 0) {
      setError("Vui lòng phân công ít nhất 1 thợ cho 1 hạng mục.");
      return;
    }
    setSaving(true);
    setError("");
    setSuccess("");
    try {
      await request({
        url: "/api/service-staff-assignments/bulk",
        method: "POST",
        data: { assignments: payload },
      });
      setSuccess(`Đã phân công ${payload.length} tác vụ thành công!`);
      setTimeout(() => navigate(`/workshop/foreman/board`), 1500);
    } catch (err) {
      setError(err?.response?.data?.message || err?.message || "Không thể luu phân công.");
    } finally {
      setSaving(false);
    }
  };

  const unassignedCount = items.filter(it => !assignments[it.id]?.length).length;

  /* ── render ── */
  if (loading) return (
    <div className="ta-page">
      <div className="ta-loading"><div className="ta-spinner" /><span>Đang tải...</span></div>
    </div>
  );

  return (
    <div className="ta-page">
      {/* header */}
      <div className="ta-header">
        <div>
          <h1 className="ta-title">Phân công kỹ thuật viên</h1>
          <p className="ta-subtitle">
            Lệnh: <strong>{order?.code || `SO-${orderId}`}</strong> ·{" "}
            Xe: <strong>{order?.vehicleLicensePlate || "--"}</strong> ·{" "}
            KH: {order?.customerName || "--"}
          </p>
        </div>
        <div className="ta-header-actions">
          <button className="ta-btn-back" onClick={() => navigate(-1)}>← Quay lại</button>
          <button
            className="ta-btn-submit"
            onClick={handleSubmit}
            disabled={saving}
          >
            {saving ? "? Đang luu..." : `? Xác nhận phân công (${Object.values(assignments).filter(a => a.length > 0).length}/${items.length} hạng mục)`}
          </button>
        </div>
      </div>

      {error   && <div className="ta-alert ta-alert-error">⚠️ {error}</div>}
      {success && <div className="ta-alert ta-alert-success">{success}</div>}

      {unassignedCount > 0 && (
        <div className="ta-warn-banner">
          Còn <strong>{unassignedCount}</strong> hạng mục chưa được phân công
        </div>
      )}

      <div className="ta-body">
        {/* LEFT: service items */}
        <div className="ta-items-panel">
          <div className="ta-panel-head">
            <h2>📋 Hạng mục sửa chữa ({items.length})</h2>
            <span className="ta-panel-hint">Chọn hạng mục d? phân công thợ</span>
          </div>
          <div className="ta-items-list">
            {items.length === 0 && (
              <div className="ta-empty-msg">Không có hạng mục nào trong lệnh này.</div>
            )}
            {items.map(item => (
              <div
                key={item.id}
                className={`ta-item-block ${activeItemId === item.id ? "ta-item-editing" : ""}`}
                onClick={() => setActiveItemId(activeItemId === item.id ? null : item.id)}
              >
                <ServiceItemRow
                  item={item}
                  assignments={assignments}
                  availableStaff={staff}
                  onAssignChange={() => {}}
                />
                {activeItemId === item.id && (
                  <div className="ta-edit-hint">
                    Chọn thợ từ danh sách bên phải, rồi nhấn "Xác nhận hạng mục"
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>

        {/* RIGHT: staff panel */}
        <div className="ta-staff-panel">
          <div className="ta-panel-head">
            <h2>Danh sách thợ ({staff.length})</h2>
            {activeItemId ? (
              <div className="ta-staff-ctx">
                Đang phân cho:{" "}
                <strong>
                  {items.find(i => i.id === activeItemId)?.name || "Hạng mục"}
                </strong>
              </div>
            ) : (
              <span className="ta-panel-hint">Chọn hạng mục trước</span>
            )}
          </div>

          {activeItemId && (
            <>
              <div className="ta-staff-list">
                {staff.map((s, idx) => (
                  <StaffSuggestionCard
                    key={s.id}
                    staff={s}
                    selected={pickedStaff.has(s.id)}
                    onToggle={() => toggleStaff(s.id)}
                    splitRatio={pickedStaff.get(s.id)?.splitRatio ?? 0}
                    onRatioChange={v => updateRatio(s.id, v)}
                    isFirst={[...pickedStaff.keys()][0] === s.id}
                  />
                ))}
                {staff.length === 0 && (
                  <div className="ta-empty-msg">Không có thợ nào sẵn có.</div>
                )}
              </div>

              {/* ratio validation */}
              {pickedStaff.size > 0 && (
                <div className={`ta-ratio-check ${ratioOk ? "ta-ratio-ok" : "ta-ratio-err"}`}>
                  {ratioOk
                    ? `✅ Tổng tỉ lệ = ${totalRatio}% (hợp lệ)`
                    : `⚠️ Tổng tỉ lệ = ${totalRatio}% (phải bằng 100%)`
                  }
                </div>
              )}

              <div className="ta-staff-footer">
                <button className="ta-btn-cancel-item" onClick={() => setActiveItemId(null)}>
                  Hủy
                </button>
                <button
                  className="ta-btn-apply"
                  onClick={applyPicks}
                  disabled={pickedStaff.size === 0 || !ratioOk}
                >
                  ? Xác nhận hạng mục ({pickedStaff.size} thợ)
                </button>
              </div>
            </>
          )}

          {!activeItemId && (
            <div className="ta-staff-placeholder">
              <span>ðŸ‘†</span>
              <p>Chọn m?t hạng mục d? b?t d?u phân công thợ</p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}


