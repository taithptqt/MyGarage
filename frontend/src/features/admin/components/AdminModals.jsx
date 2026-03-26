import React from "react";

export function Modal({ title, onClose, children, className = "" }) {
  const handleBackdropClick = (e) => {
    if (e.target.classList.contains("modal-backdrop")) {
      onClose();
    }
  };

  return (
    <div className="modal-backdrop luxury-backdrop" onMouseDown={handleBackdropClick}>
      <div className={`modal-box premium-modal ${className}`}>
        <div className="modal-header">
          <h2>{title}</h2>
          <button className="modal-close-btn" onClick={onClose} aria-label="Close">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>
          </button>
        </div>
        <div className="modal-body-content">
          {children}
        </div>
      </div>
    </div>
  );
}

import { MdReportProblem } from "react-icons/md";

export function ConfirmDialog({ message, onConfirm, onCancel, loading }) {
  return (
    <div className="confirm-dialog-content">
      <div className="confirm-icon-danger">
        <MdReportProblem />
      </div>
      <p className="confirm-text">{message}</p>
      <div className="form-actions" style={{ width: '100%', justifyContent: 'center', gap: '16px' }}>
        <button className="btn-ghost" onClick={onCancel} disabled={loading} style={{ minWidth: '120px' }}>
          Hủy bỏ
        </button>
        <button className="btn-danger" onClick={onConfirm} disabled={loading} style={{ minWidth: '120px' }}>
          {loading ? "Đang xử lý..." : "Xác nhận xóa"}
        </button>
      </div>
    </div>
  );
}
