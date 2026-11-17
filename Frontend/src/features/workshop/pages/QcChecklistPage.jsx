import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { request } from "@app/apiClient";
import { executionApi } from "@features/workshop/api/executionApi";
import { useAuth } from "@shared/core/context/AuthContext";
import "./QcChecklist.css";

/* ── constants ── */
const RESULT_OPTIONS = [
  { value: "PASS",   label: "ĐẠT",    color: "#10b981", bg: "#d1fae5" },
  { value: "FAIL",   label: "KHÔNG",  color: "#ef4444", bg: "#fee2e2" },
  { value: "NA",     label: "N/A",    color: "#94a3b8", bg: "#f1f5f9" },
];

const GROUP_ICONS = {
  SAFETY:    { icon: "🛡️", label: "An toàn" },
  OPERATION: { icon: "⚙️", label: "Vận hành" },
  COSMETIC:  { icon: "✨", label: "Mỹ thuật" },
  ELECTRIC:  { icon: "⚡", label: "Điện" },
  GENERAL:   { icon: "??", label: "Tổng quát" },
};

/* ── sub-components ── */
function ResultToggle({ value, onChange, disabled }) {
  return (
    <div className="qc-toggle-group">
      {RESULT_OPTIONS.map(opt => (
        <button
          key={opt.value}
          type="button"
          className={`qc-toggle-btn ${value === opt.value ? "qc-toggle-active" : ""}`}
          style={value === opt.value ? { background: opt.bg, color: opt.color, borderColor: opt.color } : {}}
          onClick={() => onChange(opt.value === value ? null : opt.value)}
          disabled={disabled}
        >
          {opt.label}
        </button>
      ))}
    </div>
  );
}

function ChecklistItem({ item, result, note, onResultChange, onNoteChange, disabled }) {
  const [showNote, setShowNote] = useState(false);
  const passed = result === "PASS";
  const failed = result === "FAIL";

  return (
    <div className={`qc-item ${passed ? "qc-item-pass" : failed ? "qc-item-fail" : ""}`}>
      <div className="qc-item-main">
        <div className="qc-item-info">
          <span className="qc-item-name">
            {item.required && <span className="qc-required" title="Bắt buộc">*</span>}
            {item.name}
          </span>
          {item.description && (
            <span className="qc-item-desc">{item.description}</span>
          )}
        </div>
        <div className="qc-item-controls">
          <ResultToggle value={result} onChange={onResultChange} disabled={disabled} />
          <button
            type="button"
            className="qc-note-toggle"
            onClick={() => setShowNote(s => !s)}
            title="Ghi chú"
          >
            💬 {note ? "Có ghi chú" : "Ghi chú"}
          </button>
        </div>
      </div>
      {showNote && (
        <textarea
          className="qc-note-input"
          placeholder="Ghi chú về hạng mục này..."
          value={note || ""}
          onChange={e => onNoteChange(e.target.value)}
          disabled={disabled}
          rows={2}
        />
      )}
    </div>
  );
}

function ChecklistGroup({ group, items, results, notes, onResultChange, onNoteChange, disabled }) {
  const meta = GROUP_ICONS[String(group).toUpperCase()] || GROUP_ICONS.GENERAL;
  const total   = items.length;
  const passed  = items.filter(it => results[it.id] === "PASS").length;
  const failed  = items.filter(it => results[it.id] === "FAIL").length;
  const progPct = total > 0 ? Math.round((passed / total) * 100) : 0;
  const [collapsed, setCollapsed] = useState(false);

  return (
    <div className="qc-group">
      <button
        className="qc-group-header"
        onClick={() => setCollapsed(c => !c)}
        type="button"
      >
        <div className="qc-group-left">
          <span className="qc-group-icon">{meta.icon}</span>
          <span className="qc-group-label">{meta.label || group}</span>
          <span className="qc-group-count">{total} hạng mục</span>
        </div>
        <div className="qc-group-stats">
          {passed > 0 && <span className="qc-stat-pass">✅ {passed} đạt</span>}
          {failed > 0 && <span className="qc-stat-fail">❌ {failed} lỗi</span>}
          <div className="qc-group-bar">
            <div className="qc-group-bar-fill" style={{ width: `${progPct}%`, background: progPct === 100 ? "#10b981" : "#3b82f6" }} />
          </div>
          <span className="qc-chevron">{collapsed ? "▶" : "▼"}</span>
        </div>
      </button>
      {!collapsed && (
        <div className="qc-group-body">
          {items.map(item => (
            <ChecklistItem
              key={item.id}
              item={item}
              result={results[item.id] || null}
              note={notes[item.id] || ""}
              onResultChange={v => onResultChange(item.id, v)}
              onNoteChange={v => onNoteChange(item.id, v)}
              disabled={disabled}
            />
          ))}
        </div>
      )}
    </div>
  );
}

/* ── FailModal ── */
function FailModal({ onConfirm, onClose, staff }) {
  const [selectedTechId, setSelectedTechId] = useState("");
  const [reason, setReason] = useState("");

  return (
    <div className="qc-modal-backdrop" onClick={onClose}>
      <div className="qc-modal" onClick={e => e.stopPropagation()}>
        <div className="qc-modal-header">
          <h2>Xác nhận KHÔNG ĐẠT</h2>
          <button className="qc-modal-close" onClick={onClose}>✕</button>
        </div>
        <div className="qc-modal-body">
          <p className="qc-modal-desc">
            Vui lòng chọn thợ cần làm lại và mô tả lỗi cần sửa:
          </p>
          <div className="qc-form-group">
            <label>Thợ cần làm lại</label>
            <select value={selectedTechId} onChange={e => setSelectedTechId(e.target.value)} className="qc-select">
              <option value="">-- Chọn thợ --</option>
              {staff.map(s => (
                <option key={s.id} value={s.id}>{s.fullName}</option>
              ))}
            </select>
          </div>
          <div className="qc-form-group">
            <label>Mô tả lỗi cần sửa</label>
            <textarea
              className="qc-textarea"
              rows={3}
              value={reason}
              onChange={e => setReason(e.target.value)}
              placeholder="Mô tả các hạng mục cần làm lại..."
            />
          </div>
          <div className="qc-modal-actions">
            <button className="qc-btn-ghost" onClick={onClose}>Hủy</button>
            <button
              className="qc-btn-fail"
              onClick={() => onConfirm({ technicianId: selectedTechId || null, reason })}
            >
              ❌ Xác nhận không đạt
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

/* ── main page ── */
export default function QcChecklistPage() {
  const { orderId } = useParams();
  const { user }    = useAuth();
  const navigate    = useNavigate();

  const [order, setOrder]           = useState(null);
  const [inspection, setInspection] = useState(null);
  const [templateItems, setTemplateItems] = useState([]);
  const [staff, setStaff]           = useState([]);
  const [loading, setLoading]       = useState(true);
  const [saving, setSaving]         = useState(false);
  const [error, setError]           = useState("");
  const [success, setSuccess]       = useState("");
  const [showFailModal, setShowFailModal] = useState(false);

  /* results[itemId] = "PASS" | "FAIL" | "NA" | null */
  const [results, setResults] = useState({});
  const [notes, setNotes]     = useState({});

  /* ── fetch ── */
  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [orderRes, templatesRes, staffRes] = await Promise.all([
        request({ url: `/api/v1/service-orders/${orderId}`, method: "GET" }),
        executionApi.getQcChecklistTemplates({ serviceOrderId: orderId }),
        request({ url: "/api/v1/staff", method: "GET" }).catch(() => []),
      ]);

      const raw = orderRes?.data ?? orderRes;
      setOrder(raw);

      const tpls = Array.isArray(templatesRes) ? templatesRes : (templatesRes?.items ?? []);
      // Flatten all items from all templates
      const allItems = tpls.flatMap((t) =>
        (t.items ?? t.Items ?? []).map((it) => ({
          id: it.id ?? it.Id,
          name: it.name ?? it.Name ?? it.itemName ?? "Hạng mục",
          description: it.description ?? it.Description ?? "",
          required: it.isRequired ?? it.IsRequired ?? false,
          group: it.category ?? it.Category ?? "GENERAL",
          sortOrder: it.sortOrder ?? it.SortOrder ?? 0,
        }))
      );
      const sorted = [...allItems].sort((a, b) => a.sortOrder - b.sortOrder);
      setTemplateItems(sorted);

      const rawStaff = Array.isArray(staffRes) ? staffRes : (staffRes?.items ?? []);
      setStaff(rawStaff.map((s) => ({
        id: s.id ?? s.Id ?? s.staffId ?? s.StaffId,
        fullName: s.fullName ?? s.FullName ?? "Thợ",
      })));

      // Load existing inspection if any
      const inspRes = await request({
        url: "/api/v1/qc-inspections",
        method: "GET",
        params: { serviceOrderId: orderId },
      }).catch(() => null);
      if (inspRes) {
        const existing = Array.isArray(inspRes) ? inspRes[0] : inspRes;
        if (existing?.id || existing?.Id) {
          setInspection(existing);
          // pre-fill results
          const r = {};
          const n = {};
          (existing.items ?? existing.Items ?? []).forEach((it) => {
            const iid = it.checklistItemId ?? it.ChecklistItemId ?? it.id ?? it.Id;
            r[iid] = it.result ?? it.Result ?? null;
            n[iid] = it.note ?? it.Note ?? "";
          });
          setResults(r);
          setNotes(n);
        }
      }
    } catch (err) {
      setError("Không thể tải dữ liệu QC. Kiểm tra kết nối.");
      console.error(err);
    } finally {
      setLoading(false);
    }
  }, [orderId]);

  useEffect(() => { fetchData(); }, [fetchData]);

  /* ── stats ── */
  const stats = useMemo(() => {
    const total    = templateItems.length;
    const answered = templateItems.filter(it => results[it.id]).length;
    const passed   = templateItems.filter(it => results[it.id] === "PASS").length;
    const failed   = templateItems.filter(it => results[it.id] === "FAIL").length;
    const requiredAll   = templateItems.filter(it => it.required);
    const requiredPassed = requiredAll.filter(it => results[it.id] === "PASS").length;
    const allRequiredOk = requiredAll.every(it => results[it.id] === "PASS");
    const canPass   = allRequiredOk && failed === 0;
    const progPct   = total > 0 ? Math.round((answered / total) * 100) : 0;
    return { total, answered, passed, failed, canPass, progPct, requiredAll: requiredAll.length, requiredPassed };
  }, [templateItems, results]);

  /* ── group items ── */
  const grouped = useMemo(() => {
    const m = new Map();
    templateItems.forEach(it => {
      const g = it.group || "GENERAL";
      if (!m.has(g)) m.set(g, []);
      m.get(g).push(it);
    });
    return m;
  }, [templateItems]);

  /* ── save draft ── */
  const saveDraft = async () => {
    setSaving(true);
    setError("");
    setSuccess("");
    try {
      const inspectionId = inspection?.id ?? inspection?.Id;
      if (!inspectionId) {
        setError("Chưa có phiên QC. Vui lòng tạo QC Inspection trước.");
        return;
      }
      const payload = templateItems
        .filter(it => results[it.id])
        .map(it => ({
          checklistItemId: it.id,
          result: results[it.id],
          note:   notes[it.id] || null,
        }));
      await executionApi.upsertQcInspectionItems(inspectionId, { items: payload });
      setSuccess("💾 Đã lưu tiến trình QC.");
    } catch (err) {
      setError(err?.response?.data?.message || "Lưu thất bại.");
    } finally {
      setSaving(false);
    }
  };

  /* ── pass inspection ── */
  const handlePass = async () => {
    if (!window.confirm("Xác nhận xe ĐẠT kiểm tra QC?")) return;
    setSaving(true);
    setError("");
    try {
      await saveDraft();
      const inspId = inspection?.id ?? inspection?.Id;
      if (inspId) {
        await request({ url: `/api/v1/qc-inspections/${inspId}/passes`, method: "POST",
          data: { inspectorId: user?.id ?? user?.staffId } });
      }
      setSuccess("✅ QC ĐẠT — Xe sẵn sàng bàn giao!");
      setTimeout(() => navigate(`/workshop/reception/orders/${orderId}/handover`), 1600);
    } catch (err) {
      setError(err?.response?.data?.message || "Không thể cập nhật QC PASS.");
    } finally {
      setSaving(false);
    }
  };

  /* ── fail inspection ── */
  const handleFailConfirm = async ({ technicianId, reason }) => {
    setSaving(true);
    setError("");
    setShowFailModal(false);
    try {
      const inspId = inspection?.id ?? inspection?.Id;
      if (inspId) {
        await request({ url: `/api/v1/qc-inspections/${inspId}/failures`, method: "POST",
          data: { reason, assignToTechnicianId: technicianId || null, inspectorId: user?.id ?? user?.staffId } });
      }
      setSuccess("Đã đánh dấu KHÔNG ĐẠT - Trả về thợ sửa lại.");
      setTimeout(() => navigate(`/workshop/foreman/board`), 1600);
    } catch (err) {
      setError(err?.response?.data?.message || "Không thể cập nhật QC FAIL.");
    } finally {
      setSaving(false);
    }
  };

  const isDone = ["PASSED","FAILED"].includes(String(inspection?.status ?? inspection?.Status ?? "").toUpperCase());

  if (loading) return (
    <div className="qc-page"><div className="qc-loading"><div className="qc-spinner" /><span>Đang tải danh sách kiểm tra...</span></div></div>
  );

  return (
    <div className="qc-page">
      {/* header */}
      <div className="qc-header">
        <div>
          <h1 className="qc-title">✅ Kiểm tra chất lượng (QC)</h1>
          <p className="qc-subtitle">
            Lệnh: <strong>{order?.code || `SO-${orderId}`}</strong> ·{" "}
            Xe: <strong>{order?.vehicleLicensePlate || "--"}</strong> ·{" "}
            {order?.vehicleDescriptor || order?.vehicleModelName || ""}
          </p>
        </div>
        <div className="qc-header-actions">
          <button className="qc-btn-back" onClick={() => navigate(-1)}>← Quay lại</button>
          <button className="qc-btn-save" onClick={saveDraft} disabled={saving || isDone}>
            {saving ? "⏳" : "💾"} Lưu tiến trình
          </button>
        </div>
      </div>

      {error   && <div className="qc-alert qc-alert-error">⚠️ {error}</div>}
      {success && <div className="qc-alert qc-alert-success">{success}</div>}

      {isDone && (
        <div className="qc-done-banner">
          Phiên QC này đã được kết thúc với trạng thái:{" "}
          <strong>{String(inspection?.status ?? "").toUpperCase() === "PASSED" ? "✅ ĐẠT" : "❌ KHÔNG ĐẠT"}</strong>
        </div>
      )}

      {/* progress overview */}
      <div className="qc-overview">
        <div className="qc-overview-stats">
          <div className="qc-ov-stat" style={{ color: "#6366f1" }}>
            <span className="qc-ov-num">{stats.answered}/{stats.total}</span>
            <span className="qc-ov-lbl">Đã kiểm tra</span>
          </div>
          <div className="qc-ov-stat" style={{ color: "#10b981" }}>
            <span className="qc-ov-num">{stats.passed}</span>
            <span className="qc-ov-lbl">✅ Đạt</span>
          </div>
          <div className="qc-ov-stat" style={{ color: "#ef4444" }}>
            <span className="qc-ov-num">{stats.failed}</span>
            <span className="qc-ov-lbl">❌ Lỗi</span>
          </div>
          <div className="qc-ov-stat" style={{ color: "#f59e0b" }}>
            <span className="qc-ov-num">{stats.requiredPassed}/{stats.requiredAll}</span>
            <span className="qc-ov-lbl">✳️ Bắt buộc đạt</span>
          </div>
        </div>
        <div className="qc-overview-bar">
          <div className="qc-ov-bar-bg">
            <div
              className="qc-ov-bar-fill"
              style={{
                width: `${stats.progPct}%`,
                background: stats.failed > 0 ? "#ef4444" : stats.progPct === 100 ? "#10b981" : "#3b82f6",
              }}
            />
          </div>
          <span className="qc-ov-pct">{stats.progPct}% hoàn thành</span>
        </div>
      </div>

      {/* checklist groups */}
      <div className="qc-checklist">
        {templateItems.length === 0 ? (
          <div className="qc-no-template">
            <span>ðŸ“‹</span>
            <p>Chưa có mẫu QC nào được cấu hình cho loại lệnh này.</p>
          </div>
        ) : (
          [...grouped.entries()].map(([group, items]) => (
            <ChecklistGroup
              key={group}
              group={group}
              items={items}
              results={results}
              notes={notes}
              onResultChange={(id, v) => setResults(prev => ({ ...prev, [id]: v }))}
              onNoteChange={(id, v) => setNotes(prev => ({ ...prev, [id]: v }))}
              disabled={isDone}
            />
          ))
        )}
      </div>

      {/* action footer */}
      {!isDone && (
        <div className="qc-footer">
          <div className="qc-footer-info">
            {stats.failed > 0
              ? <span className="qc-footer-warn">⚠️ Có {stats.failed} hạng mục lỗi</span>
              : stats.canPass
              ? <span className="qc-footer-ok">✅ Tất cả bắt buộc đã đạt — sẵn sàng nghiệm thu</span>
              : <span className="qc-footer-pending">Hoàn thành tất cả hạng mục bắt buộc (*) để nghiệm thu</span>
            }
          </div>
          <div className="qc-footer-actions">
            <button
              className="qc-btn-fail"
              onClick={() => setShowFailModal(true)}
              disabled={saving}
            >
              ❌ KHÔNG ĐẠT — Trả lại thợ
            </button>
            <button
              className="qc-btn-pass"
              onClick={handlePass}
              disabled={!stats.canPass || saving}
              title={!stats.canPass ? "Cần tất cả hạng mục bắt buộc đạt và không có lỗi" : ""}
            >
              ✅ ĐẠT — Bàn giao xe
            </button>
          </div>
        </div>
      )}

      {showFailModal && (
        <FailModal
          staff={staff}
          onConfirm={handleFailConfirm}
          onClose={() => setShowFailModal(false)}
        />
      )}
    </div>
  );
}


