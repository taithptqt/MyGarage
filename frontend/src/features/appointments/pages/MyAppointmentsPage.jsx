import { useState, useEffect } from "react";
import { useNavigate, Link } from "react-router-dom";
import { useAuth } from "../../../context/AuthContext";
import {
  getMyAppointments,
  cancelAppointment,
} from "../api/appointmentApi";

const STATUS_LABELS = {
  PENDING: "Chá» xÃ¡c nháº­n",
  CONFIRMED: "ÄÃ£ xÃ¡c nháº­n",
  ARRIVED: "ÄÃ£ Ä‘áº¿n",
  CANCELED: "ÄÃ£ há»§y",
  NO_SHOW: "KhÃ´ng Ä‘áº¿n",
};

const FILTERS = [
  { key: "", label: "Táº¥t cáº£" },
  { key: "PENDING", label: "Chá» xÃ¡c nháº­n" },
  { key: "CONFIRMED", label: "ÄÃ£ xÃ¡c nháº­n" },
  { key: "ARRIVED", label: "ÄÃ£ Ä‘áº¿n" },
  { key: "CANCELED", label: "ÄÃ£ há»§y" },
];

function formatTime(t) {
  if (!t) return "";
  const d = new Date(t);
  if (Number.isNaN(d.getTime())) return "";
  return d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
}

function formatDate(t) {
  if (!t) return "";
  const d = new Date(t);
  if (Number.isNaN(d.getTime())) return "";
  return d.toLocaleDateString("vi-VN");
}

export default function MyAppointmentsPage() {
  const navigate = useNavigate();
  const { isAuthenticated } = useAuth();

  const [appointments, setAppointments] = useState([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState("");
  const [cancelling, setCancelling] = useState(null);
  const [error, setError] = useState("");
  const [cancelTarget, setCancelTarget] = useState(null);
  const [cancelReason, setCancelReason] = useState("");

  useEffect(() => {
    if (!isAuthenticated) {
      navigate("/login", { replace: true });
      return;
    }
    loadAppointments();
  }, [isAuthenticated, navigate]);

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
      await cancelAppointment(cancelTarget.id, cancelReason || "KhÃ¡ch hÃ ng tá»± há»§y");
      closeCancelPopup();
      await loadAppointments();
    } catch (err) {
      setError(err.message || "Há»§y tháº¥t báº¡i.");
    } finally {
      setCancelling(null);
    }
  }

  const filtered = filter
    ? appointments.filter((a) => a.status === filter)
    : appointments;

  if (loading) {
    return (
      <div>
        <h1 style={{ fontSize: 24, fontWeight: 700, marginBottom: 24 }}>Lá»‹ch háº¹n cá»§a tÃ´i</h1>
        <div className="spinner-container"><div className="spinner" /></div>
      </div>
    );
  }

  return (
    <div>
      <div className="appointments-header">
        <h1>Lá»‹ch háº¹n cá»§a tÃ´i</h1>
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
            + Äáº·t lá»‹ch má»›i
          </Link>
        </div>
      </div>

      {error && <div className="alert alert-error">{error}</div>}

      {filtered.length === 0 ? (
        <div className="empty-state">
          <div className="empty-icon">ðŸ“…</div>
          <p>
            {filter
              ? "KhÃ´ng cÃ³ lá»‹ch háº¹n nÃ o á»Ÿ tráº¡ng thÃ¡i nÃ y."
              : "Báº¡n chÆ°a cÃ³ lá»‹ch háº¹n nÃ o."}
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
                  <span>ðŸ“… {formatDate(a.startTime)}</span>
                  <span>ðŸ• {formatTime(a.startTime)} - {formatTime(a.endTime)}</span>
                  {a.vehicleLicensePlate && <span>ðŸš— {a.vehicleLicensePlate}</span>}
                  {a.vehicleModelName && <span>{a.vehicleModelName}</span>}
                  {a.serviceNames && a.serviceNames.length > 0 && (
                    <span>ðŸ”§ {a.serviceNames.join(", ")}</span>
                  )}
                </div>
                {a.customerNotes && (
                  <p style={{ fontSize: 13, color: "#718096", marginTop: 6 }}>
                    Ghi chÃº: {a.customerNotes}
                  </p>
                )}
                {a.cancelReason && (
                  <p style={{ fontSize: 13, color: "#e53e3e", marginTop: 4 }}>
                    LÃ½ do há»§y: {a.cancelReason}
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
                    {cancelling === a.id ? "Äang há»§y..." : "Há»§y lá»‹ch"}
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
            <h3>XÃ¡c nháº­n há»§y lá»‹ch háº¹n</h3>
            <p style={{ color: "#718096", fontSize: 14, margin: "8px 0 16px" }}>
              Báº¡n cÃ³ cháº¯c muá»‘n há»§y lá»‹ch háº¹n táº¡i{" "}
              <strong>{cancelTarget.branchName}</strong> vÃ o ngÃ y{" "}
              <strong>{formatDate(cancelTarget.startTime)}</strong>,{" "}
              <strong>{formatTime(cancelTarget.startTime)} - {formatTime(cancelTarget.endTime)}</strong>?
            </p>
            <div style={{ marginBottom: 16 }}>
              <label style={{ display: "block", fontSize: 13, fontWeight: 500, marginBottom: 6, color: "#4a5568" }}>
                LÃ½ do há»§y (tÃ¹y chá»n)
              </label>
              <textarea
                className="cancel-reason-input"
                placeholder="Nháº­p lÃ½ do há»§y lá»‹ch..."
                value={cancelReason}
                onChange={(e) => setCancelReason(e.target.value)}
                rows={3}
              />
            </div>
            {error && <div className="alert alert-error" style={{ marginBottom: 12 }}>{error}</div>}
            <div style={{ display: "flex", gap: 10, justifyContent: "flex-end" }}>
              <button className="btn-popup-secondary" onClick={closeCancelPopup} disabled={cancelling}>
                Quay láº¡i
              </button>
              <button
                className="btn-popup-danger"
                onClick={handleConfirmCancel}
                disabled={cancelling}
              >
                {cancelling ? "Äang há»§y..." : "XÃ¡c nháº­n há»§y"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}


