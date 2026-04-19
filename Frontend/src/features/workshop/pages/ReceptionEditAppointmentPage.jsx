import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import {
  getAllCustomers,
  getAllCustomerVehicles,
  getAllVehicles,
} from "@features/admin/api/customerApi";
import {
  getAllServiceCategories,
  getAllPackages,
  getAllStandardServices,
  getAppointmentDetail,
  getAppointmentEditCapabilities,
  getAppointmentEditRequests,
  decideAppointmentEditRequest,
  requestAppointmentEdit,
} from "@features/appointments/api/appointmentApi";
import ReceptionCreateAppointmentForm from "@features/workshop/components/ReceptionCreateAppointmentForm";
import { saveReceptionAppointment } from "@features/workshop/utils/receptionAppointmentBooking";
import { mergeBookableServicesAndPackages, toPackageSelectableId } from "@features/appointments/utils/bookingPackages";
import { useAuth } from "@shared/core/context/AuthContext";
import { useBranch } from "@shared/core/context/BranchContext";
import "./Workshop.css";

const TEXT = {
  invalidId: "Mã lịch hẹn không hợp lệ.",
  loadError: "Không thể tải dữ liệu sửa lịch hẹn.",
  notFound: "Không tìm thấy lịch hẹn cần sửa.",
  needBranch: "Vui lòng chọn dòng chi nhánh làm việc trước khi sửa lịch hẹn.",
  updateError: "Không thể cập nhật lịch hẹn.",
  notEditableStatus: "Không thể sửa lịch hẹn ? trạng thái hiện tại.",
  notEditableWindow: "Không thể sửa lịch hẹn vì xe đã đến xưởng.",
  loadingData: "Đang tải dữ liệu lịch hẹn...",
  pageTitle: "Chỉnh sửa lịch hẹn (Lễ tân)",
  back: "Quay lại",
  submit: "Lưu cập nhật",
  successTitle: "Cập nhật lịch hẹn thành công",
  successMessage: "Hệ thống đã tạo lịch mới và thay thế lịch cũ.",
  requestEditReasonRequired: "Vui lòng nhập lý do yêu cầu sửa trước khi gửi.",
  requestEditError: "Không thể gửi yêu cầu sửa lịch hẹn.",
  requestEditSuccess: "Đã gửi yêu cầu sửa lịch hẹn đến cấp phê duyệt.",
  requestEditSubmit: "Gửi yêu cầu sửa",
  requestEditSubmitting: "Đang gửi yêu cầu...",
  requestListTitle: "Lịch sử yêu cầu mở khóa",
  noRequestReason: "Không có lý do",
  decisionRejectNeedsNote: "Vui lòng nhập lý do từ chối.",
  decisionApproveSuccess: "Đã duyệt yêu cầu sửa lịch hẹn.",
  decisionRejectSuccess: "Đã từ chối yêu cầu sửa lịch hẹn.",
  decisionError: "Không thể xử lý yêu cầu sửa lịch hẹn.",
  approveButton: "Duyệt",
  rejectButton: "Từ chối",
  decisionNotePlaceholder: "Ghi chú phê duyệt/từ chối...",
  overrideActivePrefix: "Đang mở khóa tạm thời đến:",};

function parsePositiveId(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
}

function parseBranchId(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
}

function normalizeStatus(status) {
  return String(status || "").trim().toUpperCase();
}
function normalizeRoleToken(value) {
  return String(value || "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/gi, "d")
    .trim()
    .toUpperCase()
    .replace(/-/g, "_")
    .replace(/\s+/g, "_");
}

function containsRoleKeyword(roleCode, roleName, keywords) {
  const normalizedCode = normalizeRoleToken(roleCode);
  const normalizedName = normalizeRoleToken(roleName);
  return keywords.some((keyword) => {
    const token = normalizeRoleToken(keyword);
    return normalizedCode.includes(token) || normalizedName.includes(token);
  });
}

function canApproveAppointmentRequest(roleCode, roleName) {
  return containsRoleKeyword(roleCode, roleName, ["MANAGER", "SERVICE_MANAGER", "FOREMAN", "ADMIN", "OWNER", "DIRECTOR", "QUAN_DOC"]);
}

function canEditAppointmentStatus(status) {
  const normalized = normalizeStatus(status);
  return normalized === "PENDING" || normalized === "CONFIRMED" || normalized === "LATE";
}

function hasAppointmentWindowPassed(appointment) {
  const normalizedStatus = normalizeStatus(appointment?.status ?? appointment?.Status);
  return ["ARRIVED", "ARRIVED_LATE"].includes(normalizedStatus);
}

function normalizeAppointmentEditCapability(payload) {
  if (!payload || typeof payload !== "object") return null;
  const read = (...keys) => {
    for (const key of keys) {
      const value = payload?.[key];
      if (value !== undefined && value !== null) return value;
    }
    return null;
  };

  const canEditRaw = read("canEdit", "CanEdit");
  const statusLockedRaw = read("isStatusLocked", "IsStatusLocked");
  const windowPassedRaw = read("isWindowPassed", "IsWindowPassed");
  const overrideActiveRaw = read("isOverrideActive", "IsOverrideActive");
  const overrideRequestIdRaw = read("overrideRequestId", "OverrideRequestId");
  const overrideUntilUtcRaw = read("overrideUntilUtc", "OverrideUntilUtc");

  return {
    canEdit: Boolean(canEditRaw),
    isStatusLocked: Boolean(statusLockedRaw),
    isWindowPassed: Boolean(windowPassedRaw),
    isOverrideActive: Boolean(overrideActiveRaw),
    overrideRequestId: Number(overrideRequestIdRaw) > 0 ? Number(overrideRequestIdRaw) : null,
    overrideUntilUtc: overrideUntilUtcRaw || null,
    blockingReason: String(read("blockingReason", "BlockingReason") || "").trim(),
  };
}

function normalizeAppointmentEditRequestItem(raw) {
  const source = raw || {};
  const read = (...keys) => {
    for (const key of keys) {
      const value = source?.[key];
      if (value !== undefined && value !== null) return value;
    }
    return null;
  };

  const requestId = Number(read("requestId", "RequestId", "id", "Id") || 0);
  return {
    requestId: Number.isFinite(requestId) && requestId > 0 ? requestId : 0,
    section: String(read("section", "Section") || "APPOINTMENT").trim().toUpperCase(),
    reason: String(read("reason", "Reason") || "").trim(),
    status: String(read("status", "Status") || "PENDING").trim().toUpperCase(),
    requestedByUserId: Number(read("requestedByUserId", "RequestedByUserId", "userId", "UserId") || 0),
    requestedAtUtc: read("requestedAtUtc", "RequestedAtUtc", "timestamp", "Timestamp"),
    decidedByUserId: Number(read("decidedByUserId", "DecidedByUserId") || 0),
    decidedAtUtc: read("decidedAtUtc", "DecidedAtUtc"),
    decisionNote: String(read("decisionNote", "DecisionNote") || "").trim(),
    decidedByRoleCode: String(read("decidedByRoleCode", "DecidedByRoleCode") || "").trim(),
    overrideUntilUtc: read("overrideUntilUtc", "OverrideUntilUtc"),
  };
}

function formatDateTimeVi(value) {
  if (!value) return "--";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "--";
  return parsed.toLocaleString("vi-VN", {
    hour: "2-digit",
    minute: "2-digit",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    timeZone: "Asia/Ho_Chi_Minh",
  });
}

function getRequestStatusLabel(status) {
  const normalized = String(status || "").trim().toUpperCase();
  if (normalized === "APPROVED") return "Đã duyệt";
  if (normalized === "REJECTED") return "Đã từ chối";
  return "Chờ duyệt";
}

function getRequestStatusColor(status) {
  const normalized = String(status || "").trim().toUpperCase();
  if (normalized === "APPROVED") return "#166534";
  if (normalized === "REJECTED") return "#b91c1c";
  return "#9a3412";
}
function normalizeAppointmentItem(item) {
  const source = item || {};
  const pick = (...keys) => {
    for (const key of keys) {
      const value = source?.[key];
      if (value !== undefined && value !== null) return value;
    }
    return null;
  };
  const numeric = (value) => {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  };
  const text = (value) => (value === undefined || value === null ? "" : String(value));
  const stringList = (value) =>
    Array.isArray(value)
      ? value.map((entry) => String(entry || "").trim()).filter(Boolean)
      : [];

  return {
    ...source,
    id: numeric(pick("id", "Id")),
    branchId: numeric(pick("branchId", "BranchId")),
    customerId: numeric(pick("customerId", "CustomerId")),
    customerName: text(pick("customerName", "CustomerName", "customerFullName", "CustomerFullName")),
    customerPhone: text(pick("customerPhone", "CustomerPhone")),
    startTime: pick("startTime", "StartTime"),
    endTime: pick("endTime", "EndTime"),
    status: text(pick("status", "Status")),
    vehicleId: numeric(pick("vehicleId", "VehicleId")),
    requestedServiceIds: text(pick("requestedServiceIds", "RequestedServiceIds")),
    requestedPackageIds: text(pick("requestedPackageIds", "RequestedPackageIds")),
    customerNotes: text(pick("customerNotes", "CustomerNotes")),
    insuranceCompanyId: numeric(pick("insuranceCompanyId", "InsuranceCompanyId")),
    insuranceDeductibleAmount: pick("insuranceDeductibleAmount", "InsuranceDeductibleAmount"),
    insuranceDeductibleOption: text(pick("insuranceDeductibleOption", "InsuranceDeductibleOption")),
    insuranceClaimMode: text(pick("insuranceClaimMode", "InsuranceClaimMode")),
    insuranceQuoteState: text(pick("insuranceQuoteState", "InsuranceQuoteState")),
    insuranceCardImageUrls: stringList(pick("insuranceCardImageUrls", "InsuranceCardImageUrls")),
    insuranceDamageImageUrls: stringList(pick("insuranceDamageImageUrls", "InsuranceDamageImageUrls")),
  };
}

function parseRequestedServiceIds(value) {
  return Array.from(
    new Set(
      String(value || "")
        .split(/[,|]/g)
        .map((item) => Number(String(item || "").trim()))
        .filter((id) => Number.isFinite(id) && id > 0)
    )
  );
}

function buildEditInitialValues(appointment) {
  if (!appointment) return null;

  const serviceIds = parseRequestedServiceIds(appointment.requestedServiceIds);
  const packageSelectableIds = parseRequestedServiceIds(appointment.requestedPackageIds)
    .map(toPackageSelectableId)
    .filter((id) => id !== 0);
  const selectedItemIds = [...serviceIds, ...packageSelectableIds];
  const vehicleId = Number(appointment.vehicleId || 0);
  const selectedVehicleIds = vehicleId > 0 ? [vehicleId] : [];
  const vehicleServicePlans = vehicleId > 0
    ? [{
      vehicleKey: `existing-${vehicleId}`,
      vehicleId,
      serviceIds: selectedItemIds,
    }]
    : [];
  const vehicleInsurancePlans =
    vehicleId > 0 && (
      Number(appointment.insuranceCompanyId || 0) > 0 ||
      String(appointment.insuranceDeductibleOption || "").trim() ||
      String(appointment.insuranceClaimMode || "").trim() ||
      String(appointment.insuranceQuoteState || "").trim() ||
      (Array.isArray(appointment.insuranceCardImageUrls) && appointment.insuranceCardImageUrls.length > 0) ||
      (Array.isArray(appointment.insuranceDamageImageUrls) && appointment.insuranceDamageImageUrls.length > 0)
    )
      ? [{
        vehicleKey: `existing-${vehicleId}`,
        vehicleId,
        insuranceCompanyId: Number(appointment.insuranceCompanyId || 0) || "",
          insuranceDeductibleAmount: appointment.insuranceDeductibleAmount ?? null,
        insuranceDeductibleOption: appointment.insuranceDeductibleOption || "",
        insuranceClaimMode: appointment.insuranceClaimMode || "",
        insuranceQuoteState: appointment.insuranceQuoteState || "",
        insuranceCardImageUrls: Array.isArray(appointment.insuranceCardImageUrls) ? appointment.insuranceCardImageUrls : [],
        insuranceDamageImageUrls: Array.isArray(appointment.insuranceDamageImageUrls) ? appointment.insuranceDamageImageUrls : [],
      }]
      : [];

  return {
    customerId: Number(appointment.customerId || 0) || "",
    customerName: appointment.customerName || "",
    customerPhone: appointment.customerPhone || "",
    selectedVehicleIds,
    startTime: appointment.startTime || "",
    endTime: appointment.endTime || "",
    requestedServiceIds: appointment.requestedServiceIds || "",
    requestedPackageIds: appointment.requestedPackageIds || "",
    serviceIds: selectedItemIds,
    vehicleServicePlans,
    vehicleInsurancePlans,
    customerNotes: appointment.customerNotes || "",
  };
}

function formatCurrentSlotText(appointment) {
  const start = new Date(appointment?.startTime || "");
  const end = new Date(appointment?.endTime || "");
  if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) return "";

  const date = start.toLocaleDateString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" });
  const startTime = start.toLocaleTimeString("vi-VN", {
    hour: "2-digit",
    minute: "2-digit",
    timeZone: "Asia/Ho_Chi_Minh",
  });
  const endTime = end.toLocaleTimeString("vi-VN", {
    hour: "2-digit",
    minute: "2-digit",
    timeZone: "Asia/Ho_Chi_Minh",
  });
  return `Lịch hiện tại: ${date}, ${startTime} - ${endTime}`;
}

export default function ReceptionEditAppointmentPage() {
  const navigate = useNavigate();
  const { appointmentId: appointmentIdParam } = useParams();
  const { selectedBranchId } = useBranch();
  const { user } = useAuth();

  const appointmentId = parsePositiveId(appointmentIdParam);

  const [customers, setCustomers] = useState([]);
  const [vehicles, setVehicles] = useState([]);
  const [customerVehicles, setCustomerVehicles] = useState([]);
  const [services, setServices] = useState([]);
  const [serviceCategories, setServiceCategories] = useState([]);
  const [appointment, setAppointment] = useState(null);
  const [editCapability, setEditCapability] = useState(null);
  const [initialValues, setInitialValues] = useState(null);

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [requestingEdit, setRequestingEdit] = useState(false);
  const [error, setError] = useState("");
  const [successAppt, setSuccessAppt] = useState(null);
  const [requestEditReason, setRequestEditReason] = useState("");
  const [requestEditSuccess, setRequestEditSuccess] = useState("");
  const [editRequests, setEditRequests] = useState([]);
  const [loadingEditRequests, setLoadingEditRequests] = useState(false);
  const [decidingRequestId, setDecidingRequestId] = useState(0);
  const [decisionNotesByRequestId, setDecisionNotesByRequestId] = useState({});

  const userBranchId = parseBranchId(user?.branchId ?? user?.BranchId);
  const contextBranchId = parseBranchId(selectedBranchId);
  const effectiveBranchId = contextBranchId || userBranchId;
  const targetBranchId = parseBranchId(appointment?.branchId) || effectiveBranchId;
  const roleCode = String(user?.roleCode ?? user?.RoleCode ?? "").trim();
  const roleName = String(user?.roleName ?? user?.RoleName ?? "").trim();
  const canApproveRequests = canApproveAppointmentRequest(roleCode, roleName);

  const hasBranch = targetBranchId > 0;
  const canSubmit = hasBranch && appointmentId > 0 && Boolean(appointment);
  const fallbackWindowPassed = hasAppointmentWindowPassed(appointment);
  const fallbackCanEdit = canEditAppointmentStatus(appointment?.status) && !fallbackWindowPassed;
  const canEdit = editCapability ? Boolean(editCapability.canEdit) : fallbackCanEdit;
  const editBlockedMessage = editCapability?.blockingReason
    || (editCapability?.isWindowPassed || fallbackWindowPassed ? TEXT.notEditableWindow : TEXT.notEditableStatus);
  const currentSlotText = useMemo(() => formatCurrentSlotText(appointment), [appointment]);
  const hasPendingRequest = useMemo(
    () => editRequests.some((item) => String(item?.status || "").toUpperCase() === "PENDING"),
    [editRequests]
  );
  const overrideUntilText = useMemo(() => formatDateTimeVi(editCapability?.overrideUntilUtc), [editCapability?.overrideUntilUtc]);

  const fetchPageData = useCallback(async () => {
    if (!appointmentId) {
      setError(TEXT.invalidId);
      return;
    }

    setLoading(true);
    setLoadingEditRequests(true);
    setError("");
    try {
      const [detailRes, capabilityRes, requestRes, customerRes, vehicleRes, customerVehicleRes, serviceRes, packageRes, serviceCategoryRes] = await Promise.all([
        getAppointmentDetail(appointmentId),
        getAppointmentEditCapabilities(appointmentId).catch(() => null),
        getAppointmentEditRequests(appointmentId).catch(() => []),
        getAllCustomers(),
        getAllVehicles(),
        getAllCustomerVehicles(),
        getAllStandardServices(),
        getAllPackages().catch(() => []),
        getAllServiceCategories(),
      ]);

      const normalizedAppt = normalizeAppointmentItem(detailRes);
      if (!normalizedAppt?.id) {
        throw new Error(TEXT.notFound);
      }

      setAppointment(normalizedAppt);
      setEditCapability(normalizeAppointmentEditCapability(capabilityRes));
      setInitialValues(buildEditInitialValues(normalizedAppt));
      setCustomers(Array.isArray(customerRes) ? customerRes : []);
      setVehicles(Array.isArray(vehicleRes) ? vehicleRes : []);
      setCustomerVehicles(Array.isArray(customerVehicleRes) ? customerVehicleRes : []);
      setServices(mergeBookableServicesAndPackages(Array.isArray(serviceRes) ? serviceRes : [], Array.isArray(packageRes) ? packageRes : []));
      setServiceCategories(Array.isArray(serviceCategoryRes) ? serviceCategoryRes : []);
      setEditRequests(
        (Array.isArray(requestRes) ? requestRes : [])
          .map(normalizeAppointmentEditRequestItem)
          .filter((item) => item.requestId > 0)
      );
    } catch (err) {
      setError(err?.message || TEXT.loadError);
    } finally {
      setLoading(false);
      setLoadingEditRequests(false);
    }
  }, [appointmentId]);

  useEffect(() => {
    fetchPageData();
  }, [fetchPageData]);

  const handleGoBack = useCallback(() => {
    navigate("/workshop/reception/appointments");
  }, [navigate]);

  useEffect(() => {
    if (!successAppt) return undefined;
    const timeoutId = setTimeout(() => {
      navigate("/workshop/reception/appointments", { replace: true });
    }, 2000);
    return () => clearTimeout(timeoutId);
  }, [successAppt, navigate]);

  useEffect(() => {
    if (!error && (!appointment || canSubmit)) return;
    if (typeof window !== "undefined" && typeof window.scrollTo === "function") {
      window.scrollTo({ top: 0, behavior: "smooth" });
    }
  }, [appointment, error, canSubmit]);

  const handleSubmit = useCallback(
    async (formData) => {
      if (!canSubmit) {
        setError(TEXT.needBranch);
        return;
      }

      const latestCapabilityRaw = await getAppointmentEditCapabilities(appointment.id).catch(() => null);
      const latestCapability = normalizeAppointmentEditCapability(latestCapabilityRaw);
      if (latestCapability && !latestCapability.canEdit) {
        setEditCapability(latestCapability);
        setError(latestCapability.blockingReason || TEXT.notEditableWindow);
        return;
      }

      const windowPassedAtSubmit = hasAppointmentWindowPassed(appointment);
      const statusEditable = canEditAppointmentStatus(appointment?.status);
      if (!statusEditable || windowPassedAtSubmit) {
        setError(windowPassedAtSubmit ? TEXT.notEditableWindow : TEXT.notEditableStatus);
        return;
      }

      setSaving(true);
      setError("");
      try {
        const summary = await saveReceptionAppointment({
          effectiveBranchId: targetBranchId,
          formData,
          customers,
          vehicles,
          customerVehicles,
          services,
          replaceAppointmentId: appointment.id,
        });
        setSuccessAppt(summary);
      } catch (err) {
        const message = err?.message || TEXT.updateError;
        setError(message);
        throw err;
      } finally {
        setSaving(false);
      }
    },
    [appointment, canSubmit, customerVehicles, customers, services, targetBranchId, vehicles],
  );

  const handleRequestEdit = useCallback(async () => {
    if (!appointment?.id) return;
    const reason = String(requestEditReason || "").trim();
    if (!reason) {
      setError(TEXT.requestEditReasonRequired);
      return;
    }

    setRequestingEdit(true);
    setError("");
    setRequestEditSuccess("");
    try {
      const response = await requestAppointmentEdit(appointment.id, {
        section: "APPOINTMENT",
        reason,
      });
      const [latestCapabilityRaw, latestRequestsRaw] = await Promise.all([
        getAppointmentEditCapabilities(appointment.id).catch(() => null),
        getAppointmentEditRequests(appointment.id).catch(() => []),
      ]);
      setEditCapability(normalizeAppointmentEditCapability(latestCapabilityRaw));
      setEditRequests(
        (Array.isArray(latestRequestsRaw) ? latestRequestsRaw : [])
          .map(normalizeAppointmentEditRequestItem)
          .filter((item) => item.requestId > 0)
      );
      setRequestEditSuccess(String(response?.message || TEXT.requestEditSuccess));
      setRequestEditReason("");
    } catch (err) {
      setError(err?.message || TEXT.requestEditError);
    } finally {
      setRequestingEdit(false);
    }
  }, [appointment?.id, requestEditReason]);

  const handleDecisionNoteChange = useCallback((requestId, value) => {
    const safeId = Number(requestId);
    if (!Number.isFinite(safeId) || safeId <= 0) return;
    setDecisionNotesByRequestId((prev) => ({
      ...prev,
      [safeId]: value,
    }));
  }, []);

  const handleDecideRequest = useCallback(async (requestId, approve) => {
    if (!appointment?.id) return;
    const safeRequestId = Number(requestId);
    if (!Number.isFinite(safeRequestId) || safeRequestId <= 0) return;

    const note = String(decisionNotesByRequestId[safeRequestId] || "").trim();
    if (!approve && !note) {
      setError(TEXT.decisionRejectNeedsNote);
      return;
    }

    setDecidingRequestId(safeRequestId);
    setError("");
    setRequestEditSuccess("");
    try {
      const response = await decideAppointmentEditRequest(appointment.id, safeRequestId, {
        decision: approve ? "APPROVE" : "REJECT",
        note: note || null,
      });

      const [latestCapabilityRaw, latestRequestsRaw] = await Promise.all([
        getAppointmentEditCapabilities(appointment.id).catch(() => null),
        getAppointmentEditRequests(appointment.id).catch(() => []),
      ]);
      setEditCapability(normalizeAppointmentEditCapability(latestCapabilityRaw));
      setEditRequests(
        (Array.isArray(latestRequestsRaw) ? latestRequestsRaw : [])
          .map(normalizeAppointmentEditRequestItem)
          .filter((item) => item.requestId > 0)
      );
      setRequestEditSuccess(String(response?.message || (approve ? TEXT.decisionApproveSuccess : TEXT.decisionRejectSuccess)));
      setDecisionNotesByRequestId((prev) => ({
        ...prev,
        [safeRequestId]: "",
      }));
    } catch (err) {
      setError(err?.message || TEXT.decisionError);
    } finally {
      setDecidingRequestId(0);
    }
  }, [appointment?.id, decisionNotesByRequestId]);

  const editAppointmentFormId = "reception-edit-appointment-form";

  return (
    <div className="create-order-page reception-create-appointment-page reception-edit-appointment-page">
      <div className="page-header-actions reception-create-header">
        <button className="btn-back" type="button" onClick={handleGoBack}>
          {TEXT.back}
        </button>
        <div className="reception-create-header-copy">
          <h2 className="page-title">{TEXT.pageTitle}</h2>
          {appointment?.id ? (
            <p className="reception-create-header-note">
              {`Mã lịch hẹn #${appointment.id}`}
              {currentSlotText ? ` - ${currentSlotText}` : ""}
            </p>
          ) : null}
        </div>
        {appointment && initialValues && canEdit ? (
          <button
            type="submit"
            form={editAppointmentFormId}
            className="btn-primary-large reception-header-submit-btn"
            disabled={saving}
          >
            {saving ? "Đang xử lý..." : TEXT.submit}
          </button>
        ) : null}
      </div>

      {appointment && !hasBranch && <div className="validation-summary">{TEXT.needBranch}</div>}
      {error && <div className="validation-summary">{error}</div>}
      {requestEditSuccess ? <div className="validation-summary">{requestEditSuccess}</div> : null}
      {editCapability?.isOverrideActive ? (
        <div className="validation-summary" role="status">
          {`${TEXT.overrideActivePrefix} ${overrideUntilText}`}
        </div>
      ) : null}
      {!loading && appointment && !canEdit && <div className="validation-summary">{editBlockedMessage}</div>}

      {!loading && appointment && !canEdit ? (
        <section className="form-section reception-create-appointment-form">
          <div className="form-group">
            <label htmlFor="appointment-edit-request-reason">Lý do yêu cầu sửa</label>
            <textarea
              id="appointment-edit-request-reason"
              rows={3}
              className="input-field"
              value={requestEditReason}
              onChange={(event) => setRequestEditReason(event.target.value)}
              placeholder="Nhập lý do để quản đốc/kế toán/chủ xưởng xem xét mở khóa"
            />
          </div>
          <div className="form-footer">
            <button
              type="button"
              className="btn-primary-large"
              onClick={handleRequestEdit}
              disabled={requestingEdit || hasPendingRequest}
            >
              {requestingEdit ? TEXT.requestEditSubmitting : hasPendingRequest ? "Đang có yêu cầu chờ duyệt" : TEXT.requestEditSubmit}
            </button>
          </div>
        </section>
      ) : null}

      {!loading && appointment ? (
        <section className="form-section reception-create-appointment-form">
          <div className="reception-appt-section-head">
            <h4 className="reception-appt-section-title">{TEXT.requestListTitle}</h4>
          </div>
          {loadingEditRequests ? (
            <p className="reception-muted-note">Đang tải yêu cầu sửa...</p>
          ) : editRequests.length === 0 ? (
            <p className="reception-muted-note">Chưa có yêu cầu nào.</p>
          ) : (
            <div className="reception-plan-preview-list">
              {editRequests.map((request) => {
                const status = String(request.status || "").toUpperCase();
                const isPending = status === "PENDING";
                const deciding = decidingRequestId === request.requestId;
                const decisionNote = decisionNotesByRequestId[request.requestId] || "";
                return (
                  <article key={request.requestId} className="reception-plan-preview-item">
                    <div className="reception-plan-preview-head">
                      <strong>{`Yêu cầu #${request.requestId}`}</strong>
                      <span style={{ color: getRequestStatusColor(status), fontWeight: 700 }}>
                        {getRequestStatusLabel(status)}
                      </span>
                    </div>
                    <p>{`Phạm vi: ${request.section || "APPOINTMENT"}`}</p>
                    <p>{`Lý do: ${request.reason || TEXT.noRequestReason}`}</p>
                    <div className="reception-plan-preview-meta">
                      <span>{`Gửi lúc: ${formatDateTimeVi(request.requestedAtUtc)}`}</span>
                      <span>{`Xử lý lúc: ${formatDateTimeVi(request.decidedAtUtc)}`}</span>
                    </div>
                    {request.decisionNote ? <p>{`Ghi chú xử lý: ${request.decisionNote}`}</p> : null}
                    {canApproveRequests && isPending ? (
                      <div style={{ marginTop: 12 }}>
                        <textarea
                          rows={2}
                          className="input-field"
                          value={decisionNote}
                          onChange={(event) => handleDecisionNoteChange(request.requestId, event.target.value)}
                          placeholder={TEXT.decisionNotePlaceholder}
                        />
                        <div className="form-footer" style={{ marginTop: 10 }}>
                          <button
                            type="button"
                            className="btn-secondary-outline"
                            onClick={() => handleDecideRequest(request.requestId, false)}
                            disabled={deciding}
                          >
                            {TEXT.rejectButton}
                          </button>
                          <button
                            type="button"
                            className="btn-primary-large"
                            onClick={() => handleDecideRequest(request.requestId, true)}
                            disabled={deciding}
                          >
                            {deciding ? "Đang xử lý..." : TEXT.approveButton}
                          </button>
                        </div>
                      </div>
                    ) : null}
                  </article>
                );
              })}
            </div>
          )}
        </section>
      ) : null}

      {loading ? (
        <div className="workshop-table-card">
          <div className="loading-state">
            <div className="spinner" />
            <p>{TEXT.loadingData}</p>
          </div>
        </div>
      ) : appointment && initialValues && canEdit ? (
        <section className="form-section reception-create-appointment-form">
          <ReceptionCreateAppointmentForm
            customers={customers}
            vehicles={vehicles}
            customerVehicles={customerVehicles}
            serviceCategories={serviceCategories}
            services={services}
            branchId={targetBranchId}
            initialValues={initialValues}
            onErrorMessage={setError}
            onSubmit={handleSubmit}
            onCancel={handleGoBack}
            loading={saving}
            formId={editAppointmentFormId}
            showFooterActions={false}
            submitLabel={TEXT.submit}
          />
        </section>
      ) : !error ? (
        <div className="workshop-table-card">
          <p>{TEXT.notFound}</p>
        </div>
      ) : null}

      {successAppt ? (
        <div className="reception-create-success-backdrop" role="status" aria-live="polite">
          <div className="reception-create-success-popup">
            <span className="reception-create-success-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24" focusable="false">
                <path d="M9.2 16.3 4.9 12l1.4-1.4 2.9 2.9 8.5-8.5 1.4 1.4z" />
              </svg>
            </span>
            <h3>{TEXT.successTitle}</h3>
            <p>{TEXT.successMessage}</p>
          </div>
        </div>
      ) : null}
    </div>
  );
}





