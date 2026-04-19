import React, { useState, useEffect, useMemo, useCallback } from "react";
import { useSearchParams, useNavigate } from "react-router-dom";
import { request } from "@app/apiClient";
import { useAuth } from "@shared/core/context/AuthContext";
import "./PurchaseOrder.css";

function fmtCurrency(v) { return Number(v ?? 0).toLocaleString("vi-VN") + " ?"; }
function fmtNum(v) { return Number(v ?? 0).toLocaleString("vi-VN"); }
function fmtDate(v) { return v ? new Date(v).toLocaleDateString("vi-VN") : "--"; }

export default function PurchaseOrderPage() {
  const { user } = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();

  const isNew = searchParams.get("action") === "new" || searchParams.has("productId");
  const defaultProductId = searchParams.get("productId") || "";

  // -- FOR LIST VIEW --
  const [pos, setPos] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [activeTab, setActiveTab] = useState("ALL"); // ALL, PENDING, APPROVED, RECEIVED

  // -- FOR FORM VIEW --
  const [suppliers, setSuppliers] = useState([]);
  const [products, setProducts] = useState([]);
  
  const [form, setForm] = useState({
    supplierId: "",
    expectedDate: "",
    notes: "",
    items: defaultProductId ? [{ productId: defaultProductId, qty: 1, unitPrice: 0 }] : []
  });
  const [saving, setSaving] = useState(false);

  // Load PO list
  const loadPos = useCallback(async () => {
    if (isNew) return;
    setLoading(true);
    try {
      const res = await request({ url: "/api/purchase-orders", method: "GET", params: { branchId: user?.branchId } });
      setPos(res?.items ?? res ?? []);
    } catch (e) {
      setError("Kh?ng th? t?i danh s?ch PO.");
    } finally {
      setLoading(false);
    }
  }, [user, isNew]);

  useEffect(() => {
    if (!isNew) loadPos();
    else {
      // load form dictionaries
      request({ url: "/api/suppliers", method: "GET" }).then(res => setSuppliers(res?.items ?? res ?? []));
      request({ url: "/api/products", method: "GET", params: { branchId: user?.branchId } }).then(res => setProducts(res?.items ?? res ?? []));
    }
  }, [isNew, loadPos, user]);

  const filteredPos = useMemo(() => {
    if (activeTab === "ALL") return pos;
    return pos.filter(p => p.status === activeTab || p.Status === activeTab);
  }, [pos, activeTab]);

  /* -- FORM HANDLERS -- */
  const addItem = () => setForm(f => ({ ...f, items: [...f.items, { productId: "", qty: 1, unitPrice: 0 }] }));
  const updateItem = (idx, field, val) => {
    const list = [...form.items];
    if (field === 'productId') {
      const p = products.find(x => String(x.id) === String(val));
      if (p) list[idx].unitPrice = p.costPrice ?? p.price ?? 0;
    }
    list[idx][field] = val;
    setForm(f => ({ ...f, items: list }));
  };
  const removeItem = (idx) => setForm(f => ({ ...f, items: f.items.filter((_, i) => i !== idx) }));

  const totalAmount = useMemo(() => form.items.reduce((sum, item) => sum + (item.qty * item.unitPrice), 0), [form.items]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!form.supplierId || form.items.length === 0) return;
    if (form.items.some(i => !i.productId || i.qty <= 0)) {
       setError("Vui l�ng nh?p d?y d? thông tin s?n ph?m v� s? lu?ng > 0");
       return;
    }
    setSaving(true);
    setError("");
    try {
      await request({
        url: "/api/purchase-orders",
        method: "POST",
        data: {
          ...form,
          branchId: user?.branchId,
          createdBy: user?.id,
        }
      });
      // success, go back to list
      setSearchParams(new URLSearchParams());
    } catch (err) {
      setError(err?.response?.data?.message || "L?i khi t?o PO.");
    } finally {
      setSaving(false);
    }
  };

  const handleApprove = async (id) => {
    try {
      await request({ url: `/api/purchase-orders/${id}/approve`, method: "PUT", data: { approvedBy: user?.id } });
      loadPos();
    } catch (e) {
      alert("L?i khi duy?t: " + (e?.response?.data?.message || e.message));
    }
  };

  /* -- RENDER FORM -- */
  if (isNew) {
    return (
      <div className="po-page">
        <div className="po-header">
          <h1 className="po-title">?? T?o Đơn Đặt H�ng (PO)</h1>
          <button className="po-btn po-btn-ghost" onClick={() => setSearchParams(new URLSearchParams())}>?? Tr? v? danh s?ch</button>
        </div>

        <form className="po-board" onSubmit={handleSubmit}>
          {error && <div className="po-alert err">{error}</div>}
          
          <div className="po-grid">
            <div className="po-form-group">
              <label>Nh� cung c?p (*)</label>
              <select value={form.supplierId} onChange={e => setForm(f => ({ ...f, supplierId: e.target.value }))} required>
                <option value="">-- Chọn Nh� Cung C?p --</option>
                {suppliers.map(s => <option key={s.id} value={s.id}>{s.name} ({s.code})</option>)}
              </select>
            </div>
            <div className="po-form-group">
              <label>Ngày d? ki?n nh?n</label>
              <input type="date" value={form.expectedDate} onChange={e => setForm(f => ({ ...f, expectedDate: e.target.value }))} />
            </div>
            <div className="po-form-group span-full">
              <label>Ghi ch�</label>
              <input type="text" value={form.notes} onChange={e => setForm(f => ({ ...f, notes: e.target.value }))} placeholder="Nh?p ghi ch� cho NCC..." />
            </div>
          </div>

          <h3 className="po-section-title">Danh s?ch vật tư / ph? tổng</h3>
          <div className="po-items">
            <div className="po-items-header">
              <span>S?n ph?m</span><span>S? lu?ng</span><span>?on gi? nh?p</span><span>Th?nh ti?n</span><span></span>
            </div>
            {form.items.map((item, idx) => (
              <div className="po-item-row" key={idx}>
                <select value={item.productId} onChange={e => updateItem(idx, 'productId', e.target.value)} required>
                  <option value="">-- Chọn SP --</option>
                  {products.map(p => <option key={p.id} value={p.id}>{p.name} (T?n: {p.currentStock ?? 0})</option>)}
                </select>
                <input type="number" min="0.5" step="0.5" value={item.qty} onChange={e => updateItem(idx, 'qty', Number(e.target.value))} required />
                <input type="number" min="0" step="1000" value={item.unitPrice} onChange={e => updateItem(idx, 'unitPrice', Number(e.target.value))} required />
                <div className="po-row-total">{fmtCurrency(item.qty * item.unitPrice)}</div>
                <button type="button" className="po-btn-icon text-red-500" onClick={() => removeItem(idx)}>🗑️</button>
              </div>
            ))}
          </div>
          <button type="button" className="po-btn po-btn-outline" onClick={addItem}>+ Th�m d�ng</button>

          <div className="po-summary">
            Tổng cầng: <strong>{fmtCurrency(totalAmount)}</strong>
          </div>

          <div className="po-footer">
             <button type="submit" className="po-btn po-btn-primary" disabled={saving || form.items.length === 0}>
               {saving ? "? Đang t?o..." : "? Luu & T?o PO"}
             </button>
          </div>
        </form>
      </div>
    );
  }

  /* -- RENDER LIST -- */
  return (
    <div className="po-page">
      <div className="po-header">
        <div>
          <h1 className="po-title">?? Quản lý Đơn Đặt H�ng (PO)</h1>
          <p className="po-subtitle">Theo d�i t�nh tr?ng d?t mua v� ph� duy?t nh?p kho.</p>
        </div>
        <button className="po-btn po-btn-primary" onClick={() => setSearchParams(new URLSearchParams({ action: "new" }))}>
          + L?p PO m?i
        </button>
      </div>

      <div className="po-tabs">
        {[{k:"ALL", l:"T?t c?"}, {k:"PENDING", l:"Ch? duy?t"}, {k:"APPROVED", l:"Đã duy?t / Đang giao"}, {k:"RECEIVED", l:"Đã nh?n (GRN)"}].map(t => (
          <button key={t.k} className={`po-tab ${activeTab === t.k ? "active" : ""}`} onClick={() => setActiveTab(t.k)}>{t.l}</button>
        ))}
      </div>

      <div className="po-board">
        {loading ? <div className="po-loading">Đang t?i...</div> : 
         filteredPos.length === 0 ? <div className="po-empty">Không c� don d?t h�ng n�o.</div> : (
           <table className="po-table">
             <thead>
               <tr>
                 <th>M� PO</th>
                 <th>Ngày t?o</th>
                 <th>Nh� cung c?p</th>
                 <th className="t-right">Tổng ti?n</th>
                 <th>Tr?ng th?i</th>
                 <th>Thao tác</th>
               </tr>
             </thead>
             <tbody>
               {filteredPos.map(p => {
                 const st = p.status || p.Status || "PENDING";
                 const amount = p.totalAmount || p.TotalAmount || 0;
                 return (
                   <tr key={p.id || p.Id}>
                     <td className="font-semibold text-blue-700">#{p.code || p.id}</td>
                     <td>{fmtDate(p.createdAt || p.CreatedAt)}</td>
                     <td>{p.supplierName || p.supplier?.name || "N/A"}</td>
                     <td className="t-right font-bold text-slate-700">{fmtCurrency(amount)}</td>
                     <td>
                        <span className={`po-status st-${st.toLowerCase()}`}>{st}</span>
                     </td>
                     <td>
                       {st === "PENDING" && (user?.role === "ADMIN" || user?.role === "MANAGER") && (
                         <button className="po-btn-sm bg-green-100 text-green-700" onClick={() => handleApprove(p.id)}>
                           ? Duy?t
                         </button>
                       )}
                       {st === "APPROVED" && (
                         <button className="po-btn-sm bg-blue-100 text-blue-700" onClick={() => navigate(`/inventory/goods-receipt/new?poId=${p.id}`)}>
                           ?? Nh?p kho (GRN)
                         </button>
                       )}
                     </td>
                   </tr>
                 );
               })}
             </tbody>
           </table>
         )}
      </div>
    </div>
  );
}


