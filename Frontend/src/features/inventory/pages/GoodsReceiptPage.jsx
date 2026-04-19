import React, { useState, useEffect, useMemo, useCallback } from "react";
import { useSearchParams, useNavigate } from "react-router-dom";
import { request } from "@app/apiClient";
import { useAuth } from "@shared/core/context/AuthContext";
import "./GoodsReceipt.css";

function fmtCurrency(v) { return Number(v ?? 0).toLocaleString("vi-VN") + " ?"; }
function fmtDate(v) { return v ? new Date(v).toLocaleDateString("vi-VN") : "--"; }

export default function GoodsReceiptPage() {
  const { user } = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();

  const isNew = searchParams.get("action") === "new" || searchParams.has("poId");
  const defaultPoId = searchParams.get("poId") || "";

  // -- FOR LIST VIEW --
  const [grns, setGrns] = useState([]);
  const [loadingList, setLoadingList] = useState(false);
  const [listError, setListError] = useState("");

  // -- FOR FORM VIEW --
  const [approvedPos, setApprovedPos] = useState([]);
  const [selectedPo, setSelectedPo] = useState(null);
  
  const [form, setForm] = useState({
    purchaseOrderId: defaultPoId,
    supplierId: "",
    notes: "",
    items: []
  });
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");

  const loadGrns = useCallback(async () => {
    if (isNew) return;
    setLoadingList(true);
    try {
      const res = await request({ url: "/api/goods-receipts", method: "GET", params: { branchId: user?.branchId } });
      setGrns(res?.items ?? res ?? []);
    } catch (e) {
      setListError("Kh?ng th? t?i danh s?ch phi?u nh?p.");
    } finally {
      setLoadingList(false);
    }
  }, [user, isNew]);

  const loadPos = useCallback(async () => {
    if (!isNew) return;
    try {
      const res = await request({ url: "/api/purchase-orders", method: "GET", params: { branchId: user?.branchId, status: "APPROVED" } });
      setApprovedPos(res?.items ?? res ?? []);
    } catch (e) {
      console.error(e);
    }
  }, [user, isNew]);

  useEffect(() => {
    if (!isNew) loadGrns();
    else loadPos();
  }, [isNew, loadGrns, loadPos]);

  // Load PO details when PO is selected
  useEffect(() => {
    if (!isNew || !form.purchaseOrderId) return;
    const po = approvedPos.find(p => String(p.id) === String(form.purchaseOrderId));
    if (!po) return;

    setSelectedPo(po);
    setForm(f => ({
      ...f,
      supplierId: po.supplierId,
      items: (po.items || []).map(i => ({
        poItemId: i.id,
        productId: i.productId,
        productName: i.product?.name ?? "N/A",
        orderedQty: i.qty,
        receivedQty: i.qty, // default to receiving exactly as ordered
        unitPrice: i.unitPrice
      }))
    }));
  }, [form.purchaseOrderId, approvedPos, isNew]);

  const updateItemQty = (idx, qty) => {
    const list = [...form.items];
    list[idx].receivedQty = qty;
    setForm(f => ({ ...f, items: list }));
  };

  const hasItemErrors = useMemo(() => {
    return form.items.some(i => i.receivedQty < 0 || i.receivedQty > i.orderedQty * 1.5);
  }, [form.items]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!form.purchaseOrderId || form.items.length === 0 || hasItemErrors) return;
    setSaving(true);
    setFormError("");
    try {
      await request({
        url: "/api/goods-receipts",
        method: "POST",
        data: {
          ...form,
          branchId: user?.branchId,
          receivedBy: user?.id,
          items: form.items.filter(i => i.receivedQty > 0) // only receive items with qty > 0
        }
      });
      setSearchParams(new URLSearchParams());
    } catch (err) {
      setFormError(err?.response?.data?.message || "L?i khi t?o phi?u nh?p kho.");
    } finally {
      setSaving(false);
    }
  };

  /* -- RENDER FORM -- */
  if (isNew) {
    return (
      <div className="grn-page">
        <div className="grn-header">
          <h1 className="grn-title">?? T?o Phi?u Nh?p Kho (GRN)</h1>
          <button className="grn-btn grn-btn-ghost" onClick={() => setSearchParams(new URLSearchParams())}>🔙 Xem danh sách</button>
        </div>

        <form className="grn-board" onSubmit={handleSubmit}>
          {formError && <div className="grn-alert err">{formError}</div>}
          
          <div className="grn-grid">
            <div className="grn-form-group">
              <label>Đơn Đặt H�ng (PO) d� duy?t</label>
              <select value={form.purchaseOrderId} onChange={e => setForm(f => ({ ...f, purchaseOrderId: e.target.value }))} required>
                <option value="">-- Chọn don d?t h�ng --</option>
                {approvedPos.map(p => <option key={p.id} value={p.id}>#{p.code || p.id} - {p.supplierName || p.supplier?.name}</option>)}
              </select>
            </div>
            <div className="grn-form-group span-2">
              <label>Ghi ch� nh?p kho</label>
              <input type="text" value={form.notes} onChange={e => setForm(f => ({ ...f, notes: e.target.value }))} placeholder="Nh?p ghi ch� t�nh tr?ng h�ng, h�a don..." />
            </div>
          </div>

          {selectedPo && (
            <div className="grn-po-info">
              <span>Đang nh?p cho <strong>#{selectedPo.code || selectedPo.id}</strong></span>
              <span>NCC: <strong>{selectedPo.supplierName || selectedPo.supplier?.name}</strong></span>
            </div>
          )}

          <h3 className="grn-section-title">Chi ti?t h�ng h�a nh?p kho</h3>
          {form.items.length === 0 ? (
            <div className="grn-empty-list">Vui l�ng chọn PO d? hi?n th? h�ng h�a</div>
          ) : (
            <div className="grn-items">
              <div className="grn-items-header">
                <span>S?n ph?m</span><span>SL Đặt</span><span>SL Th?c Nh?n</span>
              </div>
              {form.items.map((item, idx) => {
                const isOver = item.receivedQty > item.orderedQty;
                return (
                  <div className="grn-item-row" key={idx}>
                    <div className="font-semibold">{item.productName}</div>
                    <div className="text-slate-500 font-mono">{item.orderedQty}</div>
                    <div>
                      <input 
                        type="number" min="0" step="0.5" 
                        className={isOver ? "input-warn" : ""}
                        value={item.receivedQty} 
                        onChange={e => updateItem(idx, Number(e.target.value))} 
                        required 
                      />
                      {isOver && <div className="grn-warn-text">?? Nh?n l? SL d?t</div>}
                    </div>
                  </div>
                )
              })}
            </div>
          )}

          <div className="grn-footer">
             <button type="submit" className="grn-btn grn-btn-primary" disabled={saving || form.items.length === 0 || hasItemErrors}>
               {saving ? "? Đang ghi nh?n..." : "?? Ghi N?n Nh?p Kho"}
             </button>
          </div>
        </form>
      </div>
    );
  }

  /* -- RENDER LIST -- */
  return (
    <div className="grn-page">
      <div className="grn-header">
        <div>
          <h1 className="grn-title">?? Phi?u Nh?p Kho (GRN)</h1>
          <p className="grn-subtitle">L?ch s? nh?n h?ng h?a nh?p v?o kho chi nh?nh.</p>
        </div>
        <button className="grn-btn grn-btn-primary" onClick={() => setSearchParams(new URLSearchParams({ action: "new" }))}>
          + Nh?p kho t? PO
        </button>
      </div>

      <div className="grn-board">
        {loadingList ? <div className="grn-loading">Đang t?i...</div> : 
         grns.length === 0 ? <div className="grn-empty">Chua c� dữ liệu phi?u nh?p.</div> : (
           <table className="grn-table">
             <thead>
               <tr>
                 <th>M� Phi?u Nh?p</th>
                 <th>Ngày Nh?p</th>
                 <th>M� PO</th>
                 <th>Ngu?i Nh?n</th>
               </tr>
             </thead>
             <tbody>
               {grns.map(g => (
                 <tr key={g.id || g.Id}>
                   <td className="font-semibold text-emerald-700">#{g.code || g.id}</td>
                   <td>{fmtDate(g.createdAt || g.CreatedAt)}</td>
                   <td><span className="grn-po-badge">#{g.purchaseOrderCode || g.purchaseOrderId}</span></td>
                   <td>{g.receiverName || g.receivedBy}</td>
                 </tr>
               ))}
             </tbody>
           </table>
         )}
      </div>
    </div>
  );
}


