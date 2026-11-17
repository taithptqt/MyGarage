import React, { useState, useEffect, useMemo, useCallback } from "react";
import { request } from "@app/apiClient";
import { useAuth } from "@shared/core/context/AuthContext";
import "./MaterialReturn.css";

function fmtCurrency(v) { return Number(v ?? 0).toLocaleString("vi-VN") + " ₫"; }

const APPROVAL_THRESHOLD = 5000000; // 5 triệu VND (ví dụ)

export default function MaterialReturnPage() {
  const { user } = useAuth();
  const [activeTab, setActiveTab] = useState("NEW"); // NEW, HISTORY
  
  // Form state
  const [serviceOrders, setServiceOrders] = useState([]);
  const [requestDetails, setRequestDetails] = useState([]);
  const [selectedSo, setSelectedSo] = useState("");
  
  const [form, setForm] = useState({
    materialRequestDetailId: "",
    returnedQuantity: 1,
    itemCondition: "NEW", // NEW, USED, DAMAGED
    reason: "",
    notes: ""
  });

  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [successMsg, setSuccessMsg] = useState("");
  const [history, setHistory] = useState([]);
  const [loadingHistory, setLoadingHistory] = useState(false);

  // Fetch active SOs
  useEffect(() => {
    request({ url: "/api/v1/service-orders", method: "GET", params: { status: "IN_PROGRESS,READY_FOR_QC" } })
      .then(res => setServiceOrders(res?.items ?? res ?? []))
      .catch(console.error);
  }, []);

  // Fetch issued items when SO selected
  useEffect(() => {
    if (!selectedSo) {
      setRequestDetails([]);
      setForm(f => ({ ...f, materialRequestDetailId: "" }));
      return;
    }
    // Lấy các chi tiết yêu cầu vật tư đã xuất (IssuedQuantity > 0)
    request({ url: "/api/v1/material-request-details", method: "GET", params: { serviceOrderId: selectedSo } })
      .then(res => {
        const raw = res?.items ?? res ?? [];
        setRequestDetails(raw);
      })
      .catch(console.error);
  }, [selectedSo]);

  const loadHistory = useCallback(() => {
    setLoadingHistory(true);
    request({ url: "/api/v1/material-returns", method: "GET" })
      .then(res => setHistory(res?.items ?? res ?? []))
      .catch(console.error)
      .finally(() => setLoadingHistory(false));
  }, []);

  useEffect(() => {
    if (activeTab === "HISTORY") loadHistory();
  }, [activeTab, loadHistory]);

  const selectedItem = useMemo(() => 
    requestDetails.find(d => String(d.id) === String(form.materialRequestDetailId)), 
    [form.materialRequestDetailId, requestDetails]
  );
  
  const needsApproval = useMemo(() => {
    if (!selectedItem) return false;
    // Giả định đơn giá lấy từ Product hoặc thông tin đi kèm (Trong thực tế cần giá nhập/xuất chính xác)
    // Ở đây ta dùng 0 nếu không có thông tin giá trực tiếp trong MR Detail
    const unitPrice = selectedItem.unitPrice ?? 0; 
    const val = unitPrice * form.returnedQuantity;
    return val >= APPROVAL_THRESHOLD;
  }, [selectedItem, form.returnedQuantity]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!selectedSo || !form.materialRequestDetailId || form.returnedQuantity <= 0) return;
    setSaving(true);
    setError("");
    setSuccessMsg("");

    try {
      await request({
        url: "/api/v1/material-returns",
        method: "POST",
        data: {
          serviceOrderId: selectedSo,
          materialRequestDetailId: form.materialRequestDetailId,
          productId: selectedItem.productId,
          returnedQuantity: form.returnedQuantity,
          itemCondition: form.itemCondition,
          reason: form.reason,
          notes: form.notes,
          returnedByStaffId: user?.id,
          returnTime: new Date().toISOString()
        }
      });
      setSuccessMsg("Đã thực hiện hoàn trả vật tư và cập nhật kho thành công.");
      setForm({
        materialRequestDetailId: "",
        returnedQuantity: 1,
        itemCondition: "NEW",
        reason: "",
        notes: ""
      });
      // Refresh list
      const updatedDetails = await request({ url: "/api/v1/material-request-details", method: "GET", params: { serviceOrderId: selectedSo } });
      setRequestDetails(updatedDetails?.items ?? updatedDetails ?? []);
    } catch (err) {
      setError(err?.response?.data?.message || "Lỗi khi xử lý hoàn trả.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="mret-page">
      <div className="mret-header">
        <div>
          <h1 className="mret-title">♻️ Hoàn trả Vật tư / Phụ tùng</h1>
          <p className="mret-subtitle">Nhập lại kho các vật tư đã xuất nhưng không sử dụng hết hoặc cần thay thế.</p>
        </div>
        <div className="mret-tabs">
          <button className={`mret-tab ${activeTab === "NEW" ? "active" : ""}`} onClick={() => setActiveTab("NEW")}>Tạo phiếu mới</button>
          <button className={`mret-tab ${activeTab === "HISTORY" ? "active" : ""}`} onClick={() => setActiveTab("HISTORY")}>Lịch sử hoàn trả</button>
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
              <label>Vật tư đã xuất</label>
              <select 
                value={form.materialRequestDetailId} 
                onChange={e => setForm(f => ({ ...f, materialRequestDetailId: e.target.value }))} 
                required 
                disabled={!selectedSo}
              >
                <option value="">-- Chọn vật tư cần trả --</option>
                {requestDetails.map(d => (
                  <option key={d.id} value={d.id}>
                    {d.productName} (Đã xuất: {d.issuedQuantity} - Đã trả: {d.returnedQuantity || 0})
                  </option>
                ))}
              </select>
            </div>
            
            <div className="mret-form-group">
              <label>Số lượng trả lại</label>
              <input 
                type="number" 
                min="0.01" 
                step="0.01" 
                max={selectedItem ? (selectedItem.issuedQuantity - (selectedItem.returnedQuantity || 0)) : 999}
                value={form.returnedQuantity} 
                onChange={e => setForm(f => ({ ...f, returnedQuantity: Number(e.target.value) }))} 
                required 
              />
            </div>

            <div className="mret-form-group">
              <label>Tình trạng hàng</label>
              <select value={form.itemCondition} onChange={e => setForm(f => ({ ...f, itemCondition: e.target.value }))}>
                <option value="NEW">Mới 100% (nguyên bao bì)</option>
                <option value="USED">Đã tháo lắp (còn sử dụng được)</option>
                <option value="DAMAGED">Hỏng / Lỗi kỹ thuật</option>
              </select>
            </div>
          </div>

          <div className="mret-form-group">
            <label>Lý do hoàn trả</label>
            <textarea 
              placeholder="Ví dụ: Khách hàng đổi ý, Chẩn đoán lại không cần thay, Phụ tùng không tương thích..." 
              value={form.reason} 
              onChange={e => setForm(f => ({ ...f, reason: e.target.value }))} 
              required 
            />
          </div>

          <div className="mret-summary">
            <div className="mret-summary-info">
              {selectedItem && (
                <>
                  <span>Vật tư: <strong>{selectedItem.productName}</strong></span>
                  <span>SKU: <strong>{selectedItem.productSku || "--"}</strong></span>
                </>
              )}
            </div>
          </div>

          <div className="mret-footer">
            <button type="submit" className="mret-btn mret-btn-primary" disabled={saving || !form.materialRequestDetailId}>
               {saving ? "⏳ Đang xử lý..." : "Xác nhận hoàn trả"}
            </button>
          </div>
        </form>
      ) : (
        <div className="mret-history-board">
          {loadingHistory ? (
            <div className="p-4 text-center text-slate-500">Đang tải...</div>
          ) : history.length === 0 ? (
            <div className="p-4 text-center text-slate-500">Không có lịch sử hoàn trả.</div>
          ) : (
             <div className="mret-table-wrap">
               <table className="mret-table">
                 <thead>
                   <tr>
                     <th>Mã phiếu</th>
                     <th>Sản phẩm</th>
                     <th>Số lượng</th>
                     <th>Tình trạng</th>
                     <th>Trạng thái</th>
                     <th>Thời gian</th>
                   </tr>
                 </thead>
                 <tbody>
                   {history.map(h => (
                     <tr key={h.id}>
                       <td>#{h.id}</td>
                       <td className="font-semibold">{h.productName || h.productId}</td>
                       <td>{h.returnedQuantity}</td>
                       <td>{h.itemCondition}</td>
                       <td>
                         <span className={`mret-status-badge st-${(h.status||"COMPLETED").toLowerCase()}`}>
                           {h.status || "Hoàn tất"}
                         </span>
                       </td>
                       <td>{new Date(h.returnTime || h.createdAt).toLocaleString("vi-VN")}</td>
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


