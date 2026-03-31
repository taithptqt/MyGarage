import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../../../context/AuthContext";
import { cancelAppointment, getMyAppointments } from "../api/appointmentApi";
import Footer from "../../../shared/components/Footer";
import Header from "../../../shared/components/Header";
import "./MyAppointmentsPage.css";

const STATUS_META = {
  PENDING: { label: "Chờ xác nhận", tone: "pending" },
  CONFIRMED: { label: "Đã xác nhận", tone: "confirmed" },
  ARRIVED: { label: "Đã đến", tone: "arrived" },
  CANCELED: { label: "Đã hủy", tone: "canceled" },
  NO_SHOW: { label: "Không đến", tone: "no-show" },
};

const FILTERS = [
  { key: "", label: "Tất cả" },
  { key: "PENDING", label: "Chờ xác nhận" },
  { key: "CONFIRMED", label: "Đã xác nhận" },
  { key: "ARRIVED", label: "Đã đến" },
  { key: "CANCELED", label: "Đã hủy" },
  { key: "NO_SHOW", label: "Không đến" },
];

function toDateTime(value) {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
}

function formatDate(value) {
  const date = toDateTime(value);
  if (!date) return "--";
  return date.toLocaleDateString("vi-VN", {
    weekday: "long",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
}

function formatTime(value) {
  const date = toDateTime(value);
  if (!date) return "--";
  return date.toLocaleTimeString("vi-VN", {
    hour: "2-digit",
    minute: "2-digit",
  });
}

function normalizeAppointmentsResponse(payload) {
  if (Array.isArray(payload)) return payload;
  if (Array.isArray(payload?.items)) return payload.items;
  if (Array.isArray(payload?.data)) return payload.data;
  if (Array.isArray(payload?.result)) return payload.result;
  if (Array.isArray(payload?.$values)) return payload.$values;
  return [];
}

function extractDisplayNotes(raw) {
  const source = String(raw || "").trim();
  if (!source) return "";

  const withoutMeta = source
    .replace(/\s*\[FLOW:[^\]]*\]/gi, "")
    .replace(/\s*\[CLUSTER:[^\]]*\]/gi, "")
    .trim();

  if (!withoutMeta) return "";

  try {
    const parsed = JSON.parse(withoutMeta);
    const symptoms = Array.isArray(parsed)
      ? parsed
      : Array.isArray(parsed?.symptoms)
        ? parsed.symptoms
        : [];
    const cleaned = symptoms
      .map((entry) => String(entry || "").trim())
      .filter(Boolean)
      .filter((entry) => entry.toLowerCase() !== "không có ghi chú gì");
    return cleaned.join(", ");
  } catch {
    if (withoutMeta.toLowerCase() === "không có ghi chú gì") return "";
    return withoutMeta;
  }
}

function normalizeAppointment(item) {
  return {
    id: item?.id ?? item?.Id,
    status: String(item?.status ?? item?.Status ?? "").toUpperCase(),
    branchName: item?.branchName ?? item?.BranchName ?? "",
    startTime: item?.startTime ?? item?.StartTime,
    endTime: item?.endTime ?? item?.EndTime,
    vehicleLicensePlate: item?.vehicleLicensePlate ?? item?.VehicleLicensePlate ?? "",
    vehicleModelName: item?.vehicleModelName ?? item?.VehicleModelName ?? "",
    serviceNames: Array.isArray(item?.serviceNames)
      ? item.serviceNames
      : Array.isArray(item?.ServiceNames)
        ? item.ServiceNames
        : [],
    customerNotes: extractDisplayNotes(item?.customerNotes ?? item?.CustomerNotes ?? ""),
    cancelReason: item?.cancelReason ?? item?.CancelReason ?? "",
  };
}

function sortAppointments(items) {
  return [...items].sort((a, b) => {
    const timeA = toDateTime(a.startTime)?.getTime() || 0;
    const timeB = toDateTime(b.startTime)?.getTime() || 0;
    return timeB - timeA;
  });
}

export default function MyAppointmentsPage() {
  const navigate = useNavigate();
  const { isAuthenticated } = useAuth();

  const [appointments, setAppointments] = useState([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState("");
  const [searchTerm, setSearchTerm] = useState("");
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
    setError("");
    try {
      const payload = await getMyAppointments();
      const list = normalizeAppointmentsResponse(payload)
        .map(normalizeAppointment)
        .filter((item) => item.id !== undefined && item.id !== null);
      setAppointments(sortAppointments(list));
    } catch (err) {
      setAppointments([]);
      setError(err?.message || "Không thể tải lịch hẹn. Vui lòng thử lại.");
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
      await cancelAppointment(cancelTarget.id, cancelReason || "Khách hàng tự hủy");
      closeCancelPopup();
      await loadAppointments();
    } catch (err) {
      setError(err?.message || "Hủy lịch thất bại.");
    } finally {
      setCancelling(null);
    }
  }

  const stats = useMemo(() => {
    const now = Date.now();
    const upcomingCount = appointments.filter((item) => {
      const start = toDateTime(item.startTime)?.getTime() || 0;
      return start >= now && item.status !== "CANCELED";
    }).length;

    return {
      total: appointments.length,
      upcoming: upcomingCount,
      confirmed: appointments.filter((item) => item.status === "CONFIRMED").length,
      canceled: appointments.filter((item) => item.status === "CANCELED").length,
    };
  }, [appointments]);

  const filtered = useMemo(() => {
    const term = searchTerm.trim().toLowerCase();
    return appointments.filter((item) => {
      if (filter && item.status !== filter) return false;
      if (!term) return true;

      const combined = [
        item.branchName,
        item.vehicleLicensePlate,
        item.vehicleModelName,
        Array.isArray(item.serviceNames) ? item.serviceNames.join(" ") : "",
      ]
        .filter(Boolean)
        .join(" ")
        .toLowerCase();

      return combined.includes(term);
    });
  }, [appointments, filter, searchTerm]);

  return (
    <div className="my-appt-page">
      <div className="my-appt-shell">
        <Header />

        <section className="my-appt-content">
          <header className="my-appt-head">
            <div>
              <p className="my-appt-eyebrow">Trung tâm lịch hẹn</p>
              <h1>Lịch hẹn của tôi</h1>
              <p className="my-appt-description">
                Theo dõi trạng thái lịch hẹn, thông tin xe và khung giờ xử lý trong một màn hình.
              </p>
            </div>
            <Link to="/booking/form" className="my-appt-primary-btn">
              Đặt lịch mới
            </Link>
          </header>

          <section className="my-appt-stats">
            <article className="my-appt-stat-card">
              <span>Tổng lịch hẹn</span>
              <strong>{stats.total}</strong>
            </article>
            <article className="my-appt-stat-card">
              <span>Sắp tới</span>
              <strong>{stats.upcoming}</strong>
            </article>
            <article className="my-appt-stat-card">
              <span>Đã xác nhận</span>
              <strong>{stats.confirmed}</strong>
            </article>
            <article className="my-appt-stat-card">
              <span>Đã hủy</span>
              <strong>{stats.canceled}</strong>
            </article>
          </section>

          <section className="my-appt-toolbar">
            <div className="my-appt-filter-list">
              {FILTERS.map((item) => (
                <button
                  key={item.key}
                  type="button"
                  className={filter === item.key ? "active" : ""}
                  onClick={() => setFilter(item.key)}
                >
                  {item.label}
                </button>
              ))}
            </div>
            <input
              type="text"
              className="my-appt-search"
              placeholder="Tìm theo chi nhánh, biển số, dịch vụ..."
              value={searchTerm}
              onChange={(event) => setSearchTerm(event.target.value)}
            />
          </section>

          {error && <div className="my-appt-alert my-appt-alert-error">{error}</div>}

          {loading ? (
            <div className="spinner-container">
              <div className="spinner" />
            </div>
          ) : filtered.length === 0 ? (
            <section className="my-appt-empty">
              <h3>Chưa có lịch hẹn phù hợp</h3>
              <p>
                {filter || searchTerm
                  ? "Không tìm thấy lịch hẹn theo bộ lọc hiện tại."
                  : "Bạn chưa có lịch hẹn nào. Hãy đặt lịch để gara chuẩn bị tiếp nhận xe."}
              </p>
              <div style={{ display: "flex", gap: 10, justifyContent: "center", flexWrap: "wrap" }}>
                <Link to="/booking/form" className="my-appt-secondary-btn">
                  Đặt lịch ngay
                </Link>
                <button type="button" className="my-appt-secondary-btn" onClick={loadAppointments}>
                  Tải lại
                </button>
              </div>
            </section>
          ) : (
            <section className="my-appt-list">
              {filtered.map((item) => {
                const status = STATUS_META[item.status] || { label: item.status || "Không rõ", tone: "default" };
                const canCancel = item.status === "PENDING" || item.status === "CONFIRMED";
                return (
                  <article key={item.id} className="my-appt-card">
                    <div className="my-appt-card-main">
                      <div className="my-appt-card-head">
                        <h3>{item.branchName || "Chi nhánh chưa xác định"}</h3>
                        <span className={`my-appt-status ${status.tone}`}>{status.label}</span>
                      </div>
                      <div className="my-appt-card-grid">
                        <div>
                          <label>Ngày tiếp nhận</label>
                          <p>{formatDate(item.startTime)}</p>
                        </div>
                        <div>
                          <label>Khung giờ</label>
                          <p>{`${formatTime(item.startTime)} - ${formatTime(item.endTime)}`}</p>
                        </div>
                        <div>
                          <label>Biển số</label>
                          <p>{item.vehicleLicensePlate || "--"}</p>
                        </div>
                        <div>
                          <label>Dòng xe</label>
                          <p>{item.vehicleModelName || "--"}</p>
                        </div>
                      </div>
                      {Array.isArray(item.serviceNames) && item.serviceNames.length > 0 && (
                        <div className="my-appt-service-list">
                          {item.serviceNames.map((serviceName) => (
                            <span key={`${item.id}-${serviceName}`}>{serviceName}</span>
                          ))}
                        </div>
                      )}
                      <p className="my-appt-note">
                        <strong>Ghi chú:</strong> {item.customerNotes || ""}
                      </p>
                      {item.cancelReason && (
                        <p className="my-appt-note cancel">
                          <strong>Lý do hủy:</strong> {item.cancelReason}
                        </p>
                      )}
                    </div>
                    <div className="my-appt-card-actions">
                      {canCancel && (
                        <button
                          type="button"
                          className="my-appt-cancel-btn"
                          onClick={() => openCancelPopup(item)}
                          disabled={cancelling === item.id}
                        >
                          {cancelling === item.id ? "Đang hủy..." : "Hủy lịch"}
                        </button>
                      )}
                    </div>
                  </article>
                );
              })}
            </section>
          )}
        </section>

        <Footer />
      </div>

      {cancelTarget && (
        <div className="cancel-overlay" onClick={closeCancelPopup}>
          <div className="cancel-popup" onClick={(event) => event.stopPropagation()}>
            <h3>Xác nhận hủy lịch hẹn</h3>
            <p className="my-appt-cancel-desc">
              Bạn có chắc muốn hủy lịch tại <strong>{cancelTarget.branchName}</strong> vào{" "}
              <strong>{formatDate(cancelTarget.startTime)}</strong>,{" "}
              <strong>{formatTime(cancelTarget.startTime)} - {formatTime(cancelTarget.endTime)}</strong>?
            </p>
            <div className="my-appt-cancel-form">
              <label htmlFor="cancel-reason">Lý do hủy (tùy chọn)</label>
              <textarea
                id="cancel-reason"
                className="cancel-reason-input"
                placeholder="Ví dụ: thay đổi kế hoạch cá nhân"
                value={cancelReason}
                onChange={(event) => setCancelReason(event.target.value)}
                rows={3}
              />
            </div>
            {error && <div className="my-appt-alert my-appt-alert-error">{error}</div>}
            <div className="my-appt-popup-actions">
              <button type="button" className="btn-popup-secondary" onClick={closeCancelPopup} disabled={cancelling}>
                Quay lại
              </button>
              <button type="button" className="btn-popup-danger" onClick={handleConfirmCancel} disabled={cancelling}>
                {cancelling ? "Đang hủy..." : "Xác nhận hủy"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
