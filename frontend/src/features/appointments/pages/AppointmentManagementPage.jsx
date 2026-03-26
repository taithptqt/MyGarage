import { useState, useEffect, useCallback } from "react";
import { useBranch } from "../../../context/BranchContext";
import { useSearch } from "../../../context/SearchContext";
import { Modal } from "../../admin/components/AdminModals";
import {
  getBranchAppointments,
  confirmAppointment,
  cancelAppointment,
  checkInAppointment,
} from "../api/appointmentApi";
import TablePagination from "../../../shared/components/TablePagination";

const STATUS_LABELS = {
  PENDING: "Chờ xác nhận",
  CONFIRMED: "Đã xác nhận",
  ARRIVED: "Đã đến",
  CANCELED: "Đã hủy",
  NO_SHOW: "Không đến",
};

const STATUS_FILTERS = [
  { key: "", label: "Tất cả" },
  { key: "PENDING", label: "Chờ xác nhận" },
  { key: "CONFIRMED", label: "Đã xác nhận" },
  { key: "ARRIVED", label: "Đã đến" },
  { key: "CANCELED", label: "Đã hủy" },
];

function formatTime(t) {
  if (!t) return "";
  return t.substring(0, 5);
}

function badgeClass(status) {
  const map = {
    PENDING: "badge-warning",
    CONFIRMED: "badge-active",
    ARRIVED: "badge-info",
    CANCELED: "badge-inactive",
    NO_SHOW: "badge-inactive",
  };
  return map[status] || "";
}

export default function AppointmentManagementPage() {
  const { selectedBranchId } = useBranch();
  const { searchQuery } = useSearch();

  const [appointments, setAppointments] = useState([]);
  const [loading, setLoading] = useState(true);
  const [statusFilter, setStatusFilter] = useState("");
  const [acting, setActing] = useState(null);
  const [error, setError] = useState("");
  const [detailModal, setDetailModal] = useState(null);
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const fetchData = useCallback(async () => {
    if (!selectedBranchId) return;
    setLoading(true);
    setError("");
    try {
      const data = await getBranchAppointments(selectedBranchId, statusFilter || undefined);
      setAppointments(Array.isArray(data) ? data : []);
    } catch {
      setError("Không thể tải danh sách lịch hẹn.");
    } finally {
      setLoading(false);
    }
  }, [selectedBranchId, statusFilter]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  async function handleAction(id, action) {
    setActing(id);
    setError("");
    try {
      if (action === "confirm") await confirmAppointment(id);
      else if (action === "cancel") await cancelAppointment(id, "Nhân viên hủy");
      else if (action === "checkin") await checkInAppointment(id);
      await fetchData();
    } catch (err) {
      setError(err.message || "Thao tác thất bại.");
    } finally {
      setActing(null);
    }
  }

  const query = String(searchQuery || "").trim().toLowerCase();
  const filtered = appointments.filter((a) => {
    if (!query) return true;
    return (
      (a.customerName || "").toLowerCase().includes(query) ||
      (a.customerPhone || "").toLowerCase().includes(query) ||
      (a.vehicleLicensePlate || "").toLowerCase().includes(query)
    );
  });
  const pagedData = filtered.slice((currentPage - 1) * pageSize, currentPage * pageSize);

  useEffect(() => {
    setCurrentPage(1);
  }, [searchQuery, statusFilter, appointments.length]);

  return (
    <div className="management-content">
      <header className="admin-header">
        <div>
          <h1>Quản lý lịch hẹn</h1>
          <p className="header-sub">
            Xác nhận, hủy hoặc check-in lịch hẹn của khách hàng
          </p>
        </div>
      </header>

      {error && <div className="error" style={{ marginBottom: 12 }}>{error}</div>}

      <div className="table-toolbar">
        <div style={{ display: "flex", gap: 6 }}>
          {STATUS_FILTERS.map((f) => (
            <button
              key={f.key}
              className={`btn-sm ${statusFilter === f.key ? "btn-primary" : "btn-outline"}`}
              onClick={() => setStatusFilter(f.key)}
              style={{
                padding: "4px 12px",
                borderRadius: 16,
                fontSize: 12,
                border: statusFilter === f.key ? "none" : "1px solid var(--mgmt-btn-border)",
                background: statusFilter === f.key ? "#422afb" : "transparent",
                color: statusFilter === f.key ? "#fff" : "var(--mgmt-text-muted)",
                cursor: "pointer",
              }}
            >
              {f.label}
            </button>
          ))}
        </div>
        <span className="result-count">{filtered.length} lịch hẹn</span>
      </div>

      <div className="table-wrapper">
        {loading ? (
          <div className="loading-state">
            <div className="spinner" />
            <p>Đang tải...</p>
          </div>
        ) : filtered.length === 0 ? (
          <div className="loading-state">
            <p>Không có lịch hẹn nào.</p>
          </div>
        ) : (
          <table className="admin-table">
            <thead>
              <tr>
                <th>Khách hàng</th>
                <th>SĐT</th>
                <th>Ngày</th>
                <th>Giờ</th>
                <th>Xe</th>
                <th className="th-center">Trạng thái</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {pagedData.map((a) => (
                <tr key={a.id}>
                  <td className="td-name">{a.customerName}</td>
                  <td>{a.customerPhone || "—"}</td>
                  <td>{a.scheduledDate}</td>
                  <td>
                    {formatTime(a.startTime)} - {formatTime(a.endTime)}
                  </td>
                  <td>
                    {a.vehicleLicensePlate || "—"}
                    {a.vehicleModelName && (
                      <span style={{ fontSize: 11, color: "var(--mgmt-text-muted)", marginLeft: 4 }}>
                        ({a.vehicleModelName})
                      </span>
                    )}
                  </td>
                  <td className="td-center">
                    <span className={`badge ${badgeClass(a.status)}`}>
                      {STATUS_LABELS[a.status] || a.status}
                    </span>
                  </td>
                  <td>
                    <div className="row-actions">
                      <button
                        className="btn-icon"
                        title="Xem chi tiết"
                        onClick={() => setDetailModal(a)}
                      >
                        👁️
                      </button>
                      {a.status === "PENDING" && (
                        <>
                          <button
                            className="btn-icon"
                            title="Xác nhận"
                            disabled={acting === a.id}
                            onClick={() => handleAction(a.id, "confirm")}
                          >
                            ✅
                          </button>
                          <button
                            className="btn-icon"
                            title="Hủy"
                            disabled={acting === a.id}
                            onClick={() => handleAction(a.id, "cancel")}
                          >
                            ❌
                          </button>
                        </>
                      )}
                      {a.status === "CONFIRMED" && (
                        <>
                          <button
                            className="btn-icon"
                            title="Check-in"
                            disabled={acting === a.id}
                            onClick={() => handleAction(a.id, "checkin")}
                          >
                            📋
                          </button>
                          <button
                            className="btn-icon"
                            title="Hủy"
                            disabled={acting === a.id}
                            onClick={() => handleAction(a.id, "cancel")}
                          >
                            ❌
                          </button>
                        </>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
      <TablePagination
        totalItems={filtered.length}
        currentPage={currentPage}
        pageSize={pageSize}
        onPageChange={setCurrentPage}
        onPageSizeChange={(size) => {
          setPageSize(size);
          setCurrentPage(1);
        }}
      />

      {detailModal && (
        <Modal
          title={`Chi tiết lịch hẹn #${detailModal.id}`}
          onClose={() => setDetailModal(null)}
        >
          <div style={{ display: "grid", gridTemplateColumns: "140px 1fr", gap: "10px 16px", fontSize: 14 }}>
            <strong>Trạng thái</strong>
            <span className={`badge ${badgeClass(detailModal.status)}`}>
              {STATUS_LABELS[detailModal.status] || detailModal.status}
            </span>
            <strong>Khách hàng</strong>
            <span>{detailModal.customerName}</span>
            <strong>SĐT</strong>
            <span>{detailModal.customerPhone || "—"}</span>
            <strong>Ngày</strong>
            <span>{detailModal.scheduledDate}</span>
            <strong>Giờ</strong>
            <span>
              {formatTime(detailModal.startTime)} -{" "}
              {formatTime(detailModal.endTime)}
            </span>
            <strong>Xe</strong>
            <span>
              {detailModal.vehicleLicensePlate || "Không có"}{" "}
              {detailModal.vehicleModelName && `(${detailModal.vehicleModelName})`}
            </span>
            <strong>Dịch vụ</strong>
            <span>
              {detailModal.serviceNames?.length > 0
                ? detailModal.serviceNames.join(", ")
                : "Không chọn dịch vụ"}
            </span>
            <strong>Ghi chú</strong>
            <span>{detailModal.customerNotes || "—"}</span>
            {detailModal.cancelReason && (
              <>
                <strong>Lý do hủy</strong>
                <span style={{ color: "#e53e3e" }}>{detailModal.cancelReason}</span>
              </>
            )}
            <strong>Ngày tạo</strong>
            <span>
              {detailModal.createdAt
                ? new Date(detailModal.createdAt).toLocaleString("vi-VN")
                : "—"}
            </span>
          </div>
        </Modal>
      )}
    </div>
  );
}
