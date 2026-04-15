import React, { useCallback, useEffect, useMemo, useState } from "react";
import { executionApi } from "@features/workshop/api/executionApi";
import { useAuth } from "@shared/core/context/AuthContext";
import "./ConsumableBulkReturn.css";

/* ── helpers ── */
function fmtNum(v) { return Number(v ?? 0).toLocaleString("vi-VN"); }

/* ── row component ── */
function ReturnRow({ item, usedQty, onChange }) {
  const issued    = Number(item.issuedQty ?? 0);
  const used      = Number(usedQty ?? issued);
  const remaining = issued - used;
  const isLegal   = used >= 0 && used <= issued;

  return (
    <div className={`cbr-row ${!isLegal ? "cbr-row-err" : remaining > 0 ? "cbr-row-return" : ""}`}>
      <div className="cbr-item-info">
        <span className="cbr-item-name">{item.productName ?? `SP-${item.productId}`}</span>
        <span className="cbr-item-unit">{item.unit ?? "cái"}</span>
      </div>
      <div className="cbr-item-issued">
        <span className="cbr-lbl">Đã cấp</span>
        <span className="cbr-val">{fmtNum(issued)}</span>
      </div>
      <div className="cbr-item-used">
        <span className="cbr-lbl">Đã dùng</span>
        <input
          type="number" min={0} max={issued} step={0.5}
          className={`cbr-input ${!isLegal ? "cbr-input-err" : ""}`}
          value={usedQty ?? issued}
          onChange={e => onChange(Number(e.target.value))}
        />
      </div>
      <div className="cbr-item-remaining">
        <span className="cbr-lbl">Hoàn trả</span>
        <span className={`cbr-remaining-val ${remaining > 0 ? "cbr-has-return" : ""}`}>
          {isLegal ? fmtNum(remaining) : "⚠️"}
        </span>
      </div>
    </div>
  );
}

/* ── main component ── */
export default function ConsumableBulkReturn({ serviceOrderId, onSuccess, onCancel }) {
  const { user } = useAuth();
  const [items, setItems]     = useState([]);
  const [usedMap, setUsedMap] = useState({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving]   = useState(false);
  const [error, setError]     = useState("");
  const [done, setDone]       = useState(false);

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const res = await executionApi.getConsumablesForOrder(serviceOrderId);
      const raw = Array.isArray(res) ? res : (res?.items ?? res?.data ?? []);
      const mapped = raw.map(it => ({
        id:          it.id ?? it.Id ?? it.materialRequestItemId,
        productId:   it.productId ?? it.ProductId,
        productName: it.productName ?? it.ProductName ?? it.name,
        unit:        it.unit ?? it.Unit ?? "cái",
        issuedQty:   Number(it.issuedQty ?? it.IssuedQty ?? it.quantity ?? 0),
      }));
      setItems(mapped);
      const m = {};
      mapped.forEach(it => { m[it.id] = it.issuedQty; }); // default: all consumed
      setUsedMap(m);
    } catch {
      setError("Không thể tải danh sách vật tư tiêu hao.");
    } finally {
      setLoading(false);
    }
  }, [serviceOrderId]);

  useEffect(() => { fetchData(); }, [fetchData]);

  const totalReturn = useMemo(() =>
    items.reduce((s, it) => s + Math.max(0, it.issuedQty - Number(usedMap[it.id] ?? it.issuedQty)), 0),
  [items, usedMap]);

  const hasErrors = useMemo(() =>
    items.some(it => { const u = Number(usedMap[it.id] ?? it.issuedQty); return u < 0 || u > it.issuedQty; }),
  [items, usedMap]);

  const handleSubmit = async () => {
    if (hasErrors) return;
    setSaving(true);
    setError("");
    try {
      await executionApi.createConsumableReturnBundle({
        serviceOrderId,
        returnedBy: user?.id ?? user?.staffId,
        items: items
          .map(it => ({
            materialRequestItemId: it.id,
            productId:   it.productId,
            usedQty:     Number(usedMap[it.id] ?? it.issuedQty),
            returnedQty: Math.max(0, it.issuedQty - Number(usedMap[it.id] ?? it.issuedQty)),
            unit:        it.unit,
          }))
          .filter(it => it.returnedQty > 0),
      });
      setDone(true);
      onSuccess?.();
    } catch (err) {
      setError(err?.response?.data?.message || "Không thể gửi phiếu hoàn vật tư.");
    } finally {
      setSaving(false);
    }
  };

  if (done) return (
    <div className="cbr-done">
      <span className="cbr-done-icon">✅</span>
      <p>Đã hoàn trả <strong>{fmtNum(totalReturn)}</strong> đơn vị vật tư còn dư về kho!</p>
    </div>
  );

  return (
    <div className="cbr-wrapper">
      <div className="cbr-header">
        <h3 className="cbr-title">📦 Hoàn trả vật tư tiêu hao</h3>
        <p className="cbr-subtitle">Nhập số lượng đã dùng thực tế — phần còn dư sẽ hoàn kho ngay.</p>
      </div>
      {error && <div className="cbr-alert">{error}</div>}
      {loading ? (
        <div className="cbr-loading"><div className="cbr-spinner" /><span>Đang tải...</span></div>
      ) : items.length === 0 ? (
        <div className="cbr-empty">Không có vật tư tiêu hao cần hoàn trả.</div>
      ) : (
        <>
          <div className="cbr-list-header">
            <span>Vật tư</span><span>Đã cấp</span><span>Đã dùng</span><span>Hoàn trả</span>
          </div>
          <div className="cbr-list">
            {items.map(item => (
              <ReturnRow key={item.id} item={item} usedQty={usedMap[item.id]}
                onChange={v => setUsedMap(p => ({ ...p, [item.id]: v }))} />
            ))}
          </div>
          {totalReturn > 0 && (
            <div className="cbr-summary">
              📦 Tổng sẽ hoàn kho: <strong>{fmtNum(totalReturn)}</strong> đơn vị
            </div>
          )}
          <div className="cbr-footer">
            {onCancel && <button className="cbr-btn cbr-btn-ghost" onClick={onCancel} disabled={saving}>Hủy</button>}
            <button className="cbr-btn cbr-btn-submit" onClick={handleSubmit} disabled={saving || hasErrors}>
              {saving ? "⏳ Đang gửi..." : "✅ Xác nhận hoàn vật tư"}
            </button>
          </div>
        </>
      )}
    </div>
  );
}
