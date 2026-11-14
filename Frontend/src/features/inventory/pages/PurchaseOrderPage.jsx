import React, { useState, useEffect, useMemo, useCallback, useRef } from "react";
import { useSearchParams, useNavigate, useLocation } from "react-router-dom";
import { request } from "@app/apiClient";
import { purchaseOrderApi } from "../api/purchaseOrderApi";
import { useAuth } from "@shared/core/context/AuthContext";
import "./PurchaseOrder.css";

const DEFAULT_PAGE_SIZE = 25;
const PAGE_LABELS = { previous: "Trước", next: "Sau", page: "Trang" };

function fmtCurrency(v) { return Number(v ?? 0).toLocaleString("vi-VN") + " ₫"; }
function fmtNum(v) { return Number(v ?? 0).toLocaleString("vi-VN"); }
function fmtDate(v) { return v ? new Date(v).toLocaleDateString("vi-VN") : "--"; }

export default function PurchaseOrderPage() {
  const { user } = useAuth();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const location = useLocation();

  const isNew = location.pathname.endsWith("/new") || searchParams.get("action") === "new" || searchParams.has("productId");
  const defaultProductId = searchParams.get("productId") || "";

  // -- FOR LIST VIEW --
  const [pos, setPos] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [total, setTotal] = useState(0);
  const listSequence = useRef(0);
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
    const sequence = ++listSequence.current;
    setLoading(true);
    setError("");
    try {
      const res = await purchaseOrderApi.getPurchaseOrders({
        branchId: user?.branchId,
        statusGroup: activeTab,
        envelope: true,
        page,
        pageSize
      });
      if (sequence !== listSequence.current) return;
      const count = Number(res?.total) || (Array.isArray(res?.items) ? res.items.length : Array.isArray(res) ? res.length : 0);
      const lastPage = Math.max(1, Math.ceil(count / pageSize));
      if (page > lastPage) { setPage(lastPage); return; }
      setPos(Array.isArray(res?.items) ? res.items : Array.isArray(res) ? res : []);
      setTotal(count);
    } catch (e) {
      if (sequence === listSequence.current) { setError("Không thể tải danh sách PO."); setPos([]); setTotal(0); }
    } finally {
      if (sequence === listSequence.current) setLoading(false);
    }
  }, [user?.branchId, isNew, activeTab, page, pageSize]);

  useEffect(() => {
    if (!isNew) loadPos();
    else {
      // load form dictionaries
      request({ url: "/api/suppliers", method: "GET" }).then(res => setSuppliers(res?.items ?? res ?? []));
      request({ url: "/api/products", method: "GET", params: { branchId: user?.branchId } }).then(res => setProducts(res?.items ?? res ?? []));
    }
    return () => { listSequence.current += 1; };
  }, [isNew, loadPos, user]);

  const filteredPos = pos;

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
       setError("Vui lòng nhập đầy đủ thông tin sản phẩm và số lượng > 0");
       return;
    }
    setSaving(true);
    setError("");
    try {
      await purchaseOrderApi.createPurchaseOrder({
        ...form,
        branchId: user?.branchId,
        createdBy: user?.id,
      });
      // success, go back to list
      navigate("/workshop/inventory/purchase-orders");
    } catch (err) {
      setError(err?.response?.data?.message || err?.message || "Lỗi khi tạo PO.");
    } finally {
      setSaving(false);
    }
  };

  const handleApprove = async (id) => {
    try {
      await purchaseOrderApi.approvePurchaseOrder(id, { approvedBy: user?.id });
      loadPos();
    } catch (e) {
      alert("Lỗi khi duyệt: " + (e?.response?.data?.message || e?.message || e));
    }
  };

  /* -- RENDER FORM -- */
  if (isNew) {
    return (
      <div className="po-page">
        <div className="po-header">
          <h1 className="po-title">📝 Tạo Đơn Đặt Hàng (PO)</h1>
          <button className="po-btn po-btn-ghost" onClick={() => navigate("/workshop/inventory/purchase-orders")}>🔙 Trở về danh sách</button>
        </div>

        <form className="po-board" onSubmit={handleSubmit}>
          {error && <div className="po-alert err">{error}</div>}
          
          <div className="po-grid">
            <div className="po-form-group">
              <label>Nhà cung cấp (*)</label>
              <select value={form.supplierId} onChange={e => setForm(f => ({ ...f, supplierId: e.target.value }))} required>
                <option value="">-- Chọn Nhà Cung Cấp --</option>
                {suppliers.map(s => (
                  <option key={s.id} value={s.id}>
                    {s.supplierName || s.name || `NCC #${s.id}`} {s.taxCode || s.code ? `(${s.taxCode || s.code})` : ""}
                  </option>
                ))}
              </select>
            </div>
            <div className="po-form-group">
              <label>Ngày dự kiến nhận</label>
              <input type="date" value={form.expectedDate} onChange={e => setForm(f => ({ ...f, expectedDate: e.target.value }))} />
            </div>
            <div className="po-form-group span-full">
              <label>Ghi chú</label>
              <input type="text" value={form.notes} onChange={e => setForm(f => ({ ...f, notes: e.target.value }))} placeholder="Nhập ghi chú cho NCC..." />
            </div>
          </div>

          <h3 className="po-section-title">Danh sách vật tư / phụ tùng</h3>
          <div className="po-items">
            <div className="po-items-header">
              <span>Sản phẩm</span><span>Số lượng</span><span>Đơn giá nhập</span><span>Thành tiền</span><span></span>
            </div>
            {form.items.map((item, idx) => (
              <div className="po-item-row" key={idx}>
                <select value={item.productId} onChange={e => updateItem(idx, 'productId', e.target.value)} required>
                  <option value="">-- Chọn SP --</option>
                  {products.map(p => <option key={p.id} value={p.id}>{p.name} (Tồn: {p.currentStock ?? 0})</option>)}
                </select>
                <input type="number" min="0.5" step="0.5" value={item.qty} onChange={e => updateItem(idx, 'qty', Number(e.target.value))} required />
                <input type="number" min="0" step="1000" value={item.unitPrice} onChange={e => updateItem(idx, 'unitPrice', Number(e.target.value))} required />
                <div className="po-row-total">{fmtCurrency(item.qty * item.unitPrice)}</div>
                <button type="button" className="po-btn-icon text-red-500" onClick={() => removeItem(idx)}>🗑️</button>
              </div>
            ))}
          </div>
          <button type="button" className="po-btn po-btn-outline" onClick={addItem}>+ Thêm dòng</button>

          <div className="po-summary">
            Tổng cộng: <strong>{fmtCurrency(totalAmount)}</strong>
          </div>

          <div className="po-footer">
             <button type="submit" className="po-btn po-btn-primary" disabled={saving || form.items.length === 0}>
               {saving ? "⏳ Đang tạo..." : "💾 Lưu & Tạo PO"}
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
          <h1 className="po-title">📦 Quản lý Đơn Đặt Hàng (PO)</h1>
          <p className="po-subtitle">Theo dõi tình trạng đặt mua và phê duyệt nhập kho.</p>
        </div>
        <button className="po-btn po-btn-primary" onClick={() => navigate("/workshop/inventory/purchase-orders/new")}>
          + Lập PO mới
        </button>
      </div>

      <div className="po-tabs">
        {[{k:"ALL", l:"Tất cả"}, {k:"PENDING", l:"Chờ duyệt"}, {k:"APPROVED", l:"Đã duyệt / Đang giao"}, {k:"RECEIVED", l:"Đã nhận (GRN)"}].map(t => (
          <button key={t.k} className={`po-tab ${activeTab === t.k ? "active" : ""}`} onClick={() => { setPage(1); setActiveTab(t.k); }}>{t.l}</button>
        ))}
      </div>

      <div className="po-board">
        {error && <div className="po-alert err">{error}</div>}
        {loading ? <div className="po-loading">Đang tải...</div> : 
        filteredPos.length === 0 ? <div className="po-empty">Không có đơn đặt hàng nào.</div> : (
           <table className="po-table">
             <thead>
               <tr>
                 <th>Mã PO</th>
                 <th>Ngày tạo</th>
                 <th>Nhà cung cấp</th>
                 <th className="t-right">Tổng tiền</th>
                 <th>Trạng thái</th>
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
                       {st === "PENDING" && ["ADMIN", "MANAGER"].includes(String(user?.roleCode || "").toUpperCase()) && (
                         <button className="po-btn-sm bg-green-100 text-green-700" onClick={() => handleApprove(p.id)}>
                           ✓ Duyệt
                         </button>
                       )}
                       {(st === "APPROVED" || st === "PARTIAL_RECEIVED") && (
                         <button className="po-btn-sm bg-blue-100 text-blue-700" onClick={() => navigate(`/workshop/inventory/goods-receipt/new?poId=${p.id}`)}>
                           📦 Nhập kho (GRN)
                         </button>
                       )}
                     </td>
                   </tr>
                 );
               })}
             </tbody>
           </table>
         )}
        {!loading && (
          <div className="po-pagination flex items-center justify-between mt-4 text-sm text-slate-600">
            <div>Tổng số: <strong>{total}</strong> đơn đặt hàng</div>
            <div className="flex items-center gap-4">
              <label className="flex items-center gap-1">
                Hiển thị:
                <select 
                  value={pageSize} 
                  onChange={e => { setPageSize(Number(e.target.value)); setPage(1); }}
                  className="border rounded px-2 py-1 bg-white text-slate-700"
                >
                  <option value={10}>10</option>
                  <option value={25}>25</option>
                  <option value={50}>50</option>
                </select>
                / trang
              </label>
              <nav aria-label={PAGE_LABELS.page} className="flex items-center gap-2">
                <button 
                  type="button" 
                  className="po-btn po-btn-ghost text-xs py-1 px-3" 
                  disabled={page <= 1} 
                  onClick={() => setPage(value => value - 1)}
                >
                  {PAGE_LABELS.previous}
                </button>
                <span> {PAGE_LABELS.page} {page} / {Math.max(1, Math.ceil(total / pageSize))} </span>
                <button 
                  type="button" 
                  className="po-btn po-btn-ghost text-xs py-1 px-3" 
                  disabled={page * pageSize >= total} 
                  onClick={() => setPage(value => value + 1)}
                >
                  {PAGE_LABELS.next}
                </button>
              </nav>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
