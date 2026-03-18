export default function ConfirmDialog({ message, onConfirm, onCancel, loading }) {
  return (
    <div className="modal-overlay">
      <div className="modal confirm-modal">
        <div className="modal-body">
          <div className="confirm-icon">⚠️</div>
          <h3>Xác nhận xóa</h3>
          <p>{message || "Bạn có chắc muốn xóa mục này không? Hành động này không thể hoàn tác."}</p>
        </div>
        <div className="modal-footer">
          <button className="btn btn-secondary" onClick={onCancel} disabled={loading}>
            Hủy
          </button>
          <button className="btn btn-danger" onClick={onConfirm} disabled={loading}>
            {loading ? "Đang xóa..." : "Xóa"}
          </button>
        </div>
      </div>
    </div>
  );
}
