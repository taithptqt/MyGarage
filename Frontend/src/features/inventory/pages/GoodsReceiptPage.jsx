import React, { useState, useEffect, useMemo, useCallback, useRef } from "react";
import { useSearchParams, useNavigate, useLocation } from "react-router-dom";
import { goodsReceiptApi } from "../api/goodsReceiptApi";
import { purchaseOrderApi } from "../api/purchaseOrderApi";
import { useAuth } from "@shared/core/context/AuthContext";
import "./GoodsReceipt.css";

const DEFAULT_PAGE_SIZE = 25;
const PAGE_LABELS = { previous: "Trước", next: "Sau", page: "Trang" };

function fmtCurrency(v) { return Number(v ?? 0).toLocaleString("vi-VN") + " ₫"; }
function fmtDate(v) { return v ? new Date(v).toLocaleDateString("vi-VN") : "--"; }

export default function GoodsReceiptPage() {
  const { user } = useAuth();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const location = useLocation();

  const isNew = location.pathname.endsWith("/new") || searchParams.get("action") === "new" || searchParams.has("poId");
  const defaultPoId = searchParams.get("poId") || "";

  // -- FOR LIST VIEW --
  const [grns, setGrns] = useState([]);
  const [loadingList, setLoadingList] = useState(false);
  const [listError, setListError] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [total, setTotal] = useState(0);
  const listSequence = useRef(0);

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
    const sequence = ++listSequence.current;
    setLoadingList(true);
    setListError("");
    try {
      const res = await goodsReceiptApi.getGoodsReceipts({
        branchId: user?.branchId,
        envelope: true,
        page,
        pageSize
      });
      if (sequence !== listSequence.current) return;
      const count = Number(res?.total) || (Array.isArray(res?.items) ? res.items.length : Array.isArray(res) ? res.length : 0);
      const lastPage = Math.max(1, Math.ceil(count / pageSize));
      if (page > lastPage) {
        setPage(lastPage);
        return;
      }
      setGrns(Array.isArray(res?.items) ? res.items : Array.isArray(res) ? res : []);
      setTotal(count);
    } catch (e) {
      if (sequence === listSequence.current) {
        setListError("Không thể tải danh sách phiếu nhập.");
        setGrns([]);
        setTotal(0);
      }
    } finally {
      if (sequence === listSequence.current) setLoadingList(false);
    }
  }, [user?.branchId, isNew, page, pageSize]);

  const loadPos = useCallback(async () => {
    if (!isNew) return;
    try {
      const res = await purchaseOrderApi.getPurchaseOrders({
        branchId: user?.branchId,
        statusGroup: "APPROVED"
      });
      setApprovedPos(res?.items ?? res ?? []);
    } catch (e) {
      console.error(e);
    }
  }, [user?.branchId, isNew]);

  useEffect(() => {
    if (!isNew) loadGrns();
    else loadPos();
    return () => { listSequence.current += 1; };
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
      items: (po.items || []).map(i => {
        const remaining = Number(i.remainingQuantity ?? (i.qty != null ? Math.max(0, i.qty - (i.receivedQuantity ?? 0)) : (i.orderedQuantity ?? 0)));
        const ordered = Number(i.orderedQuantity ?? i.qty ?? 0);
        const alreadyReceived = Number(i.receivedQuantity ?? 0);
        return {
          poItemId: i.id || i.Id,
          productId: i.productId || i.ProductId,
          productName: i.product?.name ?? i.productName ?? "N/A",
          orderedQty: ordered,
          alreadyReceivedQty: alreadyReceived,
          remainingQuantity: remaining,
          receivedQty: remaining > 0 ? remaining : 0,
          unitPrice: i.unitPrice ?? i.unitCost ?? 0
        };
      })
    }));
  }, [form.purchaseOrderId, approvedPos, isNew]);

  const updateItemQty = (idx, qty) => {
    const list = [...form.items];
    list[idx].receivedQty = qty;
    setForm(f => ({ ...f, items: list }));
  };

  const hasItemErrors = useMemo(() => {
    return form.items.some(i => i.receivedQty < 0 || i.receivedQty > ((i.remainingQuantity ?? i.orderedQty) * 1.5));
  }, [form.items]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!form.purchaseOrderId || form.items.length === 0 || hasItemErrors) return;
    setSaving(true);
    setFormError("");
    try {
      await goodsReceiptApi.createGoodsReceipt({
        ...form,
        branchId: user?.branchId,
        receivedBy: user?.id,
        items: form.items
          .filter(i => i.receivedQty > 0)
          .map(i => ({
            poItemId: i.poItemId,
            productId: i.productId,
            receivedQty: i.receivedQty,
            unitPrice: i.unitPrice
          }))
      });
      navigate("/workshop/inventory/goods-receipts");
    } catch (err) {
      setFormError(err?.response?.data?.message || err?.message || "Lỗi khi tạo phiếu nhập kho.");
    } finally {
      setSaving(false);
    }
  };

  /* -- RENDER FORM -- */
  if (isNew) {
    return (
      <div className="grn-page">
        <div className="grn-header">
          <h1 className="grn-title">📥 Tạo Phiếu Nhập Kho (GRN)</h1>
          <button className="grn-btn grn-btn-ghost" onClick={() => navigate("/workshop/inventory/goods-receipts")}>🔙 Xem danh sách</button>
        </div>

        <form className="grn-board" onSubmit={handleSubmit}>
          {formError && <div className="grn-alert err">{formError}</div>}
          
          <div className="grn-grid">
            <div className="grn-form-group">
              <label>Đơn Đặt Hàng (PO) đã duyệt</label>
              <select value={form.purchaseOrderId} onChange={e => setForm(f => ({ ...f, purchaseOrderId: e.target.value }))} required>
                <option value="">-- Chọn đơn đặt hàng --</option>
                {approvedPos.map(p => (
                  <option key={p.id} value={p.id}>
                    #{p.code || p.id} - {p.supplierName || p.supplier?.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="grn-form-group span-2">
              <label>Ghi chú nhập kho</label>
              <input type="text" value={form.notes} onChange={e => setForm(f => ({ ...f, notes: e.target.value }))} placeholder="Nhập ghi chú tình trạng hàng, hóa đơn..." />
            </div>
          </div>

          {selectedPo && (
            <div className="grn-po-info">
              <span>Đang nhập cho <strong>#{selectedPo.code || selectedPo.id}</strong></span>
              <span>NCC: <strong>{selectedPo.supplierName || selectedPo.supplier?.name}</strong></span>
            </div>
          )}

          <h3 className="grn-section-title">Chi tiết hàng hóa nhập kho</h3>
          {form.items.length === 0 ? (
            <div className="grn-empty-list">Vui lòng chọn PO để hiển thị hàng hóa</div>
          ) : (
            <div className="grn-items">
              <div className="grn-items-header">
                <span>Sản phẩm</span><span>SL Đặt / Còn lại</span><span>SL Thực Nhận</span>
              </div>
              {form.items.map((item, idx) => {
                const maxExpected = item.remainingQuantity ?? item.orderedQty;
                const isOver = item.receivedQty > maxExpected;
                return (
                  <div className="grn-item-row" key={idx}>
                    <div className="font-semibold">{item.productName}</div>
                    <div className="text-slate-500 font-mono text-sm">
                      {item.orderedQty}
                      {item.alreadyReceivedQty > 0 && (
                        <span className="text-xs text-blue-600 block">
                          (Đã nhận: {item.alreadyReceivedQty}, Còn: {item.remainingQuantity})
                        </span>
                      )}
                    </div>
                    <div>
                      <input 
                        type="number" min="0" step="0.5" 
                        className={isOver ? "input-warn" : ""}
                        value={item.receivedQty} 
                        onChange={e => updateItemQty(idx, Number(e.target.value))} 
                        required 
                      />
                      {isOver && (
                        <div className="grn-warn-text">
                          ⚠️ Nhận lố SL còn lại ({maxExpected})
                        </div>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          )}

          <div className="grn-footer">
             <button type="submit" className="grn-btn grn-btn-primary" disabled={saving || form.items.length === 0 || hasItemErrors}>
               {saving ? "⏳ Đang ghi nhận..." : "📥 Ghi Nhận Nhập Kho"}
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
          <h1 className="grn-title">📦 Phiếu Nhập Kho (GRN)</h1>
          <p className="grn-subtitle">Lịch sử nhận hàng hóa nhập vào kho chi nhánh.</p>
        </div>
        <button className="grn-btn grn-btn-primary" onClick={() => navigate("/workshop/inventory/goods-receipt/new")}>
          + Nhập kho từ PO
        </button>
      </div>

      <div className="grn-board">
        {listError && <div className="grn-alert err">{listError}</div>}
        {loadingList ? <div className="grn-loading">Đang tải...</div> : 
         grns.length === 0 ? <div className="grn-empty">Chưa có dữ liệu phiếu nhập.</div> : (
           <table className="grn-table">
             <thead>
               <tr>
                 <th>Mã Phiếu Nhập</th>
                 <th>Ngày Nhập</th>
                 <th>Mã PO</th>
                 <th>Người Nhận</th>
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
        {!loadingList && (
          <div className="grn-pagination flex items-center justify-between mt-4 text-sm text-slate-600">
            <div>Tổng số: <strong>{total}</strong> phiếu nhập</div>
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
                  className="grn-btn grn-btn-ghost text-xs py-1 px-3" 
                  disabled={page <= 1} 
                  onClick={() => setPage(value => value - 1)}
                >
                  {PAGE_LABELS.previous}
                </button>
                <span> {PAGE_LABELS.page} {page} / {Math.max(1, Math.ceil(total / pageSize))} </span>
                <button 
                  type="button" 
                  className="grn-btn grn-btn-ghost text-xs py-1 px-3" 
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
