import { useState, useEffect, useCallback } from "react";
import { useSearch } from "@context/SearchContext";
import { Modal } from "../../admin/components/AdminModals";
import {
  getAllAppointments,
  getBranchAppointments,
  getAllBranches,
  confirmAppointment,
  cancelAppointment,
  checkInAppointment,
} from "../api/appointmentApi";

const STATUS_LABELS = {
  PENDING: "Cho xac nhan",
  CONFIRMED: "Da xac nhan",
  ARRIVED: "Da den",
  CANCELED: "Da huy",
  NO_SHOW: "Khong den",
};

const STATUS_ORDER = { PENDING: 0, CONFIRMED: 1, ARRIVED: 2, NO_SHOW: 3, CANCELED: 4 };

const STATUS_FILTERS = [
  { key: "", label: "Tat ca" },
  { key: "PENDING", label: "Cho xac nhan" },
  { key: "CONFIRMED", label: "Da xac nhan" },
  { key: "ARRIVED", label: "Da den" },
  { key: "CANCELED", label: "Da huy" },
  { key: "NO_SHOW", label: "Khong den" },
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
  const { searchQuery } = useSearch();

  const [branches, setBranches] = useState([]);
  const [selectedBranch, setSelectedBranch] = useState("");
  const [appointments, setAppointments] = useState([]);
  const [loading, setLoading] = useState(true);
  const [statusFilter, setStatusFilter] = useState("");
  const [acting, setActing] = useState(null);
  const [error, setError] = useState("");
  const [detailModal, setDetailModal] = useState(null);

  useEffect(() => {
    getAllBranches().then((data) => {
      setBranches(Array.isArray(data) ? data : []);
    }).catch(() => {});
  }, []);

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      let data;
      if (selectedBranch) {
        data = await getBranchAppointments(selectedBranch, statusFilter || undefined);
      } else {
        data = await getAllAppointments(statusFilter || undefined);
      }
      setAppointments(Array.isArray(data) ? data : []);
    } catch {
      setError("Khong the tai danh sach lich hen.");
    } finally {
      setLoading(false);
    }
  }, [selectedBranch, statusFilter]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  async function handleAction(id, action) {
    setActing(id);
    setError("");
    try {
      if (action === "confirm") await confirmAppointment(id);
      else if (action === "cancel") await cancelAppointment(id, "Nhan vien huy");
      else if (action === "checkin") await checkInAppointment(id);
      await fetchData();
    } catch (err) {
      setError(err.message || "Thao tac that bai.");
    } finally {
      setActing(null);
    }
  }

  const query = String(searchQuery || "").trim().toLowerCase();
  const filtered = appointments
    .filter((a) => {
      if (!query) return true;
      return (
        (a.customerName || "").toLowerCase().includes(query) ||
        (a.customerPhone || "").toLowerCase().includes(query) ||
        (a.vehicleLicensePlate || "").toLowerCase().includes(query)
      );
    })
    .slice()
    .sort((a, b) => (STATUS_ORDER[a.status] ?? 99) - (STATUS_ORDER[b.status] ?? 99));

  return (
    <div className="management-content">
      <header className="admin-header">
        <div>
          <h1>Quan ly lich hen</h1>
          <p className="header-sub">
            Xac nhan, huy hoac check-in lich hen cua khach hang
          </p>
        </div>
      </header>

      {error && <div className="error" style={{ marginBottom: 12 }}>{error}</div>}

      <div className="table-toolbar">
        <div style={{ display: "flex", gap: 12, alignItems: "center", flexWrap: "wrap" }}>
          <select
            value={selectedBranch}
            onChange={(e) => setSelectedBranch(e.target.value)}
            style={{
              padding: "6px 12px",
              borderRadius: 8,
              border: "1px solid var(--mgmt-btn-border, #e2e8f0)",
              fontSize: 13,
              background: "#fff",
              cursor: "pointer",
              minWidth: 160,
            }}
          >
            <option value="">Tat ca chi nhanh</option>
            {branches.map((b) => (
              <option key={b.id} value={b.id}>{b.name}</option>
            ))}
          </select>

          <div style={{ display: "flex", gap: 6 }}>
            {STATUS_FILTERS.map((f) => (
              <button
                key={f.key}
                onClick={() => setStatusFilter(f.key)}
                style={{
                  padding: "4px 12px",
                  borderRadius: 16,
                  fontSize: 12,
                  border: statusFilter === f.key ? "none" : "1px solid var(--mgmt-btn-border, #e2e8f0)",
                  background: statusFilter === f.key ? "#422afb" : "transparent",
                  color: statusFilter === f.key ? "#fff" : "var(--mgmt-text-muted, #718096)",
                  cursor: "pointer",
                }}
              >
                {f.label}
              </button>
            ))}
          </div>
        </div>
        <span className="result-count">{filtered.length} lich hen</span>
      </div>

      <div className="table-wrapper">
        {loading ? (
          <div className="loading-state">
            <div className="spinner" />
            <p>Dang tai...</p>
          </div>
        ) : filtered.length === 0 ? (
          <div className="loading-state">
            <p>Khong co lich hen nao.</p>
          </div>
        ) : (
          <table className="admin-table">
            <thead>
              <tr>
                <th>Khach hang</th>
                <th>SDT</th>
                {!selectedBranch && <th>Chi nhanh</th>}
                <th>Ngay</th>
                <th>Gio</th>
                <th>Xe</th>
                <th className="th-center">Trang thai</th>
                <th>Thao tac</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((a) => (
                <tr key={a.id}>
                  <td className="td-name">{a.customerName}</td>
                  <td>{a.customerPhone || "—"}</td>
                  {!selectedBranch && <td>{a.branchName || "—"}</td>}
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
                        title="Xem chi tiet"
                        onClick={() => setDetailModal(a)}
                      >
                        👁️
                      </button>
                      {a.status === "PENDING" && (
                        <>
                          <button
                            className="btn-icon"
                            title="Xac nhan"
                            disabled={acting === a.id}
                            onClick={() => handleAction(a.id, "confirm")}
                          >
                            ✅
                          </button>
                          <button
                            className="btn-icon"
                            title="Huy"
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
                            title="Huy"
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

      {detailModal && (
        <Modal
          title={`Chi tiet lich hen #${detailModal.id}`}
          onClose={() => setDetailModal(null)}
        >
          <div style={{ display: "grid", gridTemplateColumns: "140px 1fr", gap: "10px 16px", fontSize: 14 }}>
            <strong>Trang thai</strong>
            <span className={`badge ${badgeClass(detailModal.status)}`}>
              {STATUS_LABELS[detailModal.status] || detailModal.status}
            </span>
            <strong>Khach hang</strong>
            <span>{detailModal.customerName}</span>
            <strong>SDT</strong>
            <span>{detailModal.customerPhone || "—"}</span>
            <strong>Chi nhanh</strong>
            <span>{detailModal.branchName || "—"}</span>
            <strong>Ngay</strong>
            <span>{detailModal.scheduledDate}</span>
            <strong>Gio</strong>
            <span>
              {formatTime(detailModal.startTime)} -{" "}
              {formatTime(detailModal.endTime)}
            </span>
            <strong>Xe</strong>
            <span>
              {detailModal.vehicleLicensePlate || "Khong co"}{" "}
              {detailModal.vehicleModelName && `(${detailModal.vehicleModelName})`}
            </span>
            <strong>Dich vu</strong>
            <span>
              {detailModal.serviceNames?.length > 0
                ? detailModal.serviceNames.join(", ")
                : "Khong chon dich vu"}
            </span>
            <strong>Ghi chu</strong>
            <span>{detailModal.customerNotes || "—"}</span>
            {detailModal.cancelReason && (
              <>
                <strong>Ly do huy</strong>
                <span style={{ color: "#e53e3e" }}>{detailModal.cancelReason}</span>
              </>
            )}
            <strong>Ngay tao</strong>
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
