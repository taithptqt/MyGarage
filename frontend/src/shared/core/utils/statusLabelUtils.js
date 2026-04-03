const STATUS_LABELS_VI = Object.freeze({
  ALL: "Tất cả",
  PENDING: "Chờ xác nhận",
  LATE: "Trễ hẹn",
  QUEUED: "Xếp hàng",
  CONFIRMED: "Đã xác nhận",
  PARTIAL_CONFIRMED: "Xác nhận một phần",
  ARRIVED: "Đã đến",
  ARRIVED_LATE: "Đến trễ",
  CHECKED_IN: "Đã check-in",
  IN_PROGRESS: "Đang xử lý",
  WAITING_APPROVAL: "Chờ báo giá",
  READY: "Sẵn sàng bàn giao",
  COMPLETED: "Hoàn tất",
  CANCELED: "Đã hủy",
  CANCELLED: "Đã hủy",
  NO_SHOW: "Không đến",
  NEW: "Mới tạo",
  RECEPTION: "Tiếp nhận",
  APPROVED: "Đã duyệt",
  REJECTED: "Từ chối",
  DRAFT: "Nháp",
  PAID: "Đã thanh toán",
  LOCKED: "Đã khóa",
});

function normalizeStatus(status) {
  return String(status || "").trim().toUpperCase();
}

export function getStatusLabelVi(status, fallback = "-") {
  const normalized = normalizeStatus(status);
  if (!normalized) return fallback;
  return STATUS_LABELS_VI[normalized] || String(status);
}

export function toStatusOptionVi(status) {
  const value = String(status || "").trim();
  return {
    value,
    label: getStatusLabelVi(value, value || "-"),
  };
}

