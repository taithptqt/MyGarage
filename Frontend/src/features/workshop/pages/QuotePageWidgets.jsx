import React from "react";
import { AlertTriangle, Check, FileText, ShieldCheck, User } from "lucide-react";

export function QuoteStepper({ status, requiresInsurance = true }) {
  const steps = requiresInsurance ? [
    { id: "DRAFT", label: "Bản nháp", icon: <FileText size={18} /> },
    { id: "PENDING_INSURANCE", label: "Chờ BH duyệt", icon: <ShieldCheck size={18} /> },
    { id: "INSURANCE_APPROVED", label: "BH đã duyệt", icon: <Check size={18} /> },
    { id: "PENDING_CUSTOMER", label: "Đợi khách chốt", icon: <User size={18} /> },
    { id: "CUSTOMER_APPROVED", label: "Hoàn tất", icon: <Check size={18} /> },
  ] : [
    { id: "DRAFT", label: "Bản nháp", icon: <FileText size={18} /> },
    { id: "INSURANCE_APPROVED", label: "Đã duyệt nội bộ", icon: <Check size={18} /> },
    { id: "PENDING_CUSTOMER", label: "Đợi khách chốt", icon: <User size={18} /> },
    { id: "CUSTOMER_APPROVED", label: "Hoàn tất", icon: <Check size={18} /> },
  ];

  const currentIdx = steps.findIndex(s => s.id === status);

  return (
    <div className="quote-stepper">
      {steps.map((step, idx) => {
        let state = "pending";
        if (idx < currentIdx) state = "completed";
        else if (idx === currentIdx) state = "active";
        if (status === "CUSTOMER_APPROVED") state = "completed";

        return (
          <div key={step.id} className={`step-item ${state}`}>
            <div className="step-icon">{state === "completed" ? <Check size={18} /> : step.icon}</div>
            <div className="step-label">{step.label}</div>
          </div>
        );
      })}
    </div>
  );
}


export function QuoteInfoCard({ icon, label, value, subValue }) {
  return (
    <div className="info-card-premium">
      <div className="info-card-icon">{icon}</div>
      <div className="info-card-content">
        <span>{label}</span>
        <strong>{value}</strong>
        {subValue && <small className="info-card-sub">{subValue}</small>}
      </div>
    </div>
  );
}


export function QuoteCancelModal({
  showCancelModal,
  cancelReason,
  setCancelReason,
  onClose,
  onConfirm,
}) {
  return (
    <>
      {showCancelModal && (
        <div className="modal-overlay">
          <div className="modal-box" style={{ maxWidth: 450 }}>
            <div className="modal-header">
              <h2 style={{ display: "flex", alignItems: "center", gap: 10, margin: 0, fontSize: 18 }}>
                <AlertTriangle color="var(--quota-danger)" size={20} /> Xác nhận hủy báo giá
              </h2>
            </div>
            <div className="modal-body-content" style={{ padding: 20 }}>
              <p style={{ margin: 0, color: "#475569" }}>Bạn có chắc chắn muốn hủy toàn bộ các hạng mục trong lệnh sửa chữa này không? Hành động này không thể hoàn tác.</p>

              <div style={{ marginTop: 15 }}>
                <label className="so-detail-card-label" style={{ fontWeight: 800 }}>Lý do hủy hồ sơ</label>
                <select
                  className="reception-quote-approval-note-input"
                  style={{ marginTop: 5, width: "100%", height: 40 }}
                  value={cancelReason}
                  onChange={(e) => setCancelReason(e.target.value)}
                >
                  <option value="Khách vội">Khách vội</option>
                  <option value="Chờ đợi">Chờ đợi</option>
                  <option value="Đổi ý">Đổi ý</option>
                  <option value="Có việc gấp">Có việc gấp</option>
                  <option value="Khác">Lý do khác...</option>
                </select>
              </div>

              <div style={{ display: "flex", gap: 10, marginTop: 24, justifyContent: "flex-end" }}>
                <button type="button" className="btn-quota btn-quota-outline" onClick={() => onClose()}>Hủy bỏ</button>
                <button type="button" className="btn-quota btn-quota-primary" style={{ background: "var(--quota-danger)", color: "white" }} onClick={onConfirm}>Xác nhận hủy</button>
              </div>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
