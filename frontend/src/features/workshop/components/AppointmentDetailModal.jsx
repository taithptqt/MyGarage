import React, { useMemo } from "react";
import { Modal } from "@features/admin/components/AdminModals";
import { getStatusLabelVi } from "@shared/core/utils/statusLabelUtils";
import { formatAppointmentCancelReason } from "@shared/core/utils/appointmentCancelReasonUtils";
import "./AppointmentDetailModal.css";

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

function normalizeDisplayText(value) {
  return String(value || "")
    .replace(/\[[A-Z_]+:[^\]]*]/g, " ")
    .replace(/^(?:[\s-]|•)+/, "")
    .replace(/\s+/g, " ")
    .trim();
}

function parseSymptomItems(customerNotes) {
  const raw = String(customerNotes || "").trim();
  if (!raw) return [];

  try {
    const parsed = JSON.parse(raw);
    const directSymptoms = Array.isArray(parsed)
      ? parsed
      : Array.isArray(parsed?.symptoms)
        ? parsed.symptoms
        : [];
    const normalizedDirect = directSymptoms.map((entry) => normalizeDisplayText(entry)).filter(Boolean);
    if (normalizedDirect.length > 0) return normalizedDirect;

    const groupedSymptoms = Array.isArray(parsed?.symptomsByVehicle) ? parsed.symptomsByVehicle : [];
    const normalizedGrouped = groupedSymptoms
      .map((group) => {
        const vehicleLabel = normalizeDisplayText(group?.vehicle);
        const symptoms = Array.isArray(group?.symptoms)
          ? group.symptoms.map((entry) => normalizeDisplayText(entry)).filter(Boolean)
          : [];
        if (symptoms.length === 0) return "";
        return vehicleLabel ? `${vehicleLabel}: ${symptoms.join(" | ")}` : symptoms.join(" | ");
      })
      .filter(Boolean);
    if (normalizedGrouped.length > 0) return normalizedGrouped;
  } catch {
    // Fallback to legacy plain text below.
  }

  return raw
    .split(/\r?\n|;/g)
    .map((entry) => normalizeDisplayText(entry))
    .filter(Boolean);
}

function normalizePartsFollowUpServices(rawValue) {
  if (!Array.isArray(rawValue)) return [];

  return rawValue
    .map((service, serviceIndex) => {
      const rawParts = service?.missingParts ?? service?.MissingParts;
      const missingParts = Array.isArray(rawParts)
        ? rawParts
            .map((part, partIndex) => ({
              key: `${service?.serviceId ?? service?.ServiceId ?? serviceIndex}-${part?.productId ?? part?.ProductId ?? partIndex}`,
              productName: normalizeDisplayText(part?.productName ?? part?.ProductName ?? ""),
              requiredQuantity: Number(part?.requiredQuantity ?? part?.RequiredQuantity ?? 0),
              availableQuantity: Number(part?.availableQuantity ?? part?.AvailableQuantity ?? 0),
              unit: normalizeDisplayText(part?.unit ?? part?.Unit ?? ""),
            }))
            .filter((part) => part.productName)
        : [];

      return {
        key: `${service?.serviceId ?? service?.ServiceId ?? serviceIndex}`,
        serviceName: normalizeDisplayText(service?.serviceName ?? service?.ServiceName ?? ""),
        missingParts,
      };
    })
    .filter((service) => service.serviceName && service.missingParts.length > 0);
}

function DetailField({ label, value }) {
  return (
    <div className="appt-detail-field">
      <span className="appt-detail-field-label">{label}</span>
      <span className="appt-detail-field-value">{value || "--"}</span>
    </div>
  );
}

function ImageGallery({ title, urls }) {
  const items = Array.isArray(urls) ? urls.filter(Boolean) : [];
  return (
    <div className="appt-detail-insurance-gallery-block">
      <h5>{title}</h5>
      {items.length > 0 ? (
        <div className="appt-detail-insurance-gallery">
          {items.map((url, index) => (
            <a
              key={`${title}-${index}-${url}`}
              href={url}
              target="_blank"
              rel="noreferrer"
              className="appt-detail-insurance-image-link"
            >
              <img src={url} alt={title} className="appt-detail-insurance-image" />
            </a>
          ))}
        </div>
      ) : (
        <p className="appt-detail-empty">Chưa có ảnh.</p>
      )}
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
  const groupSessionCode = appointment.groupSessionCode || appointment.GroupSessionCode || "";
  const customerNotes = appointment.customerNotes || appointment.CustomerNotes || "";
  const requiresDispatchReview = Boolean(appointment.requiresDispatchReview || appointment.RequiresDispatchReview);
  const dispatchReviewNote = appointment.dispatchReviewNote || appointment.DispatchReviewNote || "";
  const requiresPartsFollowUp = Boolean(appointment.requiresPartsFollowUp || appointment.RequiresPartsFollowUp);
  const partsFollowUpNote = normalizeDisplayText(appointment.partsFollowUpNote || appointment.PartsFollowUpNote || "");
  const partsFollowUpServices = useMemo(
    () => normalizePartsFollowUpServices(appointment.partsFollowUpServices || appointment.PartsFollowUpServices),
    [appointment],
  );
  const cancelReason = appointment.cancelReason || appointment.CancelReason || "";
  const symptomItems = useMemo(() => parseSymptomItems(customerNotes), [customerNotes]);
  const displayCancelReason = useMemo(() => formatAppointmentCancelReason(cancelReason), [cancelReason]);
  const createdAt = appointment.createdAt || appointment.CreatedAt;
  const statusText = appointment.status || appointment.Status || "PENDING";
  const statusLabel = getStatusLabelVi(statusText, statusText);
  const insuranceCompanyName = appointment.insuranceCompanyName || appointment.InsuranceCompanyName || "--";
  const insuranceCardImageUrls = appointment.insuranceCardImageUrls || appointment.InsuranceCardImageUrls || [];
  const insuranceDamageImageUrls = appointment.insuranceDamageImageUrls || appointment.InsuranceDamageImageUrls || [];
  const hasInsuranceEvidence = insuranceCompanyName !== "--" || insuranceCardImageUrls.length > 0 || insuranceDamageImageUrls.length > 0;

  return (
    <Modal
      title="Chi tiết lịch hẹn"
      onClose={onClose}
      className="appt-detail-modal"
      headerActions={onEdit ? (
        <button
          type="button"
          className="btn-primary-large appt-detail-header-edit-btn"
          onClick={() => onEdit(appointment)}
          disabled={onEditDisabled}
          title={onEditDisabled ? "Không thể sửa lịch khi xe đã đến hoặc lịch đã kết thúc." : "Sửa lịch hẹn"}
        >
          Sửa lịch hẹn
        </button>
      ) : null}
    >
      <div className="appt-detail-shell">
        <section className="appt-detail-hero">
          <div className="appt-detail-hero-content">
            <p className="appt-detail-kicker">Mã lịch hẹn #{appointment.id || appointment.Id || "--"}</p>
            <h3>{customerName}</h3>
            <p>{customerPhone}</p>
          </div>
          <div className="appt-detail-hero-side">
            <span className={`badge ${getApptStatusBadge(statusText)}`}>{statusLabel}</span>
            {groupSessionCode ? (
              <span className="appt-detail-group-chip">{groupSessionCode}</span>
            ) : (
              <span className="appt-detail-group-chip muted">Lịch thường</span>
            )}
          </div>
        </section>

        {error ? <div className="appt-detail-alert">{error}</div> : null}

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
                      <span key={`${appointment.id || appointment.Id}-${name}`} className="appt-detail-service-chip">
                        {name}
                      </span>
                    ))}
                  </div>
                ) : (
                  <p className="appt-detail-empty">Chưa có dịch vụ được chỉ định.</p>
                )}
              </article>
            </section>

            {requiresPartsFollowUp ? (
              <section className="appt-detail-issue-card">
                <div className="appt-detail-issue-icon" aria-hidden="true">!</div>
                <div className="appt-detail-issue-copy">
                  <h4>Cảnh báo phụ tùng</h4>
                  <p>
                    {partsFollowUpNote || "Lịch hẹn đang thiếu phụ tùng bắt buộc. Lễ tân cần gọi khách để xác nhận thời gian xử lý."}
                  </p>
                  {partsFollowUpServices.length > 0 ? (
                    <div className="appt-detail-issue-service-list">
                      {partsFollowUpServices.map((service) => (
                        <div key={service.key} className="appt-detail-issue-service-block">
                          <h5>{service.serviceName}</h5>
                          <ul className="appt-detail-issue-part-list">
                            {service.missingParts.map((part) => (
                              <li key={part.key}>
                                <strong>{part.productName}</strong>
                                <span>
                                  {`Cần ${part.requiredQuantity.toLocaleString("vi-VN")}, còn ${part.availableQuantity.toLocaleString("vi-VN")}`}
                                  {part.unit ? ` ${part.unit}` : ""}
                                </span>
                              </li>
                            ))}
                          </ul>
                        </div>
                      ))}
                    </div>
                  ) : null}
                </div>
              </section>
            ) : null}

            {hasInsuranceEvidence ? (
              <section className="appt-detail-notes">
                <h4>Hồ sơ bảo hiểm khách đã gửi</h4>
                <DetailField label="Công ty bảo hiểm" value={insuranceCompanyName} />
                <div className="appt-detail-insurance-grid">
                  <ImageGallery title="Ảnh thẻ bảo hiểm" urls={insuranceCardImageUrls} />
                  <ImageGallery title="Ảnh hiện trạng xe" urls={insuranceDamageImageUrls} />
                </div>
              </section>
            ) : null}

            {requiresDispatchReview ? (
              <section className="appt-detail-notes">
                <h4>Điều phối khoang</h4>
                <p style={{ color: "#9f2d13", fontWeight: 700 }}>
                  {dispatchReviewNote || "Lịch này đang chờ điều phối khoang thủ công. Lễ tân báo quản đốc để gán khoang trước giờ xe vào xưởng."}
                </p>
              </section>
            ) : null}

            <section className="appt-detail-notes">
              <h4>Mô tả thêm</h4>
              {symptomItems.length > 0 ? (
                <ul className="appt-detail-notes-list">
                  {symptomItems.map((item, index) => (
                    <li key={`${appointment.id || appointment.Id || "appt"}-symptom-${index}`}>{item}</li>
                  ))}
                </ul>
              ) : (
                <p>Không có</p>
              )}
            </section>

            {displayCancelReason ? (
              <section className="appt-detail-notes">
                <h4>Lý do hủy</h4>
                <p>{displayCancelReason}</p>
              </section>
            ) : null}
          </>
        )}

        <div className="appt-detail-actions">
          <button type="button" className="btn-primary" onClick={onClose}>
            Đóng
          </button>
        </div>
      </div>
    </Modal>
  );
}
