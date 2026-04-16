import React, { useState, useEffect, useMemo, useCallback } from "react";
import { request } from "@app/apiClient";
import { useAuth } from "@shared/core/context/AuthContext";
import "./MaterialReturn.css";

function fmtCurrency(v) { return Number(v ?? 0).toLocaleString("vi-VN") + " ₫"; }
function fmtNum(v) { return Number(v ?? 0).toLocaleString("vi-VN"); }

const APPROVAL_THRESHOLD = 5000000; // 5 triệu VND (ví dụ)

export default function MaterialReturnPage() {
  const { user } = useAuth();
  const [activeTab, setActiveTab] = useState("NEW"); // NEW, HISTORY
  
  // Form state
  const [serviceOrders, setServiceOrders] = useState([]);
  const [parts, setParts] = useState([]);
  const [selectedSo, setSelectedSo] = useState("");
  
  const [form, setForm] = useState({
    productId: "",
    qty: 1,
    condition: "NEW", // NEW, USED, DAMAGED
    reason: ""
  });

  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [successMsg, setSuccessMsg] = useState("");
  const [history, setHistory] = useState([]);
  const [loadingHistory, setLoadingHistory] = useState(false);

  // Fetch active SOs
  useEffect(() => {
    request({ url: "/api/service-orders", method: "GET", params: { status: "IN_PROGRESS,READY_FOR_QC" } })
      .then(res => setServiceOrders(res?.items ?? res ?? []))
      .catch(console.error);
  }, []);

  // Fetch parts when SO selected
  useEffect(() => {
    if (!selectedSo) {
      setParts([]);
      setForm(f => ({ ...f, productId: "" }));
      return;
    }
    request({ url: `/api/service-orders/${selectedSo}/items`, method: "GET" })
      .then(res => {
        const raw = res?.items ?? res ?? [];
        // Only high-value / non-consumable parts
        setParts(raw.filter(it => !it.isConsumable));
      })
      .catch(console.error);
  }, [selectedSo]);

  const loadHistory = useCallback(() => {
    setLoadingHistory(true);
    request({ url: "/api/material-returns", method: "GET", params: { branchId: user?.branchId } })
      .then(res => setHistory(res?.items ?? res ?? []))
      .catch(console.error)
      .finally(() => setLoadingHistory(false));
  }, [user]);

  useEffect(() => {
    if (activeTab === "HISTORY") loadHistory();
  }, [activeTab, loadHistory]);

  const selectedPart = useMemo(() => parts.find(p => String(p.productId) === String(form.productId)), [form.productId, parts]);
  
  const needsApproval = useMemo(() => {
    if (!selectedPart) return false;
    const val = Number(selectedPart.unitCost ?? selectedPart.price ?? 0) * form.qty;
    return val >= APPROVAL_THRESHOLD;
  }, [selectedPart, form.qty]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!selectedSo || !form.productId || form.qty <= 0) return;
    setSaving(true);
    setError("");
    setSuccessMsg("");

    try {
      await request({
        url: "/api/material-returns",
        method: "POST",
        data: {
          serviceOrderId: selectedSo,
          productId: form.productId,
          returnedQty: form.qty,
          condition: form.condition,
          reason: form.reason,
          returnedBy: user?.id,
          needsApproval
        }
      });
      setSuccessMsg("Đã gửi yêu cầu trả phụ tùng.");
      setForm({ ...form, qty: 1, reason: "" });
    } catch (err) {
      setError(err?.response?.data?.message || "Lỗi khi gửi yêu cầu.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="mret-page">
      <div className="mret-header">
        <div>
          <h1 className="mret-title">♻️ Trả lại Phụ tùng (High-Value)</h1>
          <p className="mret-subtitle">Trả lại kho phụ tùng không dùng đến, bị lỗi, hoặc thừa.</p>
        </div>
        <div className="mret-tabs">
          <button className={`mret-tab ${activeTab === "NEW" ? "active" : ""}`} onClick={() => setActiveTab("NEW")}>Tạo phiếu mới</button>
          <button className={`mret-tab ${activeTab === "HISTORY" ? "active" : ""}`} onClick={() => setActiveTab("HISTORY")}>Lịch sử / Phê duyệt</button>
        </div>
      </div>

      {activeTab === "NEW" ? (
        <form className="mret-board" onSubmit={handleSubmit}>
          {error && <div className="mret-alert err">⚠️ {error}</div>}
          {successMsg && <div className="mret-alert succ">✅ {successMsg}</div>}

          <div className="mret-grid">
            <div className="mret-form-group">
              <label>Lệnh Sửa Chữa (SO)</label>
              <select value={selectedSo} onChange={e => setSelectedSo(e.target.value)} required>
                <option value="">-- Chọn lệnh đang thi công --</option>
                {serviceOrders.map(so => (
                  <option key={so.id} value={so.id}>{(so.code || so.id)} - {so.licensePlate || "N/A"}</option>
                ))}
              </select>
            </div>

            <div className="mret-form-group">
              <label>Phụ tùng trả lại</label>
              <select value={form.productId} onChange={e => setForm(f => ({ ...f, productId: e.target.value }))} required disabled={!selectedSo}>
                <option value="">-- Chọn phụ tùng --</option>
                {parts.map(p => (
                  <option key={p.productId} value={p.productId}>{p.productName ?? p.name}</option>
                ))}
              </select>
            </div>
            
            <div className="mret-form-group">
              <label>Số lượng</label>
              <input type="number" min="0.5" step="0.5" value={form.qty} onChange={e => setForm(f => ({ ...f, qty: Number(e.target.value) }))} required />
            </div>

            <div className="mret-form-group">
              <label>Tình trạng</label>
              <select value={form.condition} onChange={e => setForm(f => ({ ...f, condition: e.target.value }))}>
                <option value="NEW">Mới 100% (còn nguyên seal)</option>
                <option value="USED">Đã tháo lắp (không lỗi)</option>
                <option value="DAMAGED">Hỏng / Lỗi NSX</option>
              </select>
            </div>
          </div>

          <div className="mret-form-group">
            <label>Lý do trả</label>
            <textarea placeholder="Nhập lý do chi tiết..." value={form.reason} onChange={e => setForm(f => ({ ...f, reason: e.target.value }))} required />
          </div>

          <div className="mret-summary">
            <div className="mret-summary-info">
              <span>Đơn giá nhập: <strong>{selectedPart ? fmtCurrency(selectedPart.unitCost ?? selectedPart.price) : "--"}</strong></span>
              <span>Tổng giá trị trả: <strong>{selectedPart ? fmtCurrency((selectedPart.unitCost ?? selectedPart.price ?? 0) * form.qty) : "--"}</strong></span>
            </div>
            {needsApproval && (
              <div className="mret-approval-badge">
                ⚠️ Giá trị &gt; 5tr, cần Quản lý duyệt trước khi nhập kho.
              </div>
            )}
          </div>

          <div className="mret-footer">
            <button type="submit" className="mret-btn mret-btn-primary" disabled={saving || !form.productId}>
               {saving ? "⏳ Đang gửi..." : " Gửi phiếu trả"}
            </button>
          </div>
        </form>
      ) : (
        <div className="mret-history-board">
          {loadingHistory ? (
            <div className="p-4 text-center text-slate-500">Đang tải...</div>
          ) : history.length === 0 ? (
            <div className="p-4 text-center text-slate-500">Không có lịch sử trả phụ tùng.</div>
          ) : (
             <div className="mret-table-wrap">
               <table className="mret-table">
                 <thead>
                   <tr>
                     <th>Mã phiếu</th>
                     <th>Sản phẩm</th>
                     <th>SL</th>
                     <th>Tình trạng</th>
                     <th>Trạng thái</th>
                     <th>Ngày</th>
                   </tr>
                 </thead>
                 <tbody>
                   {history.map(h => (
                     <tr key={h.id}>
                       <td>#{h.id}</td>
                       <td className="font-semibold">{h.productName}</td>
                       <td>{h.returnedQty}</td>
                       <td>{h.condition}</td>
                       <td>
                         <span className={`mret-status-badge st-${(h.status||"PENDING").toLowerCase()}`}>
                           {h.status}
                         </span>
                       </td>
                       <td>{new Date(h.createdAt).toLocaleDateString("vi-VN")}</td>
                     </tr>
                   ))}
                 </tbody>
               </table>
             </div>
          )}
        </div>
      )}
    </div>
  );
}
