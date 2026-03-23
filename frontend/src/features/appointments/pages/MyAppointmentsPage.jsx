import { useState, useEffect } from "react";
import { useNavigate, Link } from "react-router-dom";
import { isAuthenticated } from "../../../app/authState";
import {
  getMyAppointments,
  cancelAppointment,
} from "../api/appointmentApi";

const STATUS_LABELS = {
  PENDING: "Cho xac nhan",
  CONFIRMED: "Da xac nhan",
  ARRIVED: "Da den",
  CANCELED: "Da huy",
  NO_SHOW: "Khong den",
};

const FILTERS = [
  { key: "", label: "Tat ca" },
  { key: "PENDING", label: "Cho xac nhan" },
  { key: "CONFIRMED", label: "Da xac nhan" },
  { key: "ARRIVED", label: "Da den" },
  { key: "CANCELED", label: "Da huy" },
];

function formatTime(t) {
  if (!t) return "";
  return t.substring(0, 5);
}

export default function MyAppointmentsPage() {
  const navigate = useNavigate();

  const [appointments, setAppointments] = useState([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState("");
  const [cancelling, setCancelling] = useState(null);
  const [error, setError] = useState("");
  const [cancelTarget, setCancelTarget] = useState(null);
  const [cancelReason, setCancelReason] = useState("");

  useEffect(() => {
    if (!isAuthenticated()) {
      navigate("/", { replace: true });
      return;
    }
    loadAppointments();
  }, []);

  async function loadAppointments() {
    setLoading(true);
    try {
      const data = await getMyAppointments();
      setAppointments(Array.isArray(data) ? data : []);
    } catch {
      setAppointments([]);
    } finally {
      setLoading(false);
    }
  }

  function openCancelPopup(appointment) {
    setCancelTarget(appointment);
    setCancelReason("");
    setError("");
  }

  function closeCancelPopup() {
    setCancelTarget(null);
    setCancelReason("");
  }

  async function handleConfirmCancel() {
    if (!cancelTarget) return;
    setCancelling(cancelTarget.id);
    setError("");
    try {
      await cancelAppointment(cancelTarget.id, cancelReason || "Khach hang tu huy");
      closeCancelPopup();
      await loadAppointments();
    } catch (err) {
      setError(err.message || "Huy that bai.");
    } finally {
      setCancelling(null);
    }
  }

  const STATUS_ORDER = { PENDING: 0, CONFIRMED: 1, ARRIVED: 2, NO_SHOW: 3, CANCELED: 4 };

  const filtered = (filter
    ? appointments.filter((a) => a.status === filter)
    : appointments
  ).slice().sort((a, b) => (STATUS_ORDER[a.status] ?? 99) - (STATUS_ORDER[b.status] ?? 99));

  if (loading) {
    return (
      <div>
        <h1 style={{ fontSize: 24, fontWeight: 700, marginBottom: 24 }}>Lich hen cua toi</h1>
        <div className="spinner-container"><div className="spinner" /></div>
      </div>
    );
  }

  return (
    <div>
      <div className="appointments-header">
        <h1>Lich hen cua toi</h1>
        <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
          <div className="filter-tabs">
            {FILTERS.map((f) => (
              <button
                key={f.key}
                className={filter === f.key ? "active" : ""}
                onClick={() => setFilter(f.key)}
              >
                {f.label}
              </button>
            ))}
          </div>
          <Link
            to="/booking"
            className="btn-book"
            style={{ padding: "8px 20px", fontSize: 14, marginTop: 0, textDecoration: "none", display: "inline-block" }}
          >
            + Dat lich moi
          </Link>
        </div>
      </div>

      {error && <div className="alert alert-error">{error}</div>}

      {filtered.length === 0 ? (
        <div className="empty-state">
          <div className="empty-icon">📅</div>
          <p>
            {filter
              ? "Khong co lich hen nao o trang thai nay."
              : "Ban chua co lich hen nao."}
          </p>
        </div>
      ) : (
        <div className="appointment-cards">
          {filtered.map((a) => (
            <div key={a.id} className="appt-card">
              <div className="appt-info">
                <h4>
                  <span className={`status-badge status-${a.status}`}>
                    {STATUS_LABELS[a.status] || a.status}
                  </span>
                  {" "}
                  {a.branchName}
                </h4>
                <div className="appt-details">
                  <span>📅 {a.scheduledDate}</span>
                  <span>🕐 {formatTime(a.startTime)} - {formatTime(a.endTime)}</span>
                  {a.vehicleLicensePlate && <span>🚗 {a.vehicleLicensePlate}</span>}
                  {a.vehicleModelName && <span>{a.vehicleModelName}</span>}
                  {a.serviceNames && a.serviceNames.length > 0 && (
                    <span>🔧 {a.serviceNames.join(", ")}</span>
                  )}
                </div>
                {a.customerNotes && (
                  <p style={{ fontSize: 13, color: "#718096", marginTop: 6 }}>
                    Ghi chu: {a.customerNotes}
                  </p>
                )}
                {a.cancelReason && (
                  <p style={{ fontSize: 13, color: "#e53e3e", marginTop: 4 }}>
                    Ly do huy: {a.cancelReason}
                  </p>
                )}
              </div>
              <div className="appt-actions">
                {(a.status === "PENDING" || a.status === "CONFIRMED") && (
                  <button
                    className="btn-cancel-appt"
                    onClick={() => openCancelPopup(a)}
                    disabled={cancelling === a.id}
                  >
                    {cancelling === a.id ? "Dang huy..." : "Huy lich"}
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Cancel Confirmation Popup */}
      {cancelTarget && (
        <div className="cancel-overlay" onClick={closeCancelPopup}>
          <div className="cancel-popup" onClick={(e) => e.stopPropagation()}>
            <h3>Xac nhan huy lich hen</h3>
            <p style={{ color: "#718096", fontSize: 14, margin: "8px 0 16px" }}>
              Ban co chac muon huy lich hen tai{" "}
              <strong>{cancelTarget.branchName}</strong> vao ngay{" "}
              <strong>{cancelTarget.scheduledDate}</strong>,{" "}
              <strong>{formatTime(cancelTarget.startTime)} - {formatTime(cancelTarget.endTime)}</strong>?
            </p>
            <div style={{ marginBottom: 16 }}>
              <label style={{ display: "block", fontSize: 13, fontWeight: 500, marginBottom: 6, color: "#4a5568" }}>
                Ly do huy (tuy chon)
              </label>
              <textarea
                className="cancel-reason-input"
                placeholder="Nhap ly do huy lich..."
                value={cancelReason}
                onChange={(e) => setCancelReason(e.target.value)}
                rows={3}
              />
            </div>
            {error && <div className="alert alert-error" style={{ marginBottom: 12 }}>{error}</div>}
            <div style={{ display: "flex", gap: 10, justifyContent: "flex-end" }}>
              <button className="btn-popup-secondary" onClick={closeCancelPopup} disabled={cancelling}>
                Quay lai
              </button>
              <button
                className="btn-popup-danger"
                onClick={handleConfirmCancel}
                disabled={cancelling}
              >
                {cancelling ? "Dang huy..." : "Xac nhan huy"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
