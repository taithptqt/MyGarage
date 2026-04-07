import React, { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Modal } from "@features/admin/components/AdminModals";
import { MdAssignment, MdCheckCircle, MdDirectionsCar, MdEdit, MdHistory, MdPerson } from "react-icons/md";
import { formatOrderDateTime, parseCustomerNotes } from "@features/workshop/utils/serviceOrderDetailUtils";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { vehicleConditionLogApi } from "@features/workshop/api/vehicleConditionLogApi";

const STATUS_LABELS = {
  NEW: "Mới tiếp nhận",
  RECEPTION: "Lễ tân tiếp nhận",
  INSPECTING: "Đang chuẩn đoán",
  WAITING_APPROVAL: "Chờ duyệt",
  REPAIRING: "Đang sửa chữa",
  QC_PENDING: "Chờ kiểm tra chất lượng",
  READY: "Sẵn sàng giao xe",
  COMPLETED: "Hoàn tất",
  CANCELED: "Đã hủy",
  CANCELLED: "Đã hủy",
};

const ORDER_CATEGORY_LABELS = {
  MAINTENANCE: "Bảo dưỡng",
  WARRANTLY: "Bảo hành",
  WARRANTY: "Bảo hành",
  QUICK_SERVICE: "Dịch vụ nhanh",
  INSURANCE_REPAIR: "Sửa chữa bảo hiểm",
  GENERAL_REPAIR: "Sửa chữa chung",
  REWORK: "Sửa chữa lại",
  REPAIR: "Sửa chữa",
};

const DISPATCH_TAG_OPTIONS = [
  { value: "ENGINE_CHASSIS", label: "Máy - Gầm", description: "Động cơ, hộp số, hệ thống gầm." },
  { value: "ELECTRICAL_AC", label: "Điện - Máy lạnh", description: "Điện thân xe, điện động cơ, điều hòa." },
  { value: "BODY_PAINT", label: "Đồng sơn - Chăm sóc xe", description: "Gò, hàn, sơn và chăm sóc ngoại thất." },
  { value: "MAINTENANCE", label: "Bảo dưỡng nhanh", description: "Bảo dưỡng định kỳ và hạng mục nhanh." },
];

function formatStatusLabel(status) {
  const key = String(status || "").toUpperCase();
  return STATUS_LABELS[key] || key || "Không xác định";
}

function formatOrderCategoryLabel(orderCategory) {
  const key = String(orderCategory || "").toUpperCase();
  if (!key) return "Sửa chữa";
  return ORDER_CATEGORY_LABELS[key] || key.replace(/_/g, " ");
}

function firstText(...values) {
  for (const value of values) {
    const text = String(value || "").trim();
    if (text) return text;
  }
  return "";
}

function formatMoney(amount) {
  const value = Number(amount || 0);
  return `${value.toLocaleString("vi-VN")}đ`;
}

export default function ServiceOrderDetailModal({
  order,
  onClose,
  onRefresh,
  customers = [],
  vehicles = [],
  staff = [],
}) {
  const navigate = useNavigate();

  const customer = customers.find((item) => Number(item?.id) === Number(order?.customerId));
  const vehicle = vehicles.find((item) => Number(item?.id) === Number(order?.vehicleId));
  const advisor = staff.find((item) => Number(item?.id) === Number(order?.serviceAdvisorId));

  const parsedNotes = parseCustomerNotes(order?.customerNotes);
  const symptomNoteItems = useMemo(() => {
    try {
      const parsed = JSON.parse(String(order?.customerNotes || "").trim());
      const list = Array.isArray(parsed)
        ? parsed
        : Array.isArray(parsed?.symptoms)
          ? parsed.symptoms
          : [];
      return list.map((item) => String(item || "").trim()).filter(Boolean);
    } catch {
      return [];
    }
  }, [order?.customerNotes]);

  const [orderDetails, setOrderDetails] = useState([]);
  const [loadingDetails, setLoadingDetails] = useState(false);
  const [diagnoses, setDiagnoses] = useState([]);
  const [hasSignedCheckIn, setHasSignedCheckIn] = useState(false);
  const [selectedDispatchTag, setSelectedDispatchTag] = useState("ENGINE_CHASSIS");
  const [dispatchTagDraft, setDispatchTagDraft] = useState("ENGINE_CHASSIS");
  const [showDispatchPopup, setShowDispatchPopup] = useState(false);
  const [submittingDiagnosisRequest, setSubmittingDiagnosisRequest] = useState(false);

  const customerChosenDetails = useMemo(() => {
    const isLaborOrService = (detail) => {
      const itemType = String(detail?.itemType || "").toUpperCase();
      return itemType === "LABOR" || itemType === "SERVICE";
    };

    const candidates = (orderDetails || []).filter(
      (detail) => isLaborOrService(detail) && !detail?.diagnoseRecommendationId
    );

    const uniqueMap = new Map();
    candidates.forEach((detail) => {
      const key = detail?.standardServiceId
        ? `std-${detail.standardServiceId}`
        : detail?.itemId
          ? `item-${detail.itemId}`
          : `name-${String(detail?.itemName || "").trim().toLowerCase()}`;
      if (!uniqueMap.has(key)) {
        uniqueMap.set(key, detail);
      }
    });

    return Array.from(uniqueMap.values());
  }, [orderDetails]);

  useEffect(() => {
    const fetchDetailData = async () => {
      if (!order?.id) return;
      setLoadingDetails(true);
      try {
        const [detailsRes, diagnosesRes, conditionLogsRes] = await Promise.all([
          serviceOrderApi.getDetails(order.id),
          serviceOrderApi.getDiagnoses(order.id),
          vehicleConditionLogApi.getByServiceOrderId(order.id),
        ]);

        setOrderDetails(Array.isArray(detailsRes) ? detailsRes : []);
        setDiagnoses(Array.isArray(diagnosesRes) ? diagnosesRes : []);

        const signedCheckInExists = Array.isArray(conditionLogsRes) && conditionLogsRes.some((log) => {
          const logType = String(log?.logType || log?.LogType || "").toUpperCase();
          const signatureUrl = String(log?.customerSignatureUrl || log?.CustomerSignatureUrl || "").trim();
          return logType === "CHECK_IN" && signatureUrl.length > 0;
        });
        setHasSignedCheckIn(signedCheckInExists);
      } catch (error) {
        console.error("Lỗi tải chi tiết lệnh sửa chữa:", error);
        setHasSignedCheckIn(false);
      } finally {
        setLoadingDetails(false);
      }
    };

    void fetchDetailData();
  }, [order?.id]);

  const latestDiagnosis = useMemo(() => {
    return [...diagnoses]
      .sort((a, b) => {
        const timeA = new Date(a?.createdAt || a?.updatedAt || 0).getTime();
        const timeB = new Date(b?.createdAt || b?.updatedAt || 0).getTime();
        return timeB - timeA;
      })[0] || null;
  }, [diagnoses]);

  useEffect(() => {
    const normalizedTag = String(latestDiagnosis?.dispatchTag || "").trim().toUpperCase();
    if (normalizedTag && DISPATCH_TAG_OPTIONS.some((item) => item.value === normalizedTag)) {
      setSelectedDispatchTag(normalizedTag);
      setDispatchTagDraft(normalizedTag);
      return;
    }
    setSelectedDispatchTag("ENGINE_CHASSIS");
    setDispatchTagDraft("ENGINE_CHASSIS");
  }, [order?.id, latestDiagnosis?.dispatchTag]);

  const diagnosisStatus = String(latestDiagnosis?.status || "").toUpperCase();
  const isDiagnosisRequested = diagnosisStatus === "PENDING" || diagnosisStatus === "ASSIGNED";
  const isDiagnosisAssigned = diagnosisStatus === "ASSIGNED";
  const isDiagnosisCompleted = diagnosisStatus === "COMPLETED";

  const isFromAppointment = Boolean(order?.appointmentId);
  const isGuest = !order?.appointmentId && customer && customer.isRegistered === false;

  const sourceMeta = useMemo(() => {
    if (isFromAppointment) {
      return {
        text: "Từ lịch hẹn trước",
        tone: "source-appt",
        Icon: MdHistory,
      };
    }
    if (isGuest) {
      return {
        text: "Khách vãng lai (tạo nhanh)",
        tone: "source-guest",
        Icon: MdPerson,
      };
    }
    return {
      text: "Lễ tân tiếp nhận trực tiếp",
      tone: "source-reception",
      Icon: MdAssignment,
    };
  }, [isFromAppointment, isGuest]);

  const customerName = firstText(order?.customerName, customer?.fullName, "-");
  const customerPhone = firstText(order?.customerPhone, customer?.phone, "-");
  const vehiclePlate = firstText(order?.vehicleLicensePlate, vehicle?.licensePlate, "N/A");
  const vehicleModel = firstText(order?.vehicleModelName, vehicle?.modelName, "Chưa rõ dòng xe");
  const vehicleColor = firstText(order?.vehicleColor, vehicle?.color);
  const vehicleVin = firstText(order?.vehicleVin, vehicle?.vin);
  const advisorName = firstText(advisor?.fullName, "-");

  const statusLabel = formatStatusLabel(order?.status);
  const categoryLabel = formatOrderCategoryLabel(order?.orderCategory);

  const handleOpenPage = (path) => {
    onClose?.();
    navigate(path);
  };

  const refreshAfterDiagnosisChange = async () => {
    if (onRefresh) await onRefresh();
    const diagnosisRes = await serviceOrderApi.getDiagnoses(order.id);
    setDiagnoses(Array.isArray(diagnosisRes) ? diagnosisRes : []);
  };

  const openVehicleLogPage = () => {
    handleOpenPage(`/workshop/reception/orders/${order.id}/vehicleconditionlogs`);
  };

  const handleRequestDiagnosis = async () => {
    if (!hasSignedCheckIn) {
      try {
        await serviceOrderApi.createCheckInRequest(order.id);
      } catch {
        // Ignore duplicated marker creation and continue redirecting.
      }

      alert("Vui lòng hoàn tất Nhật ký xe và ký CHECK-IN trước khi gửi yêu cầu chuẩn đoán.");
      openVehicleLogPage();
      return;
    }

    setDispatchTagDraft(selectedDispatchTag || "ENGINE_CHASSIS");
    setShowDispatchPopup(true);
  };

  const handleConfirmDiagnosisRequest = async () => {
    const normalizedTag = DISPATCH_TAG_OPTIONS.some((item) => item.value === dispatchTagDraft)
      ? dispatchTagDraft
      : "ENGINE_CHASSIS";

    setSubmittingDiagnosisRequest(true);
    try {
      await serviceOrderApi.requestDiagnosis(order.id, normalizedTag);
      setSelectedDispatchTag(normalizedTag);
      setShowDispatchPopup(false);
      await refreshAfterDiagnosisChange();
    } catch (error) {
      alert(`Lỗi khi yêu cầu chuẩn đoán: ${error?.message || "Không xác định"}`);
    } finally {
      setSubmittingDiagnosisRequest(false);
    }
  };

  const handleRecallDiagnosis = async () => {
    const confirmed = window.confirm("Thu hồi yêu cầu chuẩn đoán?");
    if (!confirmed) return;
    try {
      await serviceOrderApi.recallDiagnosisRequest(order.id);
      await refreshAfterDiagnosisChange();
    } catch (error) {
      alert(`Lỗi khi thu hồi yêu cầu chuẩn đoán: ${error?.message || "Không xác định"}`);
    }
  };

  const diagnosisHint = useMemo(() => {
    if (isDiagnosisAssigned) return "Kỹ thuật viên đang thực hiện chuẩn đoán.";
    if (!hasSignedCheckIn && !isDiagnosisCompleted && !isDiagnosisRequested) {
      return "Cần hoàn tất Nhật ký xe và chữ ký CHECK-IN trước khi gửi yêu cầu chuẩn đoán.";
    }
    if (!isDiagnosisCompleted && !isDiagnosisRequested) {
      return "Khi bấm \"Yêu cầu chuẩn đoán\", hệ thống sẽ mở popup để chọn chuyên môn điều phối.";
    }
    return "";
  }, [hasSignedCheckIn, isDiagnosisAssigned, isDiagnosisCompleted, isDiagnosisRequested]);

  return (
    <>
      <Modal
        title={(
          <div className="so-detail-title-wrap">
            <span className="so-detail-title-text">{`Chi tiết lệnh sửa chữa: ${order?.code || ""}`}</span>
            <button
              type="button"
              className="so-detail-edit-btn"
              onClick={() => handleOpenPage(`/workshop/reception/orders/${order.id}/edit`)}
            >
              <MdEdit aria-hidden="true" />
              <span>Chỉnh sửa</span>
            </button>
          </div>
        )}
        onClose={onClose}
        className="modal-lg service-order-detail-modal"
      >
        <div className="so-detail-shell">
        <section className="so-detail-overview-card">
          <div className="so-detail-chip-row">
            <span className="so-detail-chip so-detail-chip-status">{statusLabel}</span>
            <span className={`so-detail-chip ${sourceMeta.tone}`}>
              <sourceMeta.Icon aria-hidden="true" />
              {sourceMeta.text}
            </span>
            <span className="so-detail-chip so-detail-chip-category">{categoryLabel}</span>
          </div>

          <div className="so-detail-party-grid">
            <article className="so-detail-party-card">
              <p className="so-detail-card-label">Khách hàng</p>
              <h4>{customerName}</h4>
              <p>{customerPhone}</p>
            </article>

            <article className="so-detail-party-card">
              <div className="so-detail-party-head">
                <p className="so-detail-card-label">Phương tiện</p>
                <button
                  type="button"
                  className="so-detail-link-btn"
                  onClick={() => handleOpenPage(`/workshop/reception/orders/${order.id}/vehicleconditionlogs`)}
                >
                  Nhật ký xe
                </button>
              </div>
              <h4>{vehiclePlate}</h4>
              <p>{vehicleColor ? `${vehicleModel} - ${vehicleColor}` : vehicleModel}</p>
              {vehicleVin ? <p className="so-detail-vin">VIN: {vehicleVin}</p> : null}
            </article>
          </div>
        </section>

        <section className="so-detail-time-grid">
          <article className="so-detail-time-item">
            <p>Thời điểm vào</p>
            <strong>{formatOrderDateTime(order?.checkInTime)}</strong>
          </article>
          <article className="so-detail-time-item">
            <p>Hẹn trả xe</p>
            <strong>{formatOrderDateTime(order?.promisedHandoverTime)}</strong>
          </article>
          <article className="so-detail-time-item">
            <p>Dự kiến hoàn thành</p>
            <strong>{formatOrderDateTime(order?.estimatedHandover)}</strong>
          </article>
          <article className="so-detail-time-item">
            <p>Cố vấn dịch vụ</p>
            <strong>{advisorName}</strong>
          </article>
        </section>

        <section className="so-detail-section">
          <h3 className="so-detail-section-title">
            <MdAssignment aria-hidden="true" />
            <span>Ghi chú & nguồn gốc tiếp nhận</span>
          </h3>

          <div className="so-detail-note-grid">
            <article className="so-detail-panel">
              <p className="so-detail-panel-title">Dịch vụ cụ thể khách đã chọn</p>
              {loadingDetails ? (
                <p className="so-detail-empty">Đang tải dữ liệu dịch vụ...</p>
              ) : customerChosenDetails.length > 0 ? (
                <div className="so-detail-service-list">
                  {customerChosenDetails.map((detail) => (
                    <div key={detail.id} className="so-detail-service-row">
                      <span>{detail.itemName}</span>
                      <strong>{formatMoney(detail.finalAmount || detail.unitPriceAtTime || 0)}</strong>
                    </div>
                  ))}
                </div>
              ) : parsedNotes.requestedServices ? (
                <p className="so-detail-body-text">{parsedNotes.requestedServices}</p>
              ) : (
                <p className="so-detail-empty">Chưa có dịch vụ cụ thể nào được chọn.</p>
              )}
            </article>

            <article className="so-detail-panel">
              <p className="so-detail-panel-title">Ghi chú / Yêu cầu thêm</p>
              {symptomNoteItems.length > 0 ? (
                <ul className="so-detail-note-list">
                  {symptomNoteItems.map((item, index) => (
                    <li key={`${item}-${index}`}>{item}</li>
                  ))}
                </ul>
              ) : (
                <p className="so-detail-body-text">
                  {String(parsedNotes.mainNotes || "").toLowerCase().includes("không có")
                    ? "Không có ghi chú gì"
                    : parsedNotes.mainNotes || "Không có ghi chú gì"}
                </p>
              )}
            </article>
          </div>
        </section>

        <section className="so-detail-section">
          <h3 className="so-detail-section-title">
            <MdCheckCircle aria-hidden="true" />
            <span>Xem chi tiết theo từng mục</span>
          </h3>
          <p className="so-detail-section-note">Chọn mục cần xem để mở trang chi tiết tương ứng.</p>

          <div className="so-detail-action-row">
            {isDiagnosisCompleted ? (
              <button
                type="button"
                className="so-detail-action-btn so-detail-action-primary"
                onClick={() => handleOpenPage(`/workshop/reception/orders/${order.id}/diagnosis`)}
              >
                Xem chuẩn đoán
              </button>
            ) : isDiagnosisRequested || String(order?.status || "").toUpperCase() === "INSPECTING" ? (
              <button
                type="button"
                className="so-detail-action-btn so-detail-action-warn"
                onClick={handleRecallDiagnosis}
              >
                Thu hồi yêu cầu
              </button>
            ) : (
              <button
                type="button"
                className="so-detail-action-btn so-detail-action-ghost"
                onClick={handleRequestDiagnosis}
                title={!hasSignedCheckIn ? "Cần vào Nhật ký xe để hoàn tất CHECK-IN trước." : ""}
              >
                Yêu cầu chuẩn đoán
              </button>
            )}

            <button
              type="button"
              className="so-detail-action-btn so-detail-action-teal"
              onClick={() => handleOpenPage(`/workshop/reception/orders/${order.id}/items`)}
            >
              Xem hạng mục & phụ tùng
            </button>

            <button
              type="button"
              className="so-detail-action-btn so-detail-action-purple"
              onClick={() => handleOpenPage(`/workshop/reception/orders/${order.id}/quote`)}
            >
              Xem báo giá
            </button>
          </div>

          {diagnosisHint ? <p className="so-detail-diagnosis-hint">{diagnosisHint}</p> : null}
        </section>
      </div>
      </Modal>

      {showDispatchPopup ? (
        <Modal
          title="Chọn chuyên môn chuẩn đoán"
          onClose={() => {
            if (!submittingDiagnosisRequest) setShowDispatchPopup(false);
          }}
          className="so-diagnosis-request-modal"
        >
          <div className="so-dispatch-popup">
            <p className="so-dispatch-popup-note">
              Chọn chuyên môn điều phối cho xe <strong>{vehiclePlate}</strong>. Hệ thống sẽ gửi yêu cầu tới nhóm phù hợp.
            </p>

            <div className="so-dispatch-popup-grid">
              {DISPATCH_TAG_OPTIONS.map((option) => {
                const isActive = option.value === dispatchTagDraft;
                return (
                  <button
                    key={option.value}
                    type="button"
                    className={`so-dispatch-option-btn ${isActive ? "active" : ""}`}
                    onClick={() => setDispatchTagDraft(option.value)}
                    disabled={submittingDiagnosisRequest}
                  >
                    <span>{option.label}</span>
                    <small>{option.description}</small>
                  </button>
                );
              })}
            </div>

            <div className="so-dispatch-popup-actions">
              <button
                type="button"
                className="so-detail-action-btn so-detail-action-ghost"
                onClick={() => setShowDispatchPopup(false)}
                disabled={submittingDiagnosisRequest}
              >
                Hủy
              </button>
              <button
                type="button"
                className="so-detail-action-btn so-detail-action-primary"
                onClick={handleConfirmDiagnosisRequest}
                disabled={submittingDiagnosisRequest}
              >
                {submittingDiagnosisRequest ? "Đang gửi..." : "Gửi yêu cầu"}
              </button>
            </div>
          </div>
        </Modal>
      ) : null}
    </>
  );
}
