import React, { useMemo } from "react";
import { Modal } from "@features/admin/components/AdminModals";

function parseServiceNames(appointment) {
  const rawList = appointment?.serviceNames ?? appointment?.ServiceNames;
  if (Array.isArray(rawList) && rawList.length > 0) {
    return rawList.map((name) => String(name || "").trim()).filter(Boolean);
  }

  const csv = appointment?.requestedServiceIds ?? appointment?.RequestedServiceIds;
  if (!csv) return [];
  return String(csv)
    .split(",")
    .map((value) => value.trim())
    .filter(Boolean)
    .map((id) => `Dịch vụ #${id}`);
}

function parseSymptomItems(customerNotes, cancelReason) {
  const items = String(customerNotes || "")
    .split(/\r?\n|;/g)
    .map((entry) => entry.replace(/^(?:[\s-]|•)+/, "").trim())
    .filter(Boolean);

  if (cancelReason) {
    items.push(`Lý do hủy: ${cancelReason}`);
  }
  return items;
}

function DetailField({ label, value }) {
  return (
    <div className="appt-detail-field">
      <span className="appt-detail-field-label">{label}</span>
      <span className="appt-detail-field-value">{value || "--"}</span>
    </div>
  );
}

export default function AppointmentDetailModal({
  appointment,
  loading,
  error,
  onClose,
  onEdit,
  onEditDisabled = false,
  formatApptDate,
  formatApptTime,
  getApptStatusBadge,
}) {
  if (!appointment) return null;

  const serviceNames = useMemo(() => parseServiceNames(appointment), [appointment]);
  const startTime = appointment.startTime || appointment.StartTime;
  const endTime = appointment.endTime || appointment.EndTime;
  const customerName = appointment.customerName || appointment.CustomerName || "Khách hàng";
  const customerPhone = appointment.customerPhone || appointment.CustomerPhone || "--";
  const vehiclePlate = appointment.vehicleLicensePlate || appointment.VehicleLicensePlate || "--";
  const vehicleModel = appointment.vehicleModelName || appointment.VehicleModelName || "--";
  const branchName = appointment.branchName || appointment.BranchName || "--";
  const bookingGroupCode = appointment.bookingGroupCode || appointment.BookingGroupCode || "";
  const customerNotes = appointment.customerNotes || appointment.CustomerNotes || "";
  const cancelReason = appointment.cancelReason || appointment.CancelReason || "";
  const symptomItems = useMemo(
    () => parseSymptomItems(customerNotes, cancelReason),
    [customerNotes, cancelReason],
  );
  const createdAt = appointment.createdAt || appointment.CreatedAt;
  const statusText = appointment.status || appointment.Status || "PENDING";

  return (
    <Modal title="Chi tiết lịch hẹn" onClose={onClose} className="appt-detail-modal">
      <div className="appt-detail-shell">
        <section className="appt-detail-hero">
          <div className="appt-detail-hero-content">
            <p className="appt-detail-kicker">Mã lịch hẹn #{appointment.id || appointment.Id || "--"}</p>
            <h3>{customerName}</h3>
            <p>{customerPhone}</p>
          </div>
          <div className="appt-detail-hero-side">
            <span className={`badge ${getApptStatusBadge(statusText)}`}>{statusText}</span>
            {bookingGroupCode ? (
              <span className="appt-detail-group-chip">{bookingGroupCode}</span>
            ) : (
              <span className="appt-detail-group-chip muted">Lịch thường</span>
            )}
          </div>
        </section>

        {error ? (
          <div className="appt-detail-alert">{error}</div>
        ) : null}

        {loading ? (
          <div className="loading-state appt-detail-loading">
            <div className="spinner" />
            <p>Đang tải chi tiết lịch hẹn...</p>
          </div>
        ) : (
          <>
            <section className="appt-detail-grid">
              <article className="appt-detail-panel">
                <h4>Thông tin khách hàng</h4>
                <DetailField label="Họ tên" value={customerName} />
                <DetailField label="Số điện thoại" value={customerPhone} />
                <DetailField label="Chi nhánh" value={branchName} />
              </article>

              <article className="appt-detail-panel">
                <h4>Thông tin lịch hẹn</h4>
                <DetailField label="Ngày hẹn" value={startTime ? formatApptDate(startTime) : "--"} />
                <DetailField
                  label="Khung giờ"
                  value={startTime ? `${formatApptTime(startTime)} - ${formatApptTime(endTime)}` : "--"}
                />
                <DetailField
                  label="Tạo lúc"
                  value={createdAt ? `${formatApptTime(createdAt)}, ${formatApptDate(createdAt)}` : "--"}
                />
              </article>

              <article className="appt-detail-panel">
                <h4>Thông tin xe</h4>
                <DetailField label="Biển số xe" value={vehiclePlate ? String(vehiclePlate).toUpperCase() : "--"} />
                <DetailField label="Mẫu xe" value={vehicleModel} />
              </article>

              <article className="appt-detail-panel">
                <h4>Dịch vụ yêu cầu</h4>
                {serviceNames.length > 0 ? (
                  <div className="appt-detail-service-list">
                    {serviceNames.map((name) => (
                      <span key={`${appointment.id}-${name}`} className="appt-detail-service-chip">
                        {name}
                      </span>
                    ))}
                  </div>
                ) : (
                  <p className="appt-detail-empty">Chưa có dịch vụ chỉ định.</p>
                )}
              </article>
            </section>

            <section className="appt-detail-notes">
              <h4>Triệu chứng</h4>
              {symptomItems.length > 0 ? (
                <ul className="appt-detail-notes-list">
                  {symptomItems.map((item, index) => (
                    <li key={`${appointment.id || "appt"}-symptom-${index}`}>{item}</li>
                  ))}
                </ul>
              ) : (
                <p>Không có ghi chú gì.</p>
              )}
            </section>
          </>
        )}

        <div className="appt-detail-actions">
          <button
            type="button"
            className="btn-secondary-outline appt-detail-edit-btn"
            onClick={() => onEdit?.(appointment)}
            disabled={!onEdit || onEditDisabled}
            title={onEditDisabled ? "Chỉ có thể sửa lịch ở trạng thái chờ xác nhận hoặc đã xác nhận." : "Sửa lịch hẹn"}
          >
            Sửa lịch hẹn
          </button>
          <button type="button" className="btn-primary" onClick={onClose}>
            Đóng
          </button>
        </div>
      </div>
    </Modal>
  );
}

