import React, { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Modal } from "@features/admin/components/AdminModals";
import { MdAssignment, MdCheckCircle, MdDirectionsCar, MdEdit, MdHistory, MdPerson } from "react-icons/md";
import { insuranceCompanyApi } from "@features/workshop/api/insuranceCompanyApi";
import InsuranceBillingOverview from "@features/workshop/components/InsuranceBillingOverview";
import { formatOrderDateTime, parseCustomerNotes } from "@features/workshop/utils/serviceOrderDetailUtils";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { vehicleConditionLogApi } from "@features/workshop/api/vehicleConditionLogApi";
import FilterDropdown from "@shared/components/FilterDropdown";
import { formatDetailPayerLabel, summarizeInsuranceOrderDetails } from "@features/workshop/utils/insuranceOrderUi";
import "../pages/Workshop.css";

const STATUS_LABELS = {
  NEW: "Mới tiếp nhận",
  RECEPTION: "Lễ tân tiếp nhận",
  DIAGNOSING: "Đang chuẩn đoán",
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

function formatDateTimeValue(value) {
  if (!value) return "--";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "--";
  return parsed.toLocaleString("vi-VN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  });
}

function buildIntakePackageLabel(item) {
  const code = String(item?.code || item?.Code || "").trim().toUpperCase();
  const name = String(item?.name || item?.Name || "").trim();
  const nominalMinutes = Number(item?.nominalMinutes ?? item?.NominalMinutes ?? 0);
  if (name && nominalMinutes > 0 && !name.includes(`(${nominalMinutes}p)`)) {
    return `${name} (${nominalMinutes}p)`;
  }
  return name || code || "--";
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
  const [insuranceCompanies, setInsuranceCompanies] = useState([]);
  const [loadingDetails, setLoadingDetails] = useState(false);
  const [diagnoses, setDiagnoses] = useState([]);
  const [liveOrderStatus, setLiveOrderStatus] = useState(() =>
    String(order?.status || "").trim().toUpperCase()
  );
  const [hasSignedCheckIn, setHasSignedCheckIn] = useState(false);
  const [selectedDispatchTag, setSelectedDispatchTag] = useState("ENGINE_CHASSIS");
  const [dispatchTagDraft, setDispatchTagDraft] = useState("ENGINE_CHASSIS");
  const [showDispatchPopup, setShowDispatchPopup] = useState(false);
  const [showDiagnosisReportPopup, setShowDiagnosisReportPopup] = useState(false);
  const [submittingDiagnosisRequest, setSubmittingDiagnosisRequest] = useState(false);
  const [loadingIntakeSuggestion, setLoadingIntakeSuggestion] = useState(false);
  const [intakeSuggestion, setIntakeSuggestion] = useState(null);
  const [intakePackages, setIntakePackages] = useState([]);
  const [bufferMinutesDraft, setBufferMinutesDraft] = useState(15);
  const [intakePackageDraft, setIntakePackageDraft] = useState("");
  const [bypassConflictGuard, setBypassConflictGuard] = useState(false);
  const [customerConsentAccepted, setCustomerConsentAccepted] = useState(false);
  const [customerConsentChannel, setCustomerConsentChannel] = useState("APP");
  const [customerConsentNote, setCustomerConsentNote] = useState("");

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
    setLiveOrderStatus(String(order?.status || "").trim().toUpperCase());
  }, [order?.id, order?.status]);

  useEffect(() => {
    const fetchDetailData = async () => {
      if (!order?.id) return;
      setLoadingDetails(true);
      try {
        const [orderRes, detailsRes, diagnosesRes, conditionLogsRes, insuranceRes, intakePackagesRes] = await Promise.all([
          serviceOrderApi.getById(order.id),
          serviceOrderApi.getDetails(order.id),
          serviceOrderApi.getDiagnoses(order.id),
          vehicleConditionLogApi.getByServiceOrderId(order.id),
          insuranceCompanyApi.getAll().catch(() => []),
          serviceOrderApi.getDiagnosisIntakePackages().catch(() => []),
        ]);

        setLiveOrderStatus(String(orderRes?.status || order?.status || "").trim().toUpperCase());
        setOrderDetails(Array.isArray(detailsRes) ? detailsRes : []);
        setDiagnoses(Array.isArray(diagnosesRes) ? diagnosesRes : []);
        setInsuranceCompanies(Array.isArray(insuranceRes) ? insuranceRes : []);
        setIntakePackages(Array.isArray(intakePackagesRes) ? intakePackagesRes : []);

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

  const intakePackageOptions = useMemo(() => {
    const dbOptions = (Array.isArray(intakePackages) ? intakePackages : [])
      .filter((item) => String(item?.code || item?.Code || "").trim())
      .map((item) => ({
        value: String(item?.code || item?.Code || "").trim().toUpperCase(),
        label: buildIntakePackageLabel(item),
      }));

    return [{ value: "", label: "Để hệ thống tự chọn" }, ...dbOptions];
  }, [intakePackages]);

  const intakePackageNameByCode = useMemo(() => {
    const map = new Map();
    (Array.isArray(intakePackages) ? intakePackages : []).forEach((item) => {
      const code = String(item?.code || item?.Code || "").trim().toUpperCase();
      if (!code) return;
      map.set(code, buildIntakePackageLabel(item));
    });
    return map;
  }, [intakePackages]);

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
  const isOrderDiagnosing = liveOrderStatus === "DIAGNOSING";
  const canRecallDiagnosisRequest = isDiagnosisRequested && !isOrderDiagnosing;
  const hasDiagnosisReport = isDiagnosisCompleted;
  const canViewQuote = isDiagnosisCompleted;

  const customerIssueText = useMemo(() => {
    if (symptomNoteItems.length > 0) return symptomNoteItems.join(" | ");
    return parsedNotes.mainNotes || "Không có ghi chú gì";
  }, [parsedNotes.mainNotes, symptomNoteItems]);

  const diagnosisSymptomText = firstText(
    latestDiagnosis?.symptom,
    latestDiagnosis?.Symptom,
    "Chưa có mô tả chuẩn đoán."
  );
  const diagnosisRootCauseText = firstText(
    latestDiagnosis?.rootCause,
    latestDiagnosis?.RootCause,
    "Chưa có nguyên nhân gốc."
  );
  const diagnosisSeverityText = firstText(
    latestDiagnosis?.severity,
    latestDiagnosis?.Severity,
    "--"
  );
  const diagnosisUpdatedAt = latestDiagnosis?.diagnosedAt
    || latestDiagnosis?.DiagnosedAt
    || latestDiagnosis?.updatedAt
    || latestDiagnosis?.UpdatedAt
    || null;

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

  const statusLabel = formatStatusLabel(liveOrderStatus || order?.status);
  const categoryLabel = formatOrderCategoryLabel(order?.orderCategory);
  const insuranceSummary = useMemo(
    () => summarizeInsuranceOrderDetails(orderDetails),
    [orderDetails]
  );
  const insuranceCompanyNames = useMemo(() => {
    if (!insuranceSummary.hasInsurance) return [];

    const companyNameById = new Map(
      (Array.isArray(insuranceCompanies) ? insuranceCompanies : []).map((company) => [
        Number(company?.id || company?.Id || 0),
        String(company?.name || company?.Name || "").trim(),
      ])
    );

    return insuranceSummary.companyIds
      .map((companyId) => companyNameById.get(Number(companyId)) || `Công ty #${companyId}`)
      .filter(Boolean);
  }, [insuranceCompanies, insuranceSummary]);

  const handleOpenPage = (path) => {
    onClose?.();
    navigate(path);
  };

  const refreshAfterDiagnosisChange = async () => {
    if (onRefresh) await onRefresh();
    const [orderRes, diagnosisRes] = await Promise.all([
      serviceOrderApi.getById(order.id),
      serviceOrderApi.getDiagnoses(order.id),
    ]);
    setLiveOrderStatus(String(orderRes?.status || "").trim().toUpperCase());
    setDiagnoses(Array.isArray(diagnosisRes) ? diagnosisRes : []);
  };

  const openVehicleLogPage = () => {
    handleOpenPage(`/workshop/reception/orders/${order.id}/vehicleconditionlogs`);
  };

  useEffect(() => {
    if (!showDispatchPopup || !order?.id) return;

    let isCancelled = false;
    const normalizedTag = DISPATCH_TAG_OPTIONS.some((item) => item.value === dispatchTagDraft)
      ? dispatchTagDraft
      : "ENGINE_CHASSIS";
    const normalizedBuffer = Number(bufferMinutesDraft);
    const safeBuffer = Number.isFinite(normalizedBuffer) ? Math.max(0, Math.min(120, normalizedBuffer)) : 15;

    const run = async () => {
      setLoadingIntakeSuggestion(true);
      try {
        const response = await serviceOrderApi.getDiagnosisIntakeSuggestion(order.id, {
          dispatchTag: normalizedTag,
          bufferMinutes: safeBuffer,
          intakePackageCode: intakePackageDraft || undefined,
        });

        if (isCancelled) return;
        setIntakeSuggestion(response || null);
        if (!intakePackageDraft && response?.finalPackageCode) {
          setIntakePackageDraft(String(response.finalPackageCode).toUpperCase());
        }
      } catch {
        if (!isCancelled) {
          setIntakeSuggestion(null);
        }
      } finally {
        if (!isCancelled) {
          setLoadingIntakeSuggestion(false);
        }
      }
    };

    void run();

    return () => {
      isCancelled = true;
    };
  }, [showDispatchPopup, order?.id, dispatchTagDraft, bufferMinutesDraft, intakePackageDraft]);

  const isConflictBlocked = useMemo(() => {
    const conflictLevel = String(intakeSuggestion?.conflictLevel || "").toUpperCase();
    return conflictLevel === "RED" && !bypassConflictGuard;
  }, [intakeSuggestion?.conflictLevel, bypassConflictGuard]);

  const intakeConflictLevel = String(intakeSuggestion?.conflictLevel || "").toUpperCase();
  const requiresCustomerConsent = Boolean(intakeSuggestion?.requiresCustomerConsent);
  const isConsentBlocked = requiresCustomerConsent && !customerConsentAccepted;

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
    setBufferMinutesDraft(15);
    setIntakePackageDraft("");
    setBypassConflictGuard(false);
    setCustomerConsentAccepted(false);
    setCustomerConsentChannel("APP");
    setCustomerConsentNote("");
    setIntakeSuggestion(null);
    setShowDispatchPopup(true);
  };

  const handleConfirmDiagnosisRequest = async () => {
    const normalizedTag = DISPATCH_TAG_OPTIONS.some((item) => item.value === dispatchTagDraft)
      ? dispatchTagDraft
      : "ENGINE_CHASSIS";
    const normalizedBuffer = Number(bufferMinutesDraft);
    const safeBuffer = Number.isFinite(normalizedBuffer) ? Math.max(0, Math.min(120, normalizedBuffer)) : 15;
    const selectedPackageCode = String(intakePackageDraft || intakeSuggestion?.finalPackageCode || "").trim().toUpperCase();
    if (requiresCustomerConsent && !customerConsentAccepted) {
      alert("Cần xác nhận đồng ý của khách trước khi tạo yêu cầu gói chuẩn đoán này.");
      return;
    }

    setSubmittingDiagnosisRequest(true);
    try {
      await serviceOrderApi.requestDiagnosis(order.id, {
        dispatchTag: normalizedTag,
        autoAssign: true,
        enforceCapacityGuard: true,
        bypassConflictGuard,
        bufferMinutes: safeBuffer,
        intakePackageCode: selectedPackageCode || undefined,
        customerConsentAccepted: requiresCustomerConsent ? customerConsentAccepted : undefined,
        customerConsentChannel: requiresCustomerConsent ? customerConsentChannel : undefined,
        customerConsentNote: requiresCustomerConsent ? customerConsentNote : undefined,
      });
      setSelectedDispatchTag(normalizedTag);
      setShowDispatchPopup(false);
      setIntakeSuggestion(null);
      await refreshAfterDiagnosisChange();
    } catch (error) {
      alert(`Lỗi khi yêu cầu chuẩn đoán: ${error?.message || "Không xác định"}`);
    } finally {
      setSubmittingDiagnosisRequest(false);
    }
  };

  const handleRecallDiagnosis = async () => {
    if (isOrderDiagnosing) {
      alert("SO đã chuyển sang trạng thái Đang chuẩn đoán, không thể thu hồi yêu cầu.");
      return;
    }
    const confirmed = window.confirm("Thu hồi yêu cầu chuẩn đoán?");
    if (!confirmed) return;
    try {
      await serviceOrderApi.recallDiagnosisRequest(order.id);
      await refreshAfterDiagnosisChange();
    } catch (error) {
      alert(`Lỗi khi thu hồi yêu cầu chuẩn đoán: ${error?.message || "Không xác định"}`);
    }
  };

  const handleOpenDiagnosisReport = () => {
    if (!hasDiagnosisReport) return;
    setShowDiagnosisReportPopup(true);
  };

  const diagnosisHint = useMemo(() => {
    if (hasDiagnosisReport) return "Đã hoàn tất chuẩn đoán. Có thể xem báo giá.";
    if (isOrderDiagnosing) return "Đang chuẩn đoán.";
    if (canRecallDiagnosisRequest) {
      return "Đang chuẩn đoán (đang chờ kỹ thuật viên nhận)";
    }
    if (isDiagnosisAssigned) return "Kỹ thuật viên đang thực hiện chuẩn đoán.";
    if (!hasSignedCheckIn && !isDiagnosisCompleted && !isDiagnosisRequested) {
      return "Cần hoàn tất Nhật ký xe và chữ ký CHECK-IN trước khi gửi yêu cầu chuẩn đoán.";
    }
    if (!isDiagnosisCompleted && !isDiagnosisRequested) {
      return "Khi bấm \"Yêu cầu chuẩn đoán\", hệ thống sẽ mở popup để chọn chuyên môn điều phối.";
    }
    return "";
  }, [canRecallDiagnosisRequest, hasDiagnosisReport, hasSignedCheckIn, isDiagnosisAssigned, isDiagnosisCompleted, isDiagnosisRequested, isOrderDiagnosing]);

  const fallbackMainNote = String(parsedNotes.mainNotes || "").toLowerCase().includes("không có")
    ? "Không có ghi chú gì"
    : String(parsedNotes.mainNotes || "").trim();

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

          <InsuranceBillingOverview
            summary={insuranceSummary}
            companyNames={insuranceCompanyNames}
            note="Các khoản khách trả trong phần này đã bao gồm miễn thường, hồ sơ, khấu hao hoặc các dòng dịch vụ đang để payer là khách hàng."
          />

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
                        <div className="so-detail-service-copy">
                          <span>{detail.itemName}</span>
                          <small>{formatDetailPayerLabel(detail)}</small>
                        </div>
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
              {hasDiagnosisReport ? (
                <button
                  type="button"
                  className="so-detail-action-btn so-detail-action-primary"
                  onClick={handleOpenDiagnosisReport}
                >
                  Xem chuẩn đoán
                </button>
              ) : canRecallDiagnosisRequest ? (
                <button
                  type="button"
                  className="so-detail-action-btn so-detail-action-warn"
                  onClick={handleRecallDiagnosis}
                >
                  Thu hồi yêu cầu
                </button>
              ) : isOrderDiagnosing ? (
                <button
                  type="button"
                  className="so-detail-action-btn so-detail-action-primary"
                  disabled
                >
                  Đang chuẩn đoán
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
                disabled={!canViewQuote}
                title={!canViewQuote ? "Cần hoàn tất chuẩn đoán trước khi xem báo giá." : ""}
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

            <div className="so-dispatch-config-row">
              <label className="so-dispatch-config-item">
                <span>Buffer (phút)</span>
                <input
                  type="number"
                  min={0}
                  max={120}
                  value={bufferMinutesDraft}
                  onChange={(event) => setBufferMinutesDraft(event.target.value)}
                  disabled={submittingDiagnosisRequest}
                />
              </label>
              <label className="so-dispatch-config-item">
                <span>Gói chuẩn đoán</span>
                <FilterDropdown
                  id={`intake-package-${order?.id || "draft"}`}
                  value={intakePackageDraft}
                  options={intakePackageOptions}
                  onChange={(nextValue) => setIntakePackageDraft(String(nextValue || "").toUpperCase())}
                  minWidth={0}
                  menuMinWidth={280}
                  disabled={submittingDiagnosisRequest}
                  ariaLabel="Chọn gói chuẩn đoán"
                  optionNoWrap
                  fullWidth
                />
              </label>
            </div>

            <div className="so-intake-card">
              {loadingIntakeSuggestion ? (
                <p className="so-intake-loading">Đang tính gói đề xuất...</p>
              ) : intakeSuggestion ? (
                <>
                  <div className="so-intake-row">
                    <span>Gói đề xuất</span>
                    <strong>
                      {intakePackageNameByCode.get(String(intakeSuggestion?.finalPackageCode || "").trim().toUpperCase())
                        || String(intakeSuggestion?.finalPackageName || "").trim()
                        || String(intakeSuggestion?.finalPackageCode || "").trim().toUpperCase()
                        || "--"}
                    </strong>
                  </div>
                  <div className="so-intake-row">
                    <span>T1 dự kiến</span>
                    <strong>{formatDateTimeValue(intakeSuggestion.t1EstimatedHandover)}</strong>
                  </div>
                  <div className="so-intake-row">
                    <span>Công thức thời gian</span>
                    <strong>
                      {`${intakeSuggestion.mainServiceMinutes || 0}p dịch vụ + ${intakeSuggestion.bufferMinutes || 0}p buffer + ${intakeSuggestion.packageEffectiveMinutes || 0}p gói`}
                    </strong>
                  </div>
                  <div className="so-intake-row">
                    <span>Xung đột booking</span>
                    <strong className={`so-intake-badge ${String(intakeConflictLevel || "").toLowerCase()}`}>
                      {`${intakeConflictLevel || "GREEN"}${intakeSuggestion.conflictMinutes > 0 ? ` (${intakeSuggestion.conflictMinutes}p)` : ""}`}
                    </strong>
                  </div>
                  {Array.isArray(intakeSuggestion.reasons) && intakeSuggestion.reasons.length > 0 ? (
                    <ul className="so-intake-reason-list">
                      {intakeSuggestion.reasons.slice(0, 3).map((reason, idx) => (
                        <li key={`${reason}-${idx}`}>{reason}</li>
                      ))}
                    </ul>
                  ) : null}
                  {intakeSuggestion.suggestedLeadTechnicianName ? (
                    <p className="so-intake-assign-text">
                      Auto-assign: {intakeSuggestion.suggestedLeadTechnicianName}
                      {intakeSuggestion.suggestedAssistantTechnicianName ? ` + ${intakeSuggestion.suggestedAssistantTechnicianName}` : ""}
                      {intakeSuggestion.suggestedTeamName ? ` (${intakeSuggestion.suggestedTeamName})` : ""}
                    </p>
                  ) : null}
                </>
              ) : (
                <p className="so-intake-loading">Không lấy được gói đề xuất. Bạn vẫn có thể gửi yêu cầu thủ công.</p>
              )}
            </div>

            {requiresCustomerConsent ? (
              <div className="so-consent-card">
                <label className="so-consent-check">
                  <input
                    type="checkbox"
                    checked={customerConsentAccepted}
                    onChange={(event) => setCustomerConsentAccepted(Boolean(event.target.checked))}
                    disabled={submittingDiagnosisRequest}
                  />
                  <span>Khách đã đồng ý thực hiện gói chuẩn đoán cộng thêm thời gian.</span>
                </label>
                <div className="so-consent-grid">
                  <label className="so-dispatch-config-item">
                    <span>Kênh xác nhận</span>
                    <select
                      value={customerConsentChannel}
                      onChange={(event) => setCustomerConsentChannel(String(event.target.value || "APP").toUpperCase())}
                      disabled={submittingDiagnosisRequest}
                    >
                      <option value="APP">Ứng dụng</option>
                      <option value="ZALO">Zalo</option>
                      <option value="PHONE">Điện thoại</option>
                      <option value="DIRECT">Trực tiếp tại quầy</option>
                    </select>
                  </label>
                  <label className="so-dispatch-config-item">
                    <span>Ghi chú xác nhận</span>
                    <input
                      type="text"
                      value={customerConsentNote}
                      onChange={(event) => setCustomerConsentNote(event.target.value)}
                      placeholder="Ví dụ: Khách đồng ý cộng thêm 30 phút"
                      disabled={submittingDiagnosisRequest}
                    />
                  </label>
                </div>
              </div>
            ) : null}

            {intakeConflictLevel === "RED" ? (
              <label className="so-bypass-toggle">
                <input
                  type="checkbox"
                  checked={bypassConflictGuard}
                  onChange={(event) => setBypassConflictGuard(Boolean(event.target.checked))}
                  disabled={submittingDiagnosisRequest}
                />
                <span>Bypass canh bao do (chi dung khi khach rat voi)</span>
              </label>
            ) : null}

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
                disabled={submittingDiagnosisRequest || loadingIntakeSuggestion || isConflictBlocked || isConsentBlocked}
              >
                {submittingDiagnosisRequest
                  ? "Đang gửi..."
                  : isConflictBlocked
                    ? "Đang bị chặn bởi conflict đỏ"
                    : isConsentBlocked
                      ? "Chờ khách xác nhận"
                      : "Gửi yêu cầu"}
              </button>
            </div>
          </div>
        </Modal>
      ) : null}

      {showDiagnosisReportPopup ? (
        <Modal
          title="Kết quả chuẩn đoán"
          onClose={() => setShowDiagnosisReportPopup(false)}
          className="so-diagnosis-report-modal"
        >
          <div className="so-dispatch-popup">
            <div className="so-intake-card">
              <div className="so-intake-row">
                <span>Vấn đề khách mô tả</span>
                <strong>{customerIssueText}</strong>
              </div>
              <div className="so-intake-row">
                <span>Lý do / hiện tượng ghi nhận</span>
                <strong>{diagnosisSymptomText}</strong>
              </div>
              <div className="so-intake-row">
                <span>Nguyên nhân gốc</span>
                <strong>{diagnosisRootCauseText}</strong>
              </div>
              <div className="so-intake-row">
                <span>Mức độ</span>
                <strong>{diagnosisSeverityText}</strong>
              </div>
              <div className="so-intake-row">
                <span>Cập nhật lúc</span>
                <strong>{formatDateTimeValue(diagnosisUpdatedAt)}</strong>
              </div>
            </div>

            <div className="so-dispatch-popup-actions">
              <button
                type="button"
                className="so-detail-action-btn so-detail-action-ghost"
                onClick={() => setShowDiagnosisReportPopup(false)}
              >
                Đóng
              </button>
              <button
                type="button"
                className="so-detail-action-btn so-detail-action-purple"
                onClick={() => handleOpenPage(`/workshop/reception/orders/${order.id}/quote`)}
                disabled={!canViewQuote}
              >
                Xem báo giá
              </button>
            </div>
          </div>
        </Modal>
      ) : null}
    </>
  );
}
