import React from "react";

export function Modal({ title, onClose, children, className = "" }) {
  const handleBackdropClick = (e) => {
    if (e.target.classList.contains("modal-backdrop")) {
      onClose();
    }
  };

  return (
    <div className="modal-backdrop" onMouseDown={handleBackdropClick}>
      <div className={`modal-box ${className}`.trim()}>
        <div className="modal-header">
          <h2>{title}</h2>
          <button className="modal-close" onClick={onClose}>&times;</button>
        </div>
        <div className="modal-body-content">
          {children}
        </div>
      </div>
    </div>
  );
}

export function ConfirmDialog({ message, onConfirm, onCancel, loading }) {
  return (
    <div className="confirm-msg">
      <p>{message}</p>
      <div className="form-actions">
        <button className="btn-ghost" onClick={onCancel} disabled={loading}>
          Cancel
        </button>
        <button className="btn-danger" onClick={onConfirm} disabled={loading}>
          {loading ? "Deleting…" : "Delete"}
        </button>
      </div>
    </div>
  );
}
